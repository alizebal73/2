using Microsoft.Extensions.Hosting;

namespace GameNetManager.Server.Data;

public sealed class BackupScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<BackupScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var backups = scope.ServiceProvider.GetRequiredService<BackupService>();

                var settings = await backups.GetSettingsAsync(stoppingToken);
                if (settings.Enabled
                    && TimeOnly.TryParse(settings.Hour, out var targetTime)
                    && DateTime.Now.TimeOfDay >= targetTime.ToTimeSpan()
                    && !string.Equals(
                        settings.LastAutoBackupDate,
                        DateTime.Now.ToString("yyyy-MM-dd"),
                        StringComparison.Ordinal))
                {
                    await backups.CreateBackupAsync(null, stoppingToken);
                    logger.LogInformation("Automatic GameNet backup completed.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Automatic GameNet backup failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
