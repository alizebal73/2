using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record AuditLogQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Operator,
    string? Action,
    string? EntityName,
    string? Search,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditLogRecordDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid? AppUserId,
    string Operator,
    string Action,
    string EntityName,
    string? EntityId,
    string? Details);

public sealed record AuditLogPageDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AuditLogRecordDto> Items);

public sealed class AuditLogService
{
    private readonly GameNetDbContext _database;

    public AuditLogService(GameNetDbContext database)
    {
        _database = database;
    }

    public async Task<AuditLogPageDto> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);

        IQueryable<AuditLog> logs = _database.AuditLogs
            .AsNoTracking()
            .Include(item => item.AppUser);

        if (query.From is { } from)
            logs = logs.Where(item => item.CreatedAt >= from);

        if (query.To is { } to)
            logs = logs.Where(item => item.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Operator))
        {
            var value = query.Operator.Trim();
            logs = logs.Where(item =>
                item.AppUser != null
                && (item.AppUser.FullName.Contains(value)
                    || item.AppUser.UserName.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var value = query.Action.Trim();
            logs = logs.Where(item => item.Action.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var value = query.EntityName.Trim();
            logs = logs.Where(item => item.EntityName.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            logs = logs.Where(item =>
                item.Action.Contains(value)
                || item.EntityName.Contains(value)
                || (item.EntityId != null && item.EntityId.Contains(value))
                || (item.Details != null && item.Details.Contains(value))
                || (item.AppUser != null && (
                    item.AppUser.FullName.Contains(value)
                    || item.AppUser.UserName.Contains(value))));
        }

        var total = await logs.CountAsync(cancellationToken);

        var items = await logs
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditLogRecordDto(
                item.Id,
                item.CreatedAt,
                item.AppUserId,
                item.AppUser == null
                    ? "سیستم"
                    : item.AppUser.FullName,
                item.Action,
                item.EntityName,
                item.EntityId,
                item.Details))
            .ToListAsync(cancellationToken);

        return new AuditLogPageDto(page, pageSize, total, items);
    }
}
