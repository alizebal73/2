using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record SessionReportQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Station,
    string? Zone,
    string? Operator,
    string? State,
    string? CustomerSearch,
    int Page = 1,
    int PageSize = 50,
    Guid? ScopedAppUserId = null);

public sealed record SessionReportRowDto(
    Guid Id,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string State,
    Guid StationId,
    string StationName,
    string Zone,
    string StationType,
    Guid CustomerId,
    string CustomerName,
    string? CustomerCode,
    string? CustomerUsername,
    Guid? AppUserId,
    string Operator,
    Guid? GameId,
    string? GameName,
    int Persons,
    double BillableMinutes,
    decimal TotalAmount);

public sealed record SessionStationSummaryDto(
    Guid StationId,
    string StationName,
    string Zone,
    int SessionCount,
    double BillableMinutes,
    decimal Revenue);

public sealed record SessionReportSummaryDto(
    int SessionCount,
    double BillableMinutes,
    decimal Revenue,
    double AverageMinutes,
    IReadOnlyList<SessionStationSummaryDto> Stations);

public sealed record SessionReportPageDto(
    int Page,
    int PageSize,
    int Total,
    SessionReportSummaryDto Summary,
    IReadOnlyList<SessionReportRowDto> Items);

public sealed class SessionReportService
{
    private readonly GameNetDbContext _database;

    public SessionReportService(GameNetDbContext database)
    {
        _database = database;
    }

    public async Task<SessionReportPageDto> QueryAsync(
        SessionReportQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);

        var sessions = await _database.Sessions
            .AsNoTracking()
            .Include(item => item.Station)
            .Include(item => item.Customer)
            .Include(item => item.AppUser)
            .Include(item => item.Game)
            .ToListAsync(cancellationToken);

        IEnumerable<Session> filtered = sessions;

        if (query.ScopedAppUserId is { } scopedUserId)
            filtered = filtered.Where(item => item.AppUserId == scopedUserId);

        if (query.From is { } from)
            filtered = filtered.Where(item => item.StartAt >= from);

        if (query.To is { } to)
            filtered = filtered.Where(item => item.StartAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Station))
        {
            var value = query.Station.Trim();
            filtered = filtered.Where(item =>
                item.Station.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Zone))
        {
            var value = query.Zone.Trim();
            filtered = filtered.Where(item =>
                string.Equals(item.Station.Zone, value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Operator))
        {
            var value = query.Operator.Trim();
            filtered = filtered.Where(item =>
                item.AppUser != null
                && (item.AppUser.FullName.Contains(value, StringComparison.OrdinalIgnoreCase)
                    || item.AppUser.UserName.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var value = query.State.Trim();
            filtered = filtered.Where(item =>
                string.Equals(item.State.ToString(), value, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerSearch))
        {
            var value = query.CustomerSearch.Trim();
            filtered = filtered.Where(item =>
                item.Customer.FullName.Contains(value, StringComparison.OrdinalIgnoreCase)
                || (item.Customer.Code?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Customer.Username?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = filtered
            .OrderByDescending(item => item.StartAt)
            .ThenByDescending(item => item.Id)
            .ToList();

        var sessionIds = ordered.Select(item => item.Id).ToList();
        var sessionRevenue = sessionIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await _database.InvoiceItems
                .AsNoTracking()
                .Where(item => item.SessionId.HasValue && sessionIds.Contains(item.SessionId.Value))
                .GroupBy(item => item.SessionId!.Value)
                .Select(group => new { SessionId = group.Key, Amount = group.Sum(item => item.Amount) })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.SessionId, item => item.Amount);

        var mapped = ordered
            .Select(item => ToRow(
                item,
                sessionRevenue.TryGetValue(item.Id, out var ledgerAmount)
                    ? ledgerAmount
                    : item.TotalAmount))
            .ToList();

        var total = mapped.Count;
        var summary = BuildSummary(mapped);

        var items = mapped
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SessionReportPageDto(page, pageSize, total, summary, items);
    }

    private static SessionReportRowDto ToRow(Session session, decimal reportedAmount)
    {
        var end = session.EndAt ?? DateTimeOffset.UtcNow;
        var billableMinutes = SessionTiming.GetBillableMinutes(session, end);

        return new SessionReportRowDto(
            session.Id,
            session.StartAt,
            session.EndAt,
            session.State.ToString(),
            session.StationId,
            session.Station.Name,
            session.Station.Zone,
            session.Station.Type,
            session.CustomerId,
            session.Customer.FullName,
            session.Customer.Code,
            session.Customer.Username,
            session.AppUserId,
            session.AppUser?.FullName ?? "Agent / سیستم",
            session.GameId,
            session.Game?.Name,
            session.Persons,
            billableMinutes,
            reportedAmount);
    }

    private static SessionReportSummaryDto BuildSummary(
        IReadOnlyList<SessionReportRowDto> rows)
    {
        var minutes = rows.Sum(item => item.BillableMinutes);
        var revenue = rows.Sum(item => item.TotalAmount);

        var stations = rows
            .GroupBy(item => new { item.StationId, item.StationName, item.Zone })
            .Select(group => new SessionStationSummaryDto(
                group.Key.StationId,
                group.Key.StationName,
                group.Key.Zone,
                group.Count(),
                group.Sum(item => item.BillableMinutes),
                group.Sum(item => item.TotalAmount)))
            .OrderByDescending(item => item.SessionCount)
            .ThenByDescending(item => item.Revenue)
            .ThenBy(item => item.StationName)
            .ToList();

        return new SessionReportSummaryDto(
            rows.Count,
            minutes,
            revenue,
            rows.Count == 0 ? 0d : minutes / rows.Count,
            stations);
    }
}
