using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class AccountPoolService(
    GameNetDbContext database,
    GameCredentialProtectionService credentialProtection)
{
    private static readonly TimeSpan CredentialAccessWindow = TimeSpan.FromMinutes(5);

    public async Task<(AccountPoolEntry? Account, AccountLease? Lease)> AllocateAsync(
        Guid gameId, Guid? agentDeviceId, Guid? customerId, Guid? sessionId, CancellationToken cancellationToken)
    {
        var game = await database.Games.AsNoTracking().FirstOrDefaultAsync(item => item.Id == gameId && item.IsActive, cancellationToken);
        if (game is null) return (null, null);

        if (agentDeviceId.HasValue && !await database.AgentDevices.AnyAsync(item => item.Id == agentDeviceId.Value && item.IsActive, cancellationToken))
            throw new InvalidOperationException("ایستگاه/Agent انتخاب‌شده پیدا نشد.");
        if (customerId.HasValue && !await database.Customers.AnyAsync(item => item.Id == customerId.Value, cancellationToken))
            throw new InvalidOperationException("مشتری انتخاب‌شده پیدا نشد.");
        if (sessionId.HasValue && !await database.Sessions.AnyAsync(item => item.Id == sessionId.Value, cancellationToken))
            throw new InvalidOperationException("جلسه انتخاب‌شده پیدا نشد.");

        var gameIdText = game.Id.ToString();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = DateTime.UtcNow;
            var candidates = await database.AccountPoolEntries.AsNoTracking()
                .Where(item => item.Status == AccountPoolStatus.Free
                    && item.IsActive
                    && item.AllowedGameIdsCsv != null
                    && item.AllowedGameIdsCsv.Contains(gameIdText)
                    && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now))
                .ToListAsync(cancellationToken);

            var candidate = candidates
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefault(item =>
                    item.AllowedGameIdsCsv
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(gameIdText, StringComparer.OrdinalIgnoreCase));

            if (candidate is null) return (null, null);

            try
            {
                await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
                var updated = await database.AccountPoolEntries
                    .Where(item => item.Id == candidate.Id
                        && item.Status == AccountPoolStatus.Free
                        && item.IsActive
                        && (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, AccountPoolStatus.InUse)
                        .SetProperty(item => item.AssignedAgentDeviceId, agentDeviceId)
                        .SetProperty(item => item.UpdatedAt, now), cancellationToken);

                if (updated != 1) continue;

                var lease = new AccountLease
                {
                    AccountPoolEntryId = candidate.Id,
                    GameId = game.Id,
                    AgentDeviceId = agentDeviceId,
                    CustomerId = customerId,
                    SessionId = sessionId,
                    LeaseToken = AuthorizationService.CreateToken(),
                    LeasedAt = now,
                    CredentialAccessExpiresAt = now.Add(CredentialAccessWindow),
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

    public async Task<AgentGameAccountCredentialDto?> AcquireCredentialForOperationalSessionAsync(
        Guid sessionId,
        Guid agentDeviceId,
        CancellationToken cancellationToken)
    {
        var session = await database.Sessions.AsNoTracking()
            .Include(item => item.Game)
            .Include(item => item.AgentDevice)
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.State == SessionState.Active, cancellationToken);

        if (session?.GameId is null || session.Game is null)
            throw new InvalidOperationException("جلسهٔ فعال بازی معتبر ندارد.");
        if (session.AgentDeviceId != agentDeviceId || session.AgentDevice is null || !session.AgentDevice.IsActive || !session.AgentDevice.IsOnline)
            throw new InvalidOperationException("Agent درخواست‌کننده مالک این جلسه نیست یا آنلاین نیست.");
        if (session.StationId != session.AgentDevice.StationId)
            throw new InvalidOperationException("Agent و ایستگاه جلسه با هم منطبق نیستند.");

        var login = await database.CustomerLogins.AsNoTracking().FirstOrDefaultAsync(
            item => item.CustomerId == session.CustomerId
                && item.ClientKey == session.AgentDevice.DeviceId
                && item.IsActive,
            cancellationToken);
        if (login is null)
            throw new InvalidOperationException("ورود مشتری برای Agent این جلسه فعال نیست.");

        var lease = await database.AccountLeases
            .Include(item => item.AccountPoolEntry)
            .FirstOrDefaultAsync(
                item => item.SessionId == sessionId
                    && item.AgentDeviceId == agentDeviceId
                    && item.State == AccountLeaseState.Active,
                cancellationToken);

        if (lease is not null)
        {
            if (lease.CredentialAccessExpiresAt <= DateTimeOffset.UtcNow)
            {
                var renewedAt = DateTimeOffset.UtcNow;
                await database.AccountLeases
                    .Where(item => item.Id == lease.Id && item.State == AccountLeaseState.Active)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.CredentialAccessExpiresAt, renewedAt.Add(CredentialAccessWindow))
                        .SetProperty(item => item.UpdatedAt, renewedAt), cancellationToken);
                lease.CredentialAccessExpiresAt = renewedAt.Add(CredentialAccessWindow);
            }
        }
        else
        {
            var allocated = await AllocateForOperationalSessionAsync(sessionId, cancellationToken);
            if (allocated is null)
                return null;
            lease = allocated.Value.Lease;
        }

        return await GetCredentialAsync(
            lease.Id,
            lease.LeaseToken,
            agentDeviceId,
            cancellationToken);
    }

    public async Task<int> ReleaseActiveForSessionAsync(
        Guid sessionId,
        string releaseReason,
        CancellationToken cancellationToken)
    {
        var leaseIds = await database.AccountLeases
            .AsNoTracking()
            .Where(item => item.SessionId == sessionId && item.State == AccountLeaseState.Active)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var leaseId in leaseIds)
        {
            if (await ReleaseAsync(leaseId, releaseReason, cancellationToken) is not null)
                released++;
        }

        return released;
    }

    public async Task<int> ReleaseActiveForAgentAsync(
        Guid agentDeviceId,
        string releaseReason,
        CancellationToken cancellationToken)
    {
        var leaseIds = await database.AccountLeases
            .AsNoTracking()
            .Where(item => item.AgentDeviceId == agentDeviceId && item.State == AccountLeaseState.Active)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var leaseId in leaseIds)
        {
            if (await ReleaseAsync(leaseId, releaseReason, cancellationToken) is not null)
                released++;
        }

        return released;
    }

    public async Task<(AccountPoolEntry Account, AccountLease Lease)?> AllocateForOperationalSessionAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.Sessions.AsNoTracking()
            .Include(item => item.Game)
            .Include(item => item.AgentDevice)
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.State == SessionState.Active, cancellationToken);

        if (session?.GameId is null || session.Game is null)
            throw new InvalidOperationException("جلسهٔ عملیاتی باید بازی معتبر داشته باشد.");
        if (session.AgentDeviceId is null || session.AgentDevice is null)
            throw new InvalidOperationException("جلسهٔ عملیاتی به Agent معتبر متصل نیست.");
        if (!session.AgentDevice.IsActive || !session.AgentDevice.IsOnline)
            throw new InvalidOperationException("Agent این جلسه آنلاین نیست.");
        if (session.StationId != session.AgentDevice.StationId)
            throw new InvalidOperationException("Agent و ایستگاه جلسه با هم منطبق نیستند.");

        var login = await database.CustomerLogins.AsNoTracking().FirstOrDefaultAsync(
            item => item.CustomerId == session.CustomerId
                && item.ClientKey == session.AgentDevice.DeviceId
                && item.IsActive, cancellationToken);

        if (login is null)
            throw new InvalidOperationException("ورود مشتری برای Agent این جلسه فعال نیست.");

        var result = await AllocateAsync(
            session.GameId.Value,
            session.AgentDeviceId,
            session.CustomerId,
            session.Id,
            cancellationToken);

        if (result.Account is null || result.Lease is null) return null;
        if (string.IsNullOrWhiteSpace(result.Account.SecretCiphertext))
        {
            await ReleaseAsync(
                result.Lease.Id,
                "اکانت عملیاتی فاقد رمز امن بود.",
                cancellationToken);
            throw new InvalidOperationException("برای این اکانت رمز امن ثبت نشده است.");
        }

        return (result.Account, result.Lease);
    }

    public async Task<AgentGameAccountCredentialDto?> GetCredentialAsync(
        Guid leaseId,
        string leaseToken,
        Guid agentDeviceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leaseToken)) return null;

        var lease = await database.AccountLeases
            .AsNoTracking()
            .Include(item => item.AccountPoolEntry)
            .Include(item => item.Session)
            .FirstOrDefaultAsync(
                item => item.Id == leaseId
                    && item.AgentDeviceId == agentDeviceId
                    && item.State == AccountLeaseState.Active,
                cancellationToken);

        if (lease is null
            || !lease.SessionId.HasValue
            || lease.Session is null
            || lease.Session.State != SessionState.Active
            || !string.Equals(lease.LeaseToken, leaseToken, StringComparison.Ordinal)
            || !lease.CredentialAccessExpiresAt.HasValue
            || lease.CredentialAccessExpiresAt.Value <= DateTimeOffset.UtcNow
            || string.IsNullOrWhiteSpace(lease.AccountPoolEntry.SecretCiphertext))
            return null;

        try
        {
            var secret = credentialProtection.Unprotect(lease.AccountPoolEntry.SecretCiphertext);
            return new AgentGameAccountCredentialDto(
                lease.Id,
                lease.GameId,
                lease.AccountPoolEntry.Title,
                lease.AccountPoolEntry.Platform,
                lease.AccountPoolEntry.Login,
                secret,
                lease.CredentialAccessExpiresAt.Value);
        }
        catch
        {
            return null;
        }
    }

    public async Task<AccountLease?> ReleaseAsync(
        Guid leaseId,
        string? releaseReason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var lease = await database.AccountLeases.AsNoTracking().FirstOrDefaultAsync(item => item.Id == leaseId, cancellationToken);
        if (lease is null || lease.State != AccountLeaseState.Active) return null;

        var now = DateTimeOffset.UtcNow;
        var releasedLease = await database.AccountLeases
            .Where(item => item.Id == leaseId && item.State == AccountLeaseState.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, AccountLeaseState.Released)
                .SetProperty(item => item.ReleasedAt, now)
                .SetProperty(item => item.CredentialAccessExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.ReleaseReason, releaseReason)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);

        if (releasedLease != 1) return null;

        var releasedAccount = await database.AccountPoolEntries
            .Where(item => item.Id == lease.AccountPoolEntryId && item.Status == AccountPoolStatus.InUse)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, AccountPoolStatus.Free)
                .SetProperty(item => item.AssignedAgentDeviceId, (Guid?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);

        if (releasedAccount != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        lease.State = AccountLeaseState.Released;
        lease.ReleasedAt = now;
        lease.CredentialAccessExpiresAt = null;
        lease.ReleaseReason = releaseReason;
        lease.UpdatedAt = now;

        await transaction.CommitAsync(cancellationToken);
        return lease;
    }
}
