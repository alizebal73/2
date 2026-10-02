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

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddSeconds(-offlineAfter);

                var staleDevices = await database.AgentDevices
                    .Where(item => item.IsActive
                        && item.IsOnline
                        && item.LastSeenAt.HasValue
                        && item.LastSeenAt.Value < cutoff)
                    .ToListAsync(stoppingToken);

                if (staleDevices.Count > 0)
                {
                    foreach (var device in staleDevices)
                    {
                        device.IsOnline = false;
                        device.ConnectionId = null;
                    }

                    await database.SaveChangesAsync(stoppingToken);
                    logger.LogWarning(
                        "Marked {Count} stale Agent device(s) offline. Cutoff={Cutoff}",
                        staleDevices.Count,
                        cutoff);
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
