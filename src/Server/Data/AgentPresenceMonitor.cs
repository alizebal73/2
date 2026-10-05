using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class AgentPresenceMonitor(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AgentPresenceMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var heartbeatInterval = Math.Clamp(
                configuration.GetValue("Agent:HeartbeatIntervalSeconds", 10),
                3,
                60);
            var offlineAfter = Math.Clamp(
                configuration.GetValue("Agent:OfflineAfterSeconds", 30),
                heartbeatInterval * 2,
                300);
            var commandTimeoutSeconds = Math.Clamp(
                configuration.GetValue("Agent:CommandTimeoutSeconds", 15),
                5,
                120);
            var updateCommandTimeoutSeconds = Math.Clamp(
                configuration.GetValue("Agent:UpdateCommandTimeoutSeconds", 180),
                30,
                600);
            var rollbackCommandTimeoutSeconds = Math.Clamp(
                configuration.GetValue("Agent:RollbackCommandTimeoutSeconds", 60),
                15,
                300);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
                var accountPool = scope.ServiceProvider.GetRequiredService<AccountPoolService>();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationQueueService>();
                var now = DateTimeOffset.UtcNow;
                var cutoff = now.AddSeconds(-offlineAfter);
                var commandCutoff = now.AddSeconds(-commandTimeoutSeconds);

                var notificationEvents = new List<NotificationEvent>();

                var onlineDevices = await database.AgentDevices
                    .Where(item => item.IsActive
                        && item.IsOnline
                        && item.LastSeenAt.HasValue)
                    .ToListAsync(stoppingToken);

                var staleDevices = onlineDevices
                    .Where(item => item.LastSeenAt!.Value < cutoff)
                    .ToList();

                var pendingCommands = await database.AgentCommands
                    .Where(command => command.Status == "Pending"
                        || command.Status == "Sent"
                        || command.Status == "AwaitingHealth")
                    .ToListAsync(stoppingToken);

                var timedOutCommands = pendingCommands
                    .Where(command =>
                    {
                        var timeoutSeconds = command.CommandType switch
                        {
                            AgentCommandTypes.Update => updateCommandTimeoutSeconds,
                            AgentCommandTypes.Rollback => rollbackCommandTimeoutSeconds,
                            _ => commandTimeoutSeconds
                        };

                        return (command.SentAt ?? command.RequestedAt)
                            < now.AddSeconds(-timeoutSeconds);
                    })
                    .ToList();

                if (staleDevices.Count > 0)
                {
                    foreach (var staleDevice in staleDevices)
                    {
                        var observedConnectionId = staleDevice.ConnectionId;
                        var observedLastSeenAt = staleDevice.LastSeenAt;

                        if (!staleDevice.IsActive
                            || !staleDevice.IsOnline
                            || !observedLastSeenAt.HasValue
                            || observedLastSeenAt.Value >= cutoff)
                        {
                            continue;
                        }

                        var affected = await database.AgentDevices
                            .Where(device => device.Id == staleDevice.Id
                                && device.IsActive
                                && device.IsOnline
                                && device.ConnectionId == observedConnectionId
                                && device.LastSeenAt == observedLastSeenAt)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(device => device.IsOnline, false)
                                .SetProperty(device => device.ConnectionId, (string?)null)
                                .SetProperty(device => device.LifecycleState, ClientLifecycleStates.Degraded)
                                .SetProperty(device => device.LifecycleStateChangedAt, now)
                                .SetProperty(device => device.IsLocked, device => device.LockOnDisconnect ? true : device.IsLocked)
                                .SetProperty(device => device.LockedAt, device => device.LockOnDisconnect ? now : device.LockedAt),
                                stoppingToken);

                        if (affected == 0)
                        {
                            // A fresh Agent connection/heartbeat changed the row after the stale list was materialized.
                            continue;
                        }

                        var device = await database.AgentDevices
                            .AsNoTracking()
                            .FirstOrDefaultAsync(item => item.Id == staleDevice.Id, stoppingToken);

                        if (device is null)
                            continue;

                        var releasedLeases = await accountPool.ReleaseActiveForAgentAsync(
                            device.Id,
                            "آزادسازی خودکار به دلیل stale شدن heartbeat Agent",
                            stoppingToken);
                        if (releasedLeases > 0)
                            logger.LogWarning("Stale Agent {DeviceId}; released {LeaseCount} active account lease(s).", device.DeviceId, releasedLeases);

                        if (device.LockOnDisconnect && device.IsLocked)
                        {
                            database.AuditLogs.Add(new AuditLog
                            {
                                Action = "AgentAutoLockOnDisconnect",
                                EntityName = "AgentDevice",
                                EntityId = device.Id.ToString(),
                                Details = $"Agent {device.DeviceId} به دلیل stale شدن heartbeat قفل شد."
                            });
                        }

                        notificationEvents.Add(new NotificationEvent(
                            "agent.offline",
                            "قطع اتصال Agent",
                            $"Agent {device.DeviceId} از دسترس خارج شد و وارد وضعیت {ClientLifecycleStates.Degraded} شد."
                                + (device.Station is null ? "" : $" ایستگاه: {device.Station.Name}."),
                            device.LockOnDisconnect && device.IsLocked ? NotificationLevel.Critical : NotificationLevel.Warning,
                            "AgentDevice",
                            device.Id.ToString()));

                        var unfinishedCommands = await database.AgentCommands
                            .Where(command => command.AgentDeviceId == device.Id
                                && (command.Status == "Pending" || command.Status == "Sent"))
                            .ToListAsync(stoppingToken);

                        foreach (var command in unfinishedCommands)
                        {
                            // Update/Rollback intentionally disconnect the Agent during a controlled restart.
                            // Keep AwaitingHealth alive until its dedicated command timeout.
                            if (command.Status == "AwaitingHealth")
                                continue;

                            command.Status = "Failed";
                            command.Succeeded = false;
                            command.CompletedAt = now;
                            command.ResultMessage = "Agent قبل از تکمیل فرمان از دسترس خارج شد.";

                            database.AuditLogs.Add(new AuditLog
                            {
                                Action = "AgentCommandFailed",
                                EntityName = "AgentCommand",
                                EntityId = command.Id.ToString(),
                                Details = $"Agent {device.DeviceId} قبل از تکمیل فرمان {command.CommandType} آفلاین شد."
                            });
                        }
                    }
                }

                foreach (var command in timedOutCommands)
                {
                    if (command.Status != "Pending"
                        && command.Status != "Sent"
                        && command.Status != "AwaitingHealth")
                        continue;

                    command.Status = "Failed";
                    command.Succeeded = false;
                    command.CompletedAt = now;
                    command.ResultMessage = "زمان پاسخ Agent برای فرمان تمام شد.";

                    database.AuditLogs.Add(new AuditLog
                    {
                        Action = "AgentCommandFailed",
                        EntityName = "AgentCommand",
                        EntityId = command.Id.ToString(),
                        Details = $"فرمان {command.CommandType} به دلیل timeout پاسخ نگرفت."
                    });

                    notificationEvents.Add(new NotificationEvent(
                        "agent.command-failed",
                        "خطای فرمان Agent",
                        $"فرمان {command.CommandType} برای Agent پاسخ نداد و Failed شد.",
                        command.CommandType is AgentCommandTypes.Update or AgentCommandTypes.Rollback
                            ? NotificationLevel.Critical
                            : NotificationLevel.Warning,
                        "AgentCommand",
                        command.Id.ToString()));
                }

                if (staleDevices.Count > 0 || timedOutCommands.Count > 0)
                {
                    await database.SaveChangesAsync(stoppingToken);

                    if (staleDevices.Count > 0)
                    {
                        logger.LogWarning(
                            "Marked {Count} stale Agent device(s) offline. Cutoff={Cutoff}",
                            staleDevices.Count,
                            cutoff);
                    }

                    if (timedOutCommands.Count > 0)
                    {
                        logger.LogWarning(
                            "Marked {Count} Agent command(s) failed by timeout. Cutoff={Cutoff}",
                            timedOutCommands.Count,
                            commandCutoff);
                    }

                    foreach (var notification in notificationEvents)
                    {
                        await notifications.PublishToPermissionAsync(
                            "client.control",
                            notification,
                            stoppingToken);
                    }
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(Math.Min(offlineAfter, 15), 3, 15)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Agent presence monitor failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
