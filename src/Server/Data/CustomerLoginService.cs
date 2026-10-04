using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class CustomerLoginService(GameNetDbContext database)
{
    public async Task<ConcurrentLoginResultDto> AcquireAsync(
        Guid customerId,
        string clientKey,
        CancellationToken cancellationToken)
    {
        var normalizedClientKey = clientKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedClientKey))
            throw new ArgumentException("شناسه دستگاه وارد نشده است.", nameof(clientKey));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // SQLite does not provide row locks. A no-op UpdatedAt write upgrades
                // the transaction to a writer before we read the active-login count,
                // serializing concurrent acquisitions for the same Customer.
                var now = DateTimeOffset.UtcNow;
                var locked = await database.Customers
                    .Where(item => item.Id == customerId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);

                if (locked != 1)
                    throw new KeyNotFoundException("مشتری پیدا نشد.");

                var customer = await database.Customers
                    .AsNoTracking()
                    .FirstAsync(item => item.Id == customerId, cancellationToken);

                var active = await database.CustomerLogins
                    .Where(item => item.CustomerId == customerId && item.IsActive)
                    .ToListAsync(cancellationToken);

                var existing = active.FirstOrDefault(item => item.ClientKey == normalizedClientKey);
                if (existing is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new ConcurrentLoginResultDto(
                        true,
                        existing.Id,
                        active.Count,
                        Math.Max(1, customer.ConcurrentLoginLimit));
                }

                var limit = Math.Max(1, customer.ConcurrentLoginLimit);
                if (active.Count >= limit)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"CONCURRENT_LOGIN_LIMIT:{active.Count}:{limit}");
                }

                var login = new CustomerLogin
                {
                    CustomerId = customerId,
                    ClientKey = normalizedClientKey,
                    LoggedInAt = now,
                    IsActive = true
                };

                database.CustomerLogins.Add(login);
                database.AuditLogs.Add(new AuditLog
                {
                    Action = "CustomerLoginAcquire",
                    EntityName = "CustomerLogin",
                    EntityId = login.Id.ToString(),
                    Details = "ورود مشتری · دستگاه " + normalizedClientKey
                });

                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ConcurrentLoginResultDto(
                    true,
                    login.Id,
                    active.Count + 1,
                    limit);
            }
            catch (SqliteException ex) when (attempt == 0 && ex.SqliteErrorCode is 5 or 6)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await Task.Delay(25, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        throw new InvalidOperationException("ورود هم‌زمان مشتری انجام نشد.");
    }

    public async Task<(Guid LoginId, int ActiveCount)> ReleaseAsync(
        Guid customerId,
        string clientKey,
        CancellationToken cancellationToken)
    {
        var normalizedClientKey = clientKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedClientKey))
            throw new ArgumentException("شناسه دستگاه وارد نشده است.", nameof(clientKey));

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var locked = await database.Customers
                .Where(item => item.Id == customerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);

            if (locked != 1)
                throw new KeyNotFoundException("مشتری پیدا نشد.");

            var login = await database.CustomerLogins
                .FirstOrDefaultAsync(
                    item => item.CustomerId == customerId
                        && item.IsActive
                        && item.ClientKey == normalizedClientKey,
                    cancellationToken);

            if (login is null)
                throw new KeyNotFoundException("ورود فعال برای این دستگاه پیدا نشد.");

            login.IsActive = false;
            login.LoggedOutAt = now;
            database.AuditLogs.Add(new AuditLog
            {
                Action = "CustomerLoginRelease",
                EntityName = "CustomerLogin",
                EntityId = login.Id.ToString(),
                Details = "خروج مشتری · دستگاه " + normalizedClientKey
            });

            await database.SaveChangesAsync(cancellationToken);
            var activeCount = await database.CustomerLogins.CountAsync(
                item => item.CustomerId == customerId && item.IsActive,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (login.Id, activeCount);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
