using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/settings", async (
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequireAnyPermissionAsync(
                context,
                database,
                cancellationToken,
                "settings.view",
                "settings.manage");

            if (error is not null)
                return error;

            var stored = await database.AppSettings
                .AsNoTracking()
                .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
                .ToListAsync(cancellationToken);

            var values = ServerSettingsCatalog.BuildValues(stored);
            var definitions = ServerSettingsCatalog.Definitions.Select(item => new
            {
                item.Key,
                item.Title,
                item.Category,
                item.Type,
                item.DefaultValue
            });

            return Results.Ok(new
            {
                scope = ServerSettingsCatalog.GlobalScope,
                values,
                definitions,
                updatedAt = stored.Count == 0
                    ? (DateTimeOffset?)null
                    : stored.Max(item => item.UpdatedAt ?? item.CreatedAt)
            });
        }).WithName("GetServerSettings");

        app.MapPut("/api/settings", async (
            HttpContext context,
            GameNetDbContext database,
            UpdateServerSettingsRequest request,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequirePermissionAsync(
                context,
                database,
                "settings.manage",
                cancellationToken);

            if (error is not null)
                return error;

            if (request.Values is null || request.Values.Count == 0)
            {
                return Results.BadRequest(new
                {
                    code = "settings_empty",
                    message = "حداقل یک تنظیم برای ذخیره لازم است."
                });
            }

            if (request.Values.Count > ServerSettingsCatalog.Definitions.Count)
            {
                return Results.BadRequest(new
                {
                    code = "settings_too_many",
                    message = "تعداد تنظیمات ارسالی بیشتر از مقدار مجاز است."
                });
            }

            foreach (var item in request.Values)
            {
                if (!ServerSettingsCatalog.TryValidate(item.Key, item.Value, out var validationError))
                {
                    return Results.BadRequest(new
                    {
                        code = "settings_invalid",
                        key = item.Key,
                        message = validationError
                    });
                }
            }

            var existing = await database.AppSettings
                .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
                .ToDictionaryAsync(item => item.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var changedKeys = new List<string>();
            var now = DateTimeOffset.UtcNow;

            foreach (var item in request.Values)
            {
                var valueJson = item.Value.GetRawText();

                if (existing.TryGetValue(item.Key, out var stored))
                {
                    if (string.Equals(stored.ValueJson, valueJson, StringComparison.Ordinal))
                        continue;

                    stored.ValueJson = valueJson;
                    stored.UpdatedByUserId = user!.Id;
                    stored.UpdatedAt = now;
                    changedKeys.Add(item.Key);
                    continue;
                }

                database.AppSettings.Add(new AppSetting
                {
                    Key = ServerSettingsCatalog.Get(item.Key).Key,
                    ScopeKey = ServerSettingsCatalog.GlobalScope,
                    ValueJson = valueJson,
                    UpdatedByUserId = user!.Id,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                changedKeys.Add(item.Key);
            }

            if (changedKeys.Count > 0)
            {
                database.AuditLogs.Add(new AuditLog
                {
                    AppUserId = user!.Id,
                    Action = "settings.update",
                    EntityName = "AppSetting",
                    EntityId = ServerSettingsCatalog.GlobalScope,
                    Details = $"تنظیمات سروری تغییر کرد: {string.Join(", ", changedKeys)}"
                });
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var storedAfter = await database.AppSettings
                .AsNoTracking()
                .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                scope = ServerSettingsCatalog.GlobalScope,
                values = ServerSettingsCatalog.BuildValues(storedAfter),
                changedKeys
            });
        }).WithName("UpdateServerSettings");
    }
}

public sealed record UpdateServerSettingsRequest(
    Dictionary<string, JsonElement>? Values);
