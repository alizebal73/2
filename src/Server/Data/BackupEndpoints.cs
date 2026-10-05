using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/backup", async (
            HttpContext context,
            GameNetDbContext database,
            DatabaseBackupService backups,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequireAnyPermissionAsync(
                context,
                database,
                cancellationToken,
                "backup.view",
                "backup.manage",
                "backup.restore");
            if (error is not null) return error;

            var items = await backups.ListBackupsAsync(cancellationToken);
            return Results.Ok(new
            {
                items,
                canManage = AuthorizationService.HasPermission(user!, "backup.manage"),
                canRestore = AuthorizationService.HasPermission(user!, "backup.restore")
            });
        }).WithName("ListBackups");

        app.MapPost("/api/backup/create", async (
            HttpContext context,
            GameNetDbContext database,
            DatabaseBackupService backups,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequirePermissionAsync(
                context,
                database,
                "backup.manage",
                cancellationToken);
            if (error is not null) return error;

            var result = await backups.CreateBackupAsync(cancellationToken);
            database.AuditLogs.Add(new AuditLog
            {
                AppUserId = user!.Id,
                Action = "backup.create",
                EntityName = "DatabaseBackup",
                EntityId = result.FileName,
                Details = $"ایجاد نسخهٔ پشتیبان · {result.FileName}"
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Ok(result);
        }).WithName("CreateBackup");

        app.MapPost("/api/backup/{fileName}/verify", async (
            string fileName,
            HttpContext context,
            GameNetDbContext database,
            DatabaseBackupService backups,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequireAnyPermissionAsync(
                context,
                database,
                cancellationToken,
                "backup.view",
                "backup.manage",
                "backup.restore");
            if (error is not null) return error;

            var result = await backups.VerifyBackupAsync(fileName, cancellationToken);
            if (result.Valid)
            {
                database.AuditLogs.Add(new AuditLog
                {
                    AppUserId = user!.Id,
                    Action = "backup.verify",
                    EntityName = "DatabaseBackup",
                    EntityId = fileName,
                    Details = "اعتبارسنجی نسخهٔ پشتیبان موفق بود."
                });
                await database.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { valid = true, message = result.Message });
            }

            return Results.BadRequest(new { valid = false, message = result.Message });
        }).WithName("VerifyBackup");

        app.MapPost("/api/backup/{fileName}/restore", async (
            string fileName,
            HttpContext context,
            GameNetDbContext database,
            DatabaseBackupService backups,
            CancellationToken cancellationToken) =>
        {
            var (user, error) = await AuthorizationService.RequirePermissionAsync(
                context,
                database,
                "backup.restore",
                cancellationToken);
            if (error is not null) return error;

            var result = await backups.PrepareRestoreAsync(
                fileName,
                user!.Id,
                cancellationToken);

            database.AuditLogs.Add(new AuditLog
            {
                AppUserId = user.Id,
                Action = "backup.restore.requested",
                EntityName = "DatabaseBackup",
                EntityId = fileName,
                Details = "درخواست بازیابی ثبت شد؛ اعمال در راه‌اندازی بعدی Server."
            });
            await database.SaveChangesAsync(cancellationToken);

            return Results.Accepted($"/api/backup/{Uri.EscapeDataString(fileName)}", result);
        }).WithName("PrepareBackupRestore");
    }
}
