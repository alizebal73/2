using GameNetManager.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record NotificationDto(
    Guid Id,
    Guid? AppUserId,
    string Category,
    string Title,
    string Detail,
    string Level,
    string? EntityName,
    string? EntityId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationEvent(
    string Category,
    string Title,
    string Detail,
    NotificationLevel Level = NotificationLevel.Info,
    string? EntityName = null,
    string? EntityId = null);

public sealed class NotificationQueueService
{
    private readonly GameNetDbContext _database;
    private readonly IHubContext<DashboardHub> _dashboardHub;

    public NotificationQueueService(
        GameNetDbContext database,
        IHubContext<DashboardHub> dashboardHub)
    {
        _database = database;
        _dashboardHub = dashboardHub;
    }

    public async Task<int> PublishToPermissionAsync(
        string permission,
        NotificationEvent notification,
        CancellationToken cancellationToken)
    {
        var users = await _database.AppUsers
            .AsNoTracking()
            .Include(item => item.Permissions)
            .ThenInclude(item => item.Permission)
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);

        var targets = users
            .Where(user =>
                IsGlobalUser(user)
                || user.Permissions.Any(item =>
                    string.Equals(item.Permission.Name, permission, StringComparison.OrdinalIgnoreCase)))
            .Select(user => user.Id)
            .Distinct()
            .ToList();

        return await PublishToUsersAsync(targets, notification, cancellationToken);
    }

    public async Task<int> PublishToUsersAsync(
        IEnumerable<Guid> appUserIds,
        NotificationEvent notification,
        CancellationToken cancellationToken)
    {
        var targets = appUserIds.Distinct().ToArray();
        if (targets.Length == 0)
            return 0;

        var rows = targets.Select(userId => new Notification
        {
            AppUserId = userId,
            Category = notification.Category,
            Title = notification.Title,
            Detail = notification.Detail,
            Level = notification.Level,
            EntityName = notification.EntityName,
            EntityId = notification.EntityId,
            IsRead = false
        }).ToList();

        _database.Notifications.AddRange(rows);
        await _database.SaveChangesAsync(cancellationToken);

        foreach (var row in rows)
        {
            await _dashboardHub.Clients.All.SendAsync(
                "NotificationAdded",
                new { notificationId = row.Id },
                cancellationToken);
        }

        return rows.Count;
    }

    public async Task<(IReadOnlyList<NotificationDto> Items, int UnreadCount)> ListAsync(
        Guid appUserId,
        int take,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take, 1, 100);

        var items = await _database.Notifications
            .AsNoTracking()
            .Where(item => item.AppUserId == appUserId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(limit)
            .Select(item => new NotificationDto(
                item.Id,
                item.AppUserId,
                item.Category,
                item.Title,
                item.Detail,
                item.Level.ToString(),
                item.EntityName,
                item.EntityId,
                item.IsRead,
                item.CreatedAt,
                item.ReadAt))
            .ToListAsync(cancellationToken);

        var unread = await _database.Notifications
            .CountAsync(item => item.AppUserId == appUserId && !item.IsRead, cancellationToken);

        return (items, unread);
    }

    public async Task<bool> MarkReadAsync(
        Guid appUserId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var affected = await _database.Notifications
            .Where(item => item.Id == notificationId
                && item.AppUserId == appUserId
                && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsRead, true)
                .SetProperty(item => item.ReadAt, DateTimeOffset.UtcNow),
                cancellationToken);

        return affected == 1;
    }

    public async Task<int> MarkAllReadAsync(
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        return await _database.Notifications
            .Where(item => item.AppUserId == appUserId && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsRead, true)
                .SetProperty(item => item.ReadAt, DateTimeOffset.UtcNow),
                cancellationToken);
    }

    private static bool IsGlobalUser(AppUser user)
        => string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase);
}
