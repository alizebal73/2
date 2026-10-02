using GameNetManager.Server.Data;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Hubs;

public sealed class AgentHub(
    GameNetDbContext database,
    IConfiguration configuration,
    IHubContext<DashboardHub> dashboardHub,
    ILogger<AgentHub> logger) : Hub
{
    private const string AgentDeviceContextKey = "GameNet.AgentDeviceId";
    public override async Task OnConnectedAsync()
    {
        var device = await ResolveDeviceAsync(Context, Context.ConnectionAborted);
        if (device is null || !device.IsActive)
        {
            Context.Abort();
            return;
        }

        Context.Items[AgentDeviceContextKey] = device.Id;

        var now = DateTimeOffset.UtcNow;
        device.IsOnline = true;
        device.LastSeenAt = now;
        device.ConnectedAt = now;
        device.ConnectionId = Context.ConnectionId;
        device.LastIpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        device.LifecycleState = ClientLifecycleStates.Running;
        device.LifecycleStateChangedAt = now;
        device.LastHealthyAt = now;
        device.LastUpdateError = null;
        await database.SaveChangesAsync(Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            "AgentReady",
            new AgentReadyDto(
                device.Id,
                device.DeviceId,
                now,
                HeartbeatIntervalSeconds(),
                device.IsLocked,
                device.KioskEnabled,
                device.LockOnDisconnect),
            Context.ConnectionAborted);

        await BroadcastStatusAsync(device, now, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var device = await ResolveConnectedDeviceAsync(CancellationToken.None);
        if (device is not null && device.ConnectionId == Context.ConnectionId)
        {
            device.IsOnline = false;
            device.ConnectionId = null;
            device.ConnectedAt = null;
            if (device.LockOnDisconnect)
            {
                device.IsLocked = true;
                device.LockedAt = DateTimeOffset.UtcNow;
                logger.LogWarning("Agent {DeviceId} disconnected; LockOnDisconnect policy locked the device.", device.DeviceId);
            }
            await database.SaveChangesAsync();

            await BroadcastStatusAsync(device, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<AgentHeartbeatResponse> Heartbeat(AgentHeartbeatRequest request)
    {
        try
        {
            logger.LogInformation(
                "Agent heartbeat start. ConnectionId={ConnectionId}, AgentVersion={AgentVersion}",
                Context.ConnectionId,
                request.AgentVersion);

            var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);

            logger.LogInformation(
                "Agent heartbeat identity resolved. ConnectionId={ConnectionId}, DeviceId={DeviceId}, StoredConnectionId={StoredConnectionId}",
                Context.ConnectionId,
                device?.DeviceId,
                device?.ConnectionId);

            if (device is null)
                throw new HubException("دستگاه مجاز نیست.");

            if (!device.IsActive)
                throw new HubException("دستگاه غیرفعال است.");

            var now = DateTimeOffset.UtcNow;
            device.IsOnline = true;
            device.LastSeenAt = now;
            device.AgentVersion = request.AgentVersion?.Trim();
            device.OsVersion = request.OsVersion?.Trim();
            device.CpuUsagePercent = request.CpuUsagePercent is >= 0 and <= 100
                ? request.CpuUsagePercent
                : null;
            device.MemoryAvailableBytes = request.MemoryAvailableBytes is > 0
                ? request.MemoryAvailableBytes
                : null;            device.UptimeSeconds = request.UptimeSeconds is >= 0
                ? request.UptimeSeconds
                : null;
            if (!string.IsNullOrWhiteSpace(request.LifecycleState) && ClientLifecycleStates.IsKnown(request.LifecycleState))
            {
                var lifecycle = request.LifecycleState.Trim();
                if (!string.Equals(device.LifecycleState, lifecycle, StringComparison.Ordinal))
                {
                    device.LifecycleState = lifecycle;
                    device.LifecycleStateChangedAt = now;
                }

                if (string.Equals(lifecycle, ClientLifecycleStates.Running, StringComparison.Ordinal))
                    device.LastHealthyAt = now;
            }

            if (request.PendingUpdateVersion is not null)
                device.PendingUpdateVersion = string.IsNullOrWhiteSpace(request.PendingUpdateVersion)
                    ? null
                    : request.PendingUpdateVersion.Trim();

            if (request.LastUpdateError is not null)
                device.LastUpdateError = string.IsNullOrWhiteSpace(request.LastUpdateError)
                    ? null
                    : request.LastUpdateError.Trim()[..Math.Min(500, request.LastUpdateError.Trim().Length)];

            device.ConnectionId = Context.ConnectionId;

            if (request.IsLocked && !device.IsLocked)
            {
                device.IsLocked = true;
                device.LockedAt = now;
                logger.LogWarning("Agent reported a locked client state after command acknowledgement for {DeviceId}.", device.DeviceId);
            }

            await database.SaveChangesAsync(Context.ConnectionAborted);
            logger.LogInformation(
                "Agent heartbeat persisted. DeviceId={DeviceId}, LastSeenAt={LastSeenAt}",
                device.DeviceId,
                now);

            try
            {
                await BroadcastStatusAsync(device, now, Context.ConnectionAborted);
                logger.LogInformation(
                    "Agent heartbeat dashboard broadcast completed. DeviceId={DeviceId}",
                    device.DeviceId);
            }
            catch (Exception broadcastException)
            {
                logger.LogWarning(
                    broadcastException,
                    "Agent heartbeat persisted but Dashboard status broadcast failed for {DeviceId}.",
                    device.DeviceId);
            }

            return new AgentHeartbeatResponse(
                device.Id,
                now,
                HeartbeatIntervalSeconds(),
                true);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Agent heartbeat failed unexpectedly. ConnectionId={ConnectionId}",
                Context.ConnectionId);
            throw new HubException("خطا در ثبت وضعیت Agent.");
        }
    }

    public async Task AcknowledgeCommand(
        AgentCommandAcknowledgement acknowledgement)
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        var command = await database.AgentCommands
            .FirstOrDefaultAsync(
                item => item.Id == acknowledgement.CommandId
                    && item.AgentDeviceId == device.Id
                    && item.Status == "Sent"
                    && item.AgentConnectionId == Context.ConnectionId,
                Context.ConnectionAborted);

        if (command is null)
            throw new HubException("فرمان معتبر پیدا نشد.");

        command.Status = acknowledgement.Success ? "Succeeded" : "Failed";
        command.Succeeded = acknowledgement.Success;
        command.CompletedAt = acknowledgement.CompletedAt == default
            ? DateTimeOffset.UtcNow
            : acknowledgement.CompletedAt;
        command.ResultMessage = string.IsNullOrWhiteSpace(acknowledgement.Message)
            ? null
            : acknowledgement.Message.Trim();

        if (acknowledgement.Success && command.CommandType is AgentCommandTypes.Lock or AgentCommandTypes.Unlock or AgentCommandTypes.LogoutLock)
        {
            device.IsLocked = command.CommandType != AgentCommandTypes.Unlock;
            device.LockedAt = device.IsLocked ? command.CompletedAt : null;
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = acknowledgement.Success ? "AgentCommandSucceeded" : "AgentCommandFailed",
            EntityName = "AgentCommand",
            EntityId = command.Id.ToString(),
            Details = $"Agent {device.DeviceId} پاسخ فرمان {command.CommandType} را ثبت کرد."
        });

        await database.SaveChangesAsync(Context.ConnectionAborted);

        await dashboardHub.Clients.All.SendAsync(
            "AgentCommandUpdated",
            new AgentCommandStatusDto(
                command.Id,
                command.AgentDeviceId,
                command.CommandType,
                command.Status,
                command.RequestedAt,
                command.SentAt,
                command.CompletedAt,
                command.Succeeded,
                command.ResultMessage),
            Context.ConnectionAborted);
    }

    public async Task AgentLogoutAndLock()
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await database.Database.BeginTransactionAsync(Context.ConnectionAborted);

        var activeSession = device.StationId.HasValue
            ? await database.Sessions
                .Include(item => item.Station)
                .FirstOrDefaultAsync(
                    item => item.StationId == device.StationId.Value && item.State == SessionState.Active,
                    Context.ConnectionAborted)
            : null;

        if (activeSession is not null)
        {
            activeSession.EndAt = now;
            activeSession.State = SessionState.Ended;
            activeSession.Station.State = StationState.Available;

            var logins = await database.CustomerLogins
                .Where(item => item.IsActive && item.ClientKey == device.DeviceId)
                .ToListAsync(Context.ConnectionAborted);

            foreach (var login in logins)
            {
                login.IsActive = false;
                login.LoggedOutAt = now;
            }

            database.AuditLogs.Add(new AuditLog
            {
                Action = "AgentLogoutLockSessionEnd",
                EntityName = "Session",
                EntityId = activeSession.Id.ToString(),
                Details = $"خروج کاربر و قفل دستگاه · Agent {device.DeviceId}"
            });

            await database.SaveChangesAsync(Context.ConnectionAborted);

            await dashboardHub.Clients.All.SendAsync(
                "AgentSessionChanged",
                new
                {
                    sessionId = activeSession.Id,
                    stationId = activeSession.StationId,
                    customerId = activeSession.CustomerId,
                    state = "Ended",
                    changedAt = now
                },
                Context.ConnectionAborted);
        }

        device.IsLocked = true;
        device.LockedAt = now;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "AgentLogoutAndLock",
            EntityName = "AgentDevice",
            EntityId = device.Id.ToString(),
            Details = $"قفل دستگاه و خروج مشتری · {device.DeviceId}"
        });

        await database.SaveChangesAsync(Context.ConnectionAborted);
        await transaction.CommitAsync(Context.ConnectionAborted);
        await BroadcastStatusAsync(device, now, Context.ConnectionAborted);
    }

    public async Task<AgentSessionStartResponse> StartSession(
        AgentSessionStartRequest request)
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        if (!device.StationId.HasValue)
            throw new HubException("Agent به ایستگاه متصل نیست.");

        if (device.IsLocked)
            throw new HubException("دستگاه قفل است و شروع جلسه ممکن نیست.");

        var customer = await database.Customers
            .FirstOrDefaultAsync(item => item.Id == request.CustomerId, Context.ConnectionAborted);
        if (customer is null)
            throw new HubException("مشتری پیدا نشد.");

        var login = await database.CustomerLogins
            .FirstOrDefaultAsync(
                item => item.Id == request.CustomerLoginId
                    && item.CustomerId == customer.Id
                    && item.IsActive
                    && item.ClientKey == device.DeviceId,
                Context.ConnectionAborted);
        if (login is null)
            throw new HubException("ورود معتبر مشتری برای این دستگاه پیدا نشد.");

        var station = await database.Stations
            .Include(item => item.Tariff)
            .FirstOrDefaultAsync(item => item.Id == device.StationId.Value, Context.ConnectionAborted);
        if (station is null)
            throw new HubException("ایستگاه Agent پیدا نشد.");

        if (station.State != StationState.Available)
            throw new HubException("این ایستگاه دیگر آزاد نیست.");

        if (station.Tariff is null || !station.Tariff.IsActive)
            throw new HubException("تعرفهٔ فعال برای این ایستگاه تنظیم نشده است.");

        var persons = Math.Max(1, request.Persons ?? 1);
        if (station.Type.Equals("PC", StringComparison.OrdinalIgnoreCase)
            || station.Type.Contains("رایانه", StringComparison.OrdinalIgnoreCase))
            persons = 1;
        else if (persons > 4)
            throw new HubException("تعداد نفرات برای این ایستگاه بیش از حد مجاز است.");

        await using var transaction = await database.Database.BeginTransactionAsync(Context.ConnectionAborted);

        var session = new Session
        {
            CustomerId = customer.Id,
            StationId = station.Id,
            TariffId = station.TariffId,
            AppUserId = null,
            StartAt = DateTimeOffset.UtcNow,
            State = SessionState.Active,
            TotalAmount = 0m,
            // Customer/Agent cannot override the price. Server tariff is authoritative.
            HourlyRateOverride = null,
            Persons = persons
        };

        database.Sessions.Add(session);
        station.State = StationState.Occupied;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "AgentSessionStart",
            EntityName = "Session",
            EntityId = session.Id.ToString(),
            Details = $"شروع جلسه از Agent · دستگاه {device.DeviceId} · مشتری {customer.Id}"
        });

        await database.SaveChangesAsync(Context.ConnectionAborted);
        await transaction.CommitAsync(Context.ConnectionAborted);

        await dashboardHub.Clients.All.SendAsync(
            "AgentSessionChanged",
            new
            {
                sessionId = session.Id,
                stationId = station.Id,
                customerId = customer.Id,
                state = "Active",
                changedAt = session.StartAt
            },
            Context.ConnectionAborted);

        return new AgentSessionStartResponse(
            session.Id,
            station.Id,
            customer.Id,
            session.StartAt);
    }

    public async Task<AgentSessionEndResponse> EndSession(
        AgentSessionEndRequest request)
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        if (!device.StationId.HasValue)
            throw new HubException("Agent به ایستگاه متصل نیست.");

        var session = await database.Sessions
            .Include(item => item.Station)
            .FirstOrDefaultAsync(item => item.Id == request.SessionId, Context.ConnectionAborted);

        if (session is null)
            throw new HubException("جلسه پیدا نشد.");

        if (session.StationId != device.StationId.Value)
            throw new HubException("این جلسه متعلق به ایستگاه Agent نیست.");

        if (!request.CustomerLoginId.HasValue)
            throw new HubException("شناسهٔ ورود مشتری برای پایان جلسه الزامی است.");

        var login = await database.CustomerLogins.FirstOrDefaultAsync(
            item => item.Id == request.CustomerLoginId.Value
                && item.CustomerId == session.CustomerId
                && item.ClientKey == device.DeviceId,
            Context.ConnectionAborted);

        if (login is null)
            throw new HubException("ورود مشتری متعلق به این Agent پیدا نشد.");

        var now = DateTimeOffset.UtcNow;

        if (session.State == SessionState.Completed || session.State == SessionState.Cancelled)
            throw new HubException("این جلسه دیگر قابل پایان‌دادن نیست.");

        if (session.State == SessionState.Active && !login.IsActive)
            throw new HubException("ورود مشتری برای پایان این جلسه دیگر فعال نیست.");

        if (session.State == SessionState.Ended)
        {
            if (login.IsActive)
            {
                login.IsActive = false;
                login.LoggedOutAt = session.EndAt ?? now;
                await database.SaveChangesAsync(Context.ConnectionAborted);
            }

            return new AgentSessionEndResponse(
                session.Id,
                session.StationId,
                session.EndAt ?? now,
                session.State.ToString());
        }

        await using var transaction = await database.Database.BeginTransactionAsync(Context.ConnectionAborted);

        session.EndAt = now;
        session.State = SessionState.Ended;
        session.Station.State = StationState.Available;

        if (login.IsActive)
        {
            login.IsActive = false;
            login.LoggedOutAt = now;
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = "AgentSessionEnd",
            EntityName = "Session",
            EntityId = session.Id.ToString(),
            Details = $"پایان جلسه از Agent · دستگاه {device.DeviceId}"
        });

        await database.SaveChangesAsync(Context.ConnectionAborted);
        await transaction.CommitAsync(Context.ConnectionAborted);

        await dashboardHub.Clients.All.SendAsync(
            "AgentSessionChanged",
            new
            {
                sessionId = session.Id,
                stationId = session.StationId,
                customerId = session.CustomerId,
                state = "Ended",
                changedAt = now
            },
            Context.ConnectionAborted);

        return new AgentSessionEndResponse(
            session.Id,
            session.StationId,
            now,
            session.State.ToString());
    }

    private async Task<AgentDevice?> ResolveDeviceAsync(
        HubCallerContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.GetHttpContext();
        var deviceId = httpContext?.Request.Headers["X-GameNet-Device-Id"].ToString().Trim();
        var authorization = httpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(deviceId)
            || string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHash = PasswordSecurity.HashToken(token);
        return await database.AgentDevices
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.DeviceId == deviceId
                    && item.AgentTokenHash == tokenHash
                    && item.IsActive,
                cancellationToken);
    }

    private async Task<AgentDevice?> ResolveConnectedDeviceAsync(CancellationToken cancellationToken)
    {
        var connectionId = Context.ConnectionId;
        if (string.IsNullOrWhiteSpace(connectionId))
            return null;

        return await database.AgentDevices
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.ConnectionId == connectionId && item.IsActive,
                cancellationToken);
    }

    private int HeartbeatIntervalSeconds()
        => Math.Clamp(configuration.GetValue("Agent:HeartbeatIntervalSeconds", 10), 3, 60);

    private int OfflineAfterSeconds()
        => Math.Clamp(
            configuration.GetValue("Agent:OfflineAfterSeconds", 30),
            HeartbeatIntervalSeconds() * 2,
            300);

    private async Task BroadcastStatusAsync(
        AgentDevice device,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dashboardHub.Clients.All.SendAsync(
            "AgentStatusChanged",
            ToStatusDto(device, now),
            cancellationToken);
    }

    private AgentStatusDto ToStatusDto(AgentDevice device, DateTimeOffset now)
    {
        var online = device.IsActive
            && device.LastSeenAt.HasValue
            && now - device.LastSeenAt.Value <= TimeSpan.FromSeconds(OfflineAfterSeconds());

        return new AgentStatusDto(
            device.Id,
            device.DeviceId,
            device.Name,
            device.StationId,
            device.Station?.Name,
            online,
            device.IsLocked,
            device.KioskEnabled,
            device.LockOnDisconnect,
            device.LastSeenAt,
            device.ConnectedAt,
            device.AgentVersion,
            device.OsVersion,
            device.CpuUsagePercent,
            device.MemoryAvailableBytes,
            device.UptimeSeconds,
            device.LifecycleState,
            device.PendingUpdateVersion,
            device.LastUpdateError,
            device.LastHealthyAt,
            device.LifecycleStateChangedAt);
    }
}
