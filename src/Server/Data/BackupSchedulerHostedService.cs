using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class BackupSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupSchedulerHostedService> _logger;
    private DateOnly? _lastAutomaticBackupDate;

    public BackupSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "اجرای بکاپ خودکار ناموفق بود.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
        var backupService = scope.ServiceProvider.GetRequiredService<DatabaseBackupService>();

        var stored = await database.AppSettings
            .AsNoTracking()
            .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
            .ToListAsync(cancellationToken);

        var values = ServerSettingsCatalog.BuildValues(stored);
        if (!TryReadBoolean(values, "backupAuto", out var auto) || !auto)
            return;

        if (!TryReadString(values, "backupHour", out var hour)
            || !TimeOnly.TryParseExact(hour, "HH:mm", out var configuredTime))
            return;

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (_lastAutomaticBackupDate == today)
            return;

        if (now.Hour != configuredTime.Hour || now.Minute != configuredTime.Minute)
            return;

        var result = await backupService.CreateBackupAsync(cancellationToken);
        _lastAutomaticBackupDate = today;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "backup.auto",
            EntityName = "DatabaseBackup",
            EntityId = result.FileName,
            Details = $"بکاپ خودکار موفق · {result.FileName}"
        });
        await database.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("بکاپ خودکار با موفقیت ایجاد شد: {BackupFile}", result.FileName);
    }

    private static bool TryReadBoolean(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> values,
        string key,
        out bool value)
    {
        if (values.TryGetValue(key, out var element)
            && (element.ValueKind == System.Text.Json.JsonValueKind.True
                || element.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> values,
        string key,
        out string value)
    {
        if (values.TryGetValue(key, out var element)
            && element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
