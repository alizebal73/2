using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class AccountPoolService(GameNetDbContext database)
{
    public async Task<(AccountPoolEntry? Account, AccountLease? Lease)> AllocateAsync(
        Guid gameId,
        Guid? agentDeviceId,
        Guid? customerId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var game = await database.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == gameId && item.IsActive, cancellationToken);

        if (game is null)
            return (null, null);

        if (agentDeviceId.HasValue
            && !await database.AgentDevices.AnyAsync(item => item.Id == agentDeviceId.Value && item.IsActive, cancellationToken))
            throw new InvalidOperationException("ایستگاه/Agent انتخاب‌شده پیدا نشد.");

        if (customerId.HasValue
            && !await database.Customers.AnyAsync(item => item.Id == customerId.Value, cancellationToken))
            throw new InvalidOperationException("مشتری انتخاب‌شده پیدا نشد.");

        if (sessionId.HasValue
            && !await database.Sessions.AnyAsync(item => item.Id == sessionId.Value, cancellationToken))
            throw new InvalidOperationException("جلسه انتخاب‌شده پیدا نشد.");

        var gameIdText = game.Id.ToString();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidates = await database.AccountPoolEntries
                .AsNoTracking()
                .Where(item => item.Status == AccountPoolStatus.Free
                    && item.IsActive
                    && item.AllowedGameIdsCsv != null
                    && item.AllowedGameIdsCsv.Contains(gameIdText))
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            var candidate = candidates.FirstOrDefault(item =>
                item.AllowedGameIdsCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(gameIdText, StringComparer.OrdinalIgnoreCase));

            if (candidate is null)
                return (null, null);

            try
            {
                await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

                var now = DateTimeOffset.UtcNow;
                var updated = await database.AccountPoolEntries
                    .Where(item => item.Id == candidate.Id
                        && item.Status == AccountPoolStatus.Free
                        && item.IsActive)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, AccountPoolStatus.InUse)
                        .SetProperty(item => item.AssignedAgentDeviceId, agentDeviceId)
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);

                if (updated != 1)
                    return (null, null);

                var lease = new AccountLease
                {
                    AccountPoolEntryId = candidate.Id,
                    GameId = game.Id,
                    AgentDeviceId = agentDeviceId,
                    CustomerId = customerId,
                    SessionId = sessionId,
                    LeaseToken = AuthorizationService.CreateToken(),
                    LeasedAt = now,
                    State = AccountLeaseState.Active
                };

                database.AccountLeases.Add(lease);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                candidate.Status = AccountPoolStatus.InUse;
                candidate.AssignedAgentDeviceId = agentDeviceId;
                return (candidate, lease);
            }
            catch (SqliteException ex) when (attempt == 0 && ex.SqliteErrorCode is 5 or 6)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        return (null, null);
    }

    public async Task<AccountLease?> ReleaseAsync(Guid leaseId, string? releaseReason, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var lease = await database.AccountLeases
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == leaseId, cancellationToken);

        if (lease is null || lease.State != AccountLeaseState.Active)
            return null;

        var now = DateTimeOffset.UtcNow;
        var releasedLease = await database.AccountLeases
            .Where(item => item.Id == leaseId && item.State == AccountLeaseState.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, AccountLeaseState.Released)
                .SetProperty(item => item.ReleasedAt, now)
                .SetProperty(item => item.ReleaseReason, releaseReason)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);

        if (releasedLease != 1)
            return null;

        var releasedAccount = await database.AccountPoolEntries
            .Where(item => item.Id == lease.AccountPoolEntryId
                && item.Status == AccountPoolStatus.InUse)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, AccountPoolStatus.Free)
                .SetProperty(item => item.AssignedAgentDeviceId, (Guid?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);

        if (releasedAccount != 1)
            return null;

        lease.State = AccountLeaseState.Released;
        lease.ReleasedAt = now;
        lease.ReleaseReason = releaseReason;
        lease.UpdatedAt = now;

        await transaction.CommitAsync(cancellationToken);
        return lease;
    }
}
