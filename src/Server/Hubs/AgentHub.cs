using GameNetManager.Server.Data;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Hubs;

public sealed class AgentHub(
    GameNetDbContext database,
    IConfiguration configuration,
    IHubContext<DashboardHub> dashboardHub,
    ILogger<AgentHub> logger,
    AccountPoolService accountPool,
    SessionPricingService pricingService) : Hub
{
    private const string AgentDeviceContextKey = "GameNet.AgentDeviceId";

    public static string DeviceGroup(Guid deviceId) => $"agent-device:{deviceId:N}";
    public override async Task OnConnectedAsync()
    {
        var device = await ResolveDeviceAsync(Context, Context.ConnectionAborted);
        if (device is null || !device.IsActive)
        {
            Context.Abort();
            return;
        }

        // Authentication is established here, but the device is not marked online
        // until the client explicitly confirms the bidirectional SignalR connection.
        // This prevents REST callers from racing a not-yet-ready server-to-client channel.
        Context.Items[AgentDeviceContextKey] = device.Id;
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DeviceGroup(device.Id),
            Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public async Task<AgentReadyDto> ConfirmConnection()
    {
        var device = await ResolveDeviceAsync(Context, Context.ConnectionAborted);
        if (device is null || !device.IsActive)
            throw new HubException("دستگاه مجاز نیست.");

        Context.Items[AgentDeviceContextKey] = device.Id;

        // Re-add the active connection to the durable per-device group from inside
        // the already-established bidirectional hub channel.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DeviceGroup(device.Id),
            Context.ConnectionAborted);

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

        await BroadcastStatusAsync(device, now, Context.ConnectionAborted);

        return new AgentReadyDto(
            device.Id,
            device.DeviceId,
            now,
            HeartbeatIntervalSeconds(),
            device.IsLocked,
            device.KioskEnabled,
            device.LockOnDisconnect);
    }

    public async Task<AgentCommandEnvelope?> GetPendingLifecycleCommand()
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        var candidateId = await database.AgentCommands
            .Where(item => item.AgentDeviceId == device.Id
                && item.Status == "Sent"
                && item.AgentConnectionId != Context.ConnectionId
                && (item.CommandType == AgentCommandTypes.Update
                    || item.CommandType == AgentCommandTypes.Rollback))
            .OrderByDescending(item => item.RequestedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(Context.ConnectionAborted);

        if (!candidateId.HasValue)
            return null;

        var claimed = await database.AgentCommands
            .Where(item => item.Id == candidateId.Value
                && item.AgentDeviceId == device.Id
                && item.Status == "Sent"
                && item.AgentConnectionId != Context.ConnectionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AgentConnectionId, Context.ConnectionId),
                Context.ConnectionAborted);

        if (claimed != 1)
            return null;

        var command = await database.AgentCommands
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == candidateId.Value, Context.ConnectionAborted);

        if (command is null)
            return null;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "AgentCommandRecoveredOnReconnect",
            EntityName = "AgentCommand",
            EntityId = command.Id.ToString(),
            Details = $"فرمان چرخه عمر {command.CommandType} پس از اتصال مجدد Agent برای بازیابی دریافت شد · {device.DeviceId}"
        });
        await database.SaveChangesAsync(Context.ConnectionAborted);

        return new AgentCommandEnvelope(
            command.Id,
            command.CommandType,
            command.PayloadJson,
            command.RequestedAt);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var now = DateTimeOffset.UtcNow;
        var connectionId = Context.ConnectionId;

        if (Context.Items.TryGetValue(AgentDeviceContextKey, out var rawDeviceId)
            && rawDeviceId is Guid deviceId)
        {
            var disconnected = await database.AgentDevices
                .Where(item => item.Id == deviceId
                    && item.IsActive
                    && item.IsOnline
                    && item.ConnectionId == connectionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.IsOnline, false)
                    .SetProperty(item => item.ConnectionId, (string?)null)
                    .SetProperty(item => item.ConnectedAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.LifecycleState, ClientLifecycleStates.Degraded)
                    .SetProperty(item => item.LifecycleStateChangedAt, now)
                    .SetProperty(item => item.IsLocked, item => item.LockOnDisconnect || item.IsLocked)
                    .SetProperty(item => item.LockedAt, item => item.LockOnDisconnect ? now : item.LockedAt));

            if (disconnected > 0)
            {
                var device = await database.AgentDevices
                    .AsNoTracking()
                    .Include(item => item.Station)
                    .FirstOrDefaultAsync(item => item.Id == deviceId);

                if (device is not null)
                {
                    if (device.LockOnDisconnect)
                        logger.LogWarning("Agent {DeviceId} disconnected; LockOnDisconnect policy locked the device.", device.DeviceId);

                    var releasedLeases = await accountPool.ReleaseActiveForAgentAsync(
                        device.Id,
                        "آزادسازی خودکار به دلیل قطع ارتباط Agent",
                        CancellationToken.None);
                    if (releasedLeases > 0)
                        logger.LogWarning("Agent {DeviceId} disconnected; released {LeaseCount} active account lease(s).", device.DeviceId, releasedLeases);

                    await BroadcastStatusAsync(device, now, CancellationToken.None);
                }
            }
            else
            {
                logger.LogInformation(
                    "Ignoring stale Agent disconnect. DeviceId={DeviceId}, ConnectionId={ConnectionId}",
                    deviceId,
                    connectionId);
            }
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

            // Heartbeat is also a group-healing path. Any transient group loss is
            // repaired from the currently authenticated SignalR connection.
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                DeviceGroup(device.Id),
                Context.ConnectionAborted);

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
                var serverOwnsConnectionState = device.IsOnline;

                if (lifecycle is ClientLifecycleStates.UpdatePending
                    or ClientLifecycleStates.Updating
                    or ClientLifecycleStates.Failed)
                {
                    if (!string.Equals(device.LifecycleState, lifecycle, StringComparison.Ordinal))
                    {
                        device.LifecycleState = lifecycle;
                        device.LifecycleStateChangedAt = now;
                    }
                }
                else if (serverOwnsConnectionState
                    && !string.Equals(device.LifecycleState, ClientLifecycleStates.Running, StringComparison.Ordinal))
                {
                    device.LifecycleState = ClientLifecycleStates.Running;
                    device.LifecycleStateChangedAt = now;
                }

                if (string.Equals(device.LifecycleState, ClientLifecycleStates.Running, StringComparison.Ordinal))
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
                    && (
                        (!acknowledgement.Final
                            && item.Status == "Sent"
                            && item.AgentConnectionId == Context.ConnectionId
                            && (item.CommandType == AgentCommandTypes.Update
                                || item.CommandType == AgentCommandTypes.Rollback
                                || item.CommandType == AgentCommandTypes.Restart
                                || item.CommandType == AgentCommandTypes.Shutdown))
                        || (acknowledgement.Final
                            && (
                                ((item.CommandType == AgentCommandTypes.Update
                                    || item.CommandType == AgentCommandTypes.Rollback)
                                    && (item.Status == "Sent"
                                        || item.Status == "Accepted"
                                        || item.Status == "AwaitingHealth"))
                                || ((item.CommandType == AgentCommandTypes.Restart
                                    || item.CommandType == AgentCommandTypes.Shutdown)
                                    && item.Status == "AwaitingHealth")
                                || (item.CommandType != AgentCommandTypes.Update
                                    && item.CommandType != AgentCommandTypes.Rollback
                                    && item.CommandType != AgentCommandTypes.Restart
                                    && item.CommandType != AgentCommandTypes.Shutdown
                                    && item.Status == "Sent")
                            ))
                    ),
                Context.ConnectionAborted);

        if (command is null)
            throw new HubException("فرمان معتبر یا در انتظار نتیجه پیدا نشد.");

        var now = acknowledgement.CompletedAt == default
            ? DateTimeOffset.UtcNow
            : acknowledgement.CompletedAt;

        command.AgentConnectionId = Context.ConnectionId;

        if (!acknowledgement.Final)
        {
            command.Status = "AwaitingHealth";
            command.Succeeded = null;
            command.CompletedAt = null;
            command.ResultMessage = string.IsNullOrWhiteSpace(acknowledgement.Message)
                ? "فرمان دریافت شد و در انتظار تأیید سلامت پس از راه‌اندازی مجدد است."
                : acknowledgement.Message.Trim();

            database.AuditLogs.Add(new AuditLog
            {
                Action = "AgentCommandAccepted",
                EntityName = "AgentCommand",
                EntityId = command.Id.ToString(),
                Details = $"Agent {device.DeviceId} پذیرش اولیه فرمان {command.CommandType} را ثبت کرد."
            });
        }
        else
        {
            var requestedStatus = acknowledgement.FinalStatus?.Trim();
            var finalStatus = command.CommandType is AgentCommandTypes.Update or AgentCommandTypes.Rollback
                && string.Equals(requestedStatus, "RolledBack", StringComparison.OrdinalIgnoreCase)
                ? "RolledBack"
                : acknowledgement.Success ? "Succeeded" : "Failed";

            command.Status = finalStatus;
            command.Succeeded = acknowledgement.Success;
            command.CompletedAt = now;
            command.ResultMessage = string.IsNullOrWhiteSpace(acknowledgement.Message)
                ? null
                : acknowledgement.Message.Trim();

            if (command.CommandType is AgentCommandTypes.Update or AgentCommandTypes.Rollback)
            {
                device.PendingUpdateVersion = null;
                device.LastUpdateError = acknowledgement.Success ? null : command.ResultMessage;
                device.LifecycleState = acknowledgement.Success || finalStatus == "RolledBack"
                    ? ClientLifecycleStates.Running
                    : ClientLifecycleStates.Failed;
                device.LifecycleStateChangedAt = now;
            }

            database.AuditLogs.Add(new AuditLog
            {
                Action = acknowledgement.Success ? "AgentCommandSucceeded" : "AgentCommandFailed",
                EntityName = "AgentCommand",
                EntityId = command.Id.ToString(),
                Details = finalStatus == "RolledBack"
                    ? $"Agent {device.DeviceId} فرمان {command.CommandType} را پس از شکست Update و Rollback موفق، به وضعیت RolledBack نهایی کرد."
                    : $"Agent {device.DeviceId} نتیجه نهایی فرمان {command.CommandType} را ثبت کرد."
            });
        }

        if (acknowledgement.Final
            && acknowledgement.Success
            && command.CommandType is AgentCommandTypes.Lock or AgentCommandTypes.Unlock or AgentCommandTypes.LogoutLock)
        {
            device.IsLocked = command.CommandType != AgentCommandTypes.Unlock;
            device.LockedAt = device.IsLocked ? command.CompletedAt : null;
        }

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

    public async Task<AgentGameAccountCredentialDto> AcquireGameAccount(Guid sessionId)
    {
        var device = await ResolveConnectedDeviceAsync(Context.ConnectionAborted);
        if (device is null)
            throw new HubException("دستگاه مجاز نیست.");

        try
        {
            var credential = await accountPool.AcquireCredentialForOperationalSessionAsync(
                sessionId,
                device.Id,
                Context.ConnectionAborted);
            if (credential is null)
                throw new HubException("اکانت آزاد و سازگار برای بازی این جلسه وجود ندارد.");

            return credential;
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
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
                    item => item.StationId == device.StationId.Value
                        && item.AgentDeviceId == device.Id
                        && item.State == SessionState.Active,
                    Context.ConnectionAborted)
            : null;

        if (activeSession is not null)
        {
            activeSession.EndAt = now;
            activeSession.State = SessionState.Ended;
            activeSession.Station.State = StationState.Available;

            CustomerLogin? sessionLogin = null;
            if (activeSession.CustomerLoginId.HasValue)
            {
                sessionLogin = await database.CustomerLogins
                    .FirstOrDefaultAsync(
                        item => item.Id == activeSession.CustomerLoginId.Value
                            && item.CustomerId == activeSession.CustomerId
                            && item.ClientKey == device.DeviceId,
                        Context.ConnectionAborted);
            }
            else
            {
                // Legacy sessions created before CustomerLoginId was introduced.
                sessionLogin = await database.CustomerLogins
                    .FirstOrDefaultAsync(
                        item => item.CustomerId == activeSession.CustomerId
                            && item.ClientKey == device.DeviceId
                            && item.IsActive,
                        Context.ConnectionAborted);
            }

            if (sessionLogin is not null && sessionLogin.IsActive)
            {
                sessionLogin.IsActive = false;
                sessionLogin.LoggedOutAt = now;
            }

            database.AuditLogs.Add(new AuditLog
            {
                Action = "AgentLogoutLockSessionEnd",
                EntityName = "Session",
                EntityId = activeSession.Id.ToString(),
                Details = $"خروج کاربر و قفل دستگاه · Agent {device.DeviceId}"
            });

            await accountPool.ReleaseActiveForSessionWithinTransactionAsync(
                activeSession.Id,
                "آزادسازی خودکار با خروج و قفل Agent",
                Context.ConnectionAborted);

            await database.SaveChangesAsync(Context.ConnectionAborted);
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

        if (activeSession is not null)
        {
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

        if (await database.Sessions.AnyAsync(
                item => item.CustomerLoginId == login.Id && item.State == SessionState.Active,
                Context.ConnectionAborted))
            throw new HubException("این ورود مشتری از قبل یک Session فعال دارد.");

        Game? game = null;
        if (request.GameId.HasValue)
        {
            game = await database.Games.FirstOrDefaultAsync(
                item => item.Id == request.GameId.Value && item.IsActive,
                Context.ConnectionAborted);
            if (game is null)
                throw new HubException("بازی انتخاب‌شده پیدا نشد یا غیرفعال است.");
        }

        var persons = Math.Max(1, request.Persons ?? 1);

        await using var transaction = await database.Database.BeginTransactionAsync(Context.ConnectionAborted);

        var now = DateTimeOffset.UtcNow;
        var claimedStation = await database.Stations
            .Where(item => item.Id == device.StationId.Value
                && item.IsActive
                && item.State == StationState.Available)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, StationState.Occupied)
                .SetProperty(item => item.UpdatedAt, now), Context.ConnectionAborted);

        if (claimedStation != 1)
            throw new HubException("این ایستگاه دیگر آزاد نیست.");

        var station = await database.Stations
            .Include(item => item.Tariff)
            .FirstOrDefaultAsync(item => item.Id == device.StationId.Value, Context.ConnectionAborted);

        if (station is null)
            throw new HubException("ایستگاه Agent پیدا نشد.");

        if (station.Tariff is null || !station.Tariff.IsActive)
            throw new HubException("تعرفهٔ فعال برای این ایستگاه تنظیم نشده است.");

        var pricing = await pricingService.GetPricingAsync(
            customer.Id,
            station.Id,
            now,
            Context.ConnectionAborted);

        if (station.Type.Equals("PC", StringComparison.OrdinalIgnoreCase)
            || station.Type.Contains("رایانه", StringComparison.OrdinalIgnoreCase))
            persons = 1;
        else if (persons > 4)
            throw new HubException("تعداد نفرات برای این ایستگاه بیش از حد مجاز است.");

        var session = new Session
        {
            CustomerId = customer.Id,
            CustomerLoginId = login.Id,
            StationId = station.Id,
            TariffId = station.TariffId,
            AppUserId = null,
            StartAt = now,
            State = SessionState.Active,
            TotalAmount = 0m,
            HourlyRateSnapshot = pricing.HourlyRate,
            GameId = game?.Id,
            AgentDeviceId = device.Id,
            // Customer/Agent cannot override the price. Server tariff is authoritative.
            HourlyRateOverride = null,
            Persons = persons
        };

        database.Sessions.Add(session);

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
            .Include(item => item.CustomerLogin)
            .FirstOrDefaultAsync(item => item.Id == request.SessionId, Context.ConnectionAborted);

        if (session is null)
            throw new HubException("جلسه پیدا نشد.");

        if (session.AgentDeviceId != device.Id)
            throw new HubException("این Session متعلق به Agent درخواست‌کننده نیست.");
        if (session.StationId != device.StationId.Value)
            throw new HubException("این جلسه متعلق به ایستگاه Agent نیست.");

        if (!request.CustomerLoginId.HasValue)
            throw new HubException("شناسهٔ ورود مشتری برای پایان جلسه الزامی است.");
        if (!session.CustomerLoginId.HasValue || session.CustomerLoginId.Value != request.CustomerLoginId.Value)
            throw new HubException("ورود مشتری ثبت‌شده برای این Session با درخواست پایان جلسه یکسان نیست.");

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
            await using var idempotentTransaction = await database.Database.BeginTransactionAsync(Context.ConnectionAborted);

            if (login.IsActive)
            {
                login.IsActive = false;
                login.LoggedOutAt = session.EndAt ?? now;
            }

            await accountPool.ReleaseActiveForSessionWithinTransactionAsync(
                session.Id,
                "آزادسازی خودکار با پایان Session",
                Context.ConnectionAborted);

            await database.SaveChangesAsync(Context.ConnectionAborted);
            await idempotentTransaction.CommitAsync(Context.ConnectionAborted);

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

        // Lease release is part of the same transaction as the terminal
        // Session transition, so EndSession cannot commit with an InUse lease.
        await accountPool.ReleaseActiveForSessionWithinTransactionAsync(
            session.Id,
            "آزادسازی خودکار با پایان Session",
            Context.ConnectionAborted);

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
