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

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
                var now = DateTimeOffset.UtcNow;
                var cutoff = now.AddSeconds(-offlineAfter);
                var commandCutoff = now.AddSeconds(-commandTimeoutSeconds);

                var staleDevices = await database.AgentDevices
                    .Where(item => item.IsActive
                        && item.IsOnline
                        && item.LastSeenAt.HasValue
                        && item.LastSeenAt.Value < cutoff)
                    .ToListAsync(stoppingToken);

                var timedOutCommands = await database.AgentCommands
                    .Where(command =>
                        (command.Status == "Pending" || command.Status == "Sent")
                        && (command.SentAt ?? command.RequestedAt) < commandCutoff)
                    .ToListAsync(stoppingToken);

                if (staleDevices.Count > 0)
                {
                    foreach (var device in staleDevices)
                    {
                        device.IsOnline = false;
                        device.ConnectionId = null;

                        var unfinishedCommands = await database.AgentCommands
                            .Where(command => command.AgentDeviceId == device.Id
                                && (command.Status == "Pending" || command.Status == "Sent"))
                            .ToListAsync(stoppingToken);

                        foreach (var command in unfinishedCommands)
                        {
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
