using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record AuditLogQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Operator,
    string? Action,
    string? EntityName,
    string? Search,
    Guid? ScopedAppUserId = null,
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

        // SQLite cannot translate DateTimeOffset comparisons/order-by reliably.
        // Audit Explorer is intentionally kept provider-safe here; the same server
        // remains the source of truth, while filtering/order/paging happen in memory.
        var logs = await _database.AuditLogs
            .AsNoTracking()
            .Include(item => item.AppUser)
            .ToListAsync(cancellationToken);

        IEnumerable<AuditLog> filtered = logs;

        if (query.ScopedAppUserId is { } scopedUserId)
            filtered = filtered.Where(item => item.AppUserId == scopedUserId);

        if (query.From is { } from)
            filtered = filtered.Where(item => item.CreatedAt >= from);

        if (query.To is { } to)
            filtered = filtered.Where(item => item.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Operator))
        {
            var value = query.Operator.Trim();
            filtered = filtered.Where(item =>
                item.AppUser != null
                && (item.AppUser.FullName.Contains(value, StringComparison.OrdinalIgnoreCase)
                    || item.AppUser.UserName.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var value = query.Action.Trim();
            filtered = filtered.Where(item =>
                item.Action.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var value = query.EntityName.Trim();
            filtered = filtered.Where(item =>
                item.EntityName.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            filtered = filtered.Where(item =>
                item.Action.Contains(value, StringComparison.OrdinalIgnoreCase)
                || item.EntityName.Contains(value, StringComparison.OrdinalIgnoreCase)
                || (item.EntityId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Details?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.AppUser != null && (
                    item.AppUser.FullName.Contains(value, StringComparison.OrdinalIgnoreCase)
                    || item.AppUser.UserName.Contains(value, StringComparison.OrdinalIgnoreCase))));
        }

        var ordered = filtered
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id);

        var total = ordered.Count();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditLogRecordDto(
                item.Id,
                item.CreatedAt,
                item.AppUserId,
                item.AppUser == null ? "سیستم" : item.AppUser.FullName,
                item.Action,
                item.EntityName,
                item.EntityId,
                item.Details))
            .ToList();

        return new AuditLogPageDto(page, pageSize, total, items);
    }
}
