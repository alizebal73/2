using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class ReportingService(GameNetDbContext database)
{
    public async Task<ReportSummaryDto> GetSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (to < from)
            throw new ArgumentException("بازه گزارش نامعتبر است.");

        var invoices = await database.Invoices
            .AsNoTracking()
            .Where(item => item.Status == InvoiceStatus.Paid)
            .ToListAsync(cancellationToken);

        var expenses = await database.Expenses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sessions = await database.Sessions
            .AsNoTracking()
            .Where(item => item.StartAt <= to)
            .ToListAsync(cancellationToken);

        var paid = invoices.Where(item => item.IssuedAt >= from && item.IssuedAt <= to).ToList();
        var expense = expenses
            .Where(item => item.CreatedAt >= from && item.CreatedAt <= to)
            .Sum(item => item.Amount);
        var periodSessions = sessions
            .Where(item => item.StartAt >= from && item.StartAt <= to)
            .ToList();

        return new ReportSummaryDto(
            from,
            to,
            paid.Sum(item => item.TotalAmount),
            expense,
            paid.Sum(item => item.TotalAmount) - expense,
            periodSessions.Count,
            paid.Count,
            periodSessions.Select(item => item.CustomerId).Distinct().Count());
    }

    public async Task<IReadOnlyList<StationPerformanceDto>> GetStationPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var sessions = await database.Sessions
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.StartAt <= to)
            .ToListAsync(cancellationToken);

        sessions = sessions
            .Where(item => item.StartAt >= from && item.StartAt <= to)
            .ToList();

        var sessionIds = sessions.Select(item => item.Id).ToList();
        var invoiceRows = sessionIds.Count == 0
            ? new List<(Guid SessionId, decimal Amount)>()
            : (await database.Invoices
                .AsNoTracking()
                .Where(item => item.Status == InvoiceStatus.Paid
                    && item.SessionId.HasValue
                    && sessionIds.Contains(item.SessionId.Value))
                .Select(item => new { SessionId = item.SessionId!.Value, item.TotalAmount, item.IssuedAt })
                .ToListAsync(cancellationToken))
                .Where(item => item.IssuedAt >= from && item.IssuedAt <= to)
                .Select(item => (item.SessionId, item.TotalAmount))
                .ToList();

        var revenueBySession = invoiceRows
            .GroupBy(item => item.SessionId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        return sessions
            .GroupBy(item => new { item.StationId, Name = item.Station.Name, item.Station.Zone })
            .OrderByDescending(group => group.Sum(item => revenueBySession.TryGetValue(item.Id, out var amount) ? amount : 0m))
            .ThenBy(group => group.Key.Name)
            .Select(group =>
            {
                var minutes = group.Sum(item => SessionTiming.GetBillableMinutes(item, item.EndAt ?? to));
                var revenue = group.Sum(item => revenueBySession.TryGetValue(item.Id, out var amount) ? amount : 0m);
                return new StationPerformanceDto(
                    group.Key.StationId,
                    group.Key.Name,
                    group.Key.Zone,
                    group.Count(),
                    minutes,
                    revenue,
                    group.Count() == 0 ? 0m : Math.Round(revenue / group.Count(), 0, MidpointRounding.AwayFromZero),
                    group.Count());
            })
            .ToList();
    }


    public async Task<IReadOnlyList<CustomerPerformanceDto>> GetCustomerPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var sessions = await database.Sessions
            .AsNoTracking()
            .Include(item => item.Customer)
            .Where(item => item.StartAt >= from && item.StartAt <= to)
            .ToListAsync(cancellationToken);

        var sessionIds = sessions.Select(item => item.Id).ToList();
        var paid = sessionIds.Count == 0
            ? new List<(Guid SessionId, decimal Amount, DateTimeOffset IssuedAt)>()
            : (await database.Invoices
                .AsNoTracking()
                .Where(item => item.Status == InvoiceStatus.Paid
                    && item.SessionId.HasValue
                    && sessionIds.Contains(item.SessionId.Value))
                .Select(item => new { SessionId = item.SessionId!.Value, item.TotalAmount, item.IssuedAt })
                .ToListAsync(cancellationToken))
                .Where(item => item.IssuedAt >= from && item.IssuedAt <= to)
                .Select(item => (item.SessionId, item.TotalAmount, item.IssuedAt))
                .ToList();

        var revenueByCustomer = sessions
            .GroupJoin(
                paid.GroupBy(item => sessions.FirstOrDefault(s => s.Id == item.SessionId)?.CustomerId ?? Guid.Empty),
                session => session.CustomerId,
                group => group.Key,
                (session, group) => new { session, revenue = group.Sum(item => item.Sum(x => x.Amount)) })
            .ToList();

        var customerIds = sessions.Select(item => item.CustomerId).Distinct().ToList();
        var customers = await database.Customers
            .AsNoTracking()
            .Where(item => customerIds.Contains(item.Id))
            .Include(item => item.VipPackage)
            .ToListAsync(cancellationToken);
        var customerById = customers.ToDictionary(item => item.Id);

        var draftDebtRows = await database.Invoices
            .AsNoTracking()
            .Where(item => item.Status == InvoiceStatus.Draft && customerIds.Contains(item.CustomerId))
            .Select(item => new { item.CustomerId, item.TotalAmount })
            .ToListAsync(cancellationToken);

        return sessions
            .GroupBy(item => item.CustomerId)
            .Select(group =>
            {
                customerById.TryGetValue(group.Key, out var customer);
                var revenue = paid
                    .Where(item => group.Any(session => session.Id == item.SessionId))
                    .Sum(item => item.Amount);
                var minutes = group.Sum(item => SessionTiming.GetBillableMinutes(item, item.EndAt ?? to));
                var vipMinutes = group.Sum(item =>
                {
                    if (customer?.VipPackage is null) return 0;
                    return Math.Max(0, SessionTiming.GetBillableMinutes(item, item.EndAt ?? to));
                });

                return new CustomerPerformanceDto(
                    group.Key,
                    customer?.Code ?? customer?.Username ?? group.Key.ToString(),
                    customer?.FullName ?? "مشتری",
                    group.Count(),
                    minutes,
                    revenue,
                    vipMinutes,
                    customer?.Balance ?? 0m,
                    draftDebtRows.Where(item => item.CustomerId == group.Key).Sum(item => item.TotalAmount));
            })
            .OrderByDescending(item => item.Revenue)
            .ThenBy(item => item.CustomerName)
            .ToList();
    }

    public async Task<IReadOnlyList<OperatorPerformanceDto>> GetOperatorPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var shifts = await database.Shifts
            .AsNoTracking()
            .Include(item => item.AppUser)
            .ToListAsync(cancellationToken);
        shifts = shifts
            .Where(item => item.OpenAt <= to && (item.CloseAt ?? to) >= from)
            .ToList();

        var invoices = await database.Invoices
            .AsNoTracking()
            .Where(item => item.Status == InvoiceStatus.Paid && item.AppUserId.HasValue)
            .ToListAsync(cancellationToken);
        invoices = invoices.Where(item => item.IssuedAt >= from && item.IssuedAt <= to).ToList();

        var expenses = await database.Expenses
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        expenses = expenses.Where(item => item.CreatedAt >= from && item.CreatedAt <= to).ToList();

        var result = shifts
            .GroupBy(item => new { item.AppUserId, Name = item.AppUser.FullName })
            .Select(group =>
            {
                var userInvoices = invoices.Where(item => item.AppUserId == group.Key.AppUserId).ToList();
                var userExpenses = expenses
                    .Where(item => group.Any(shift => shift.Id == item.ShiftId))
                    .Sum(item => item.Amount);
                var hours = group.Sum(item =>
                    Math.Max(0, (item.CloseAt ?? to - item.OpenAt).TotalHours));

                return new OperatorPerformanceDto(
                    group.Key.AppUserId,
                    group.Key.Name,
                    group.Count(),
                    hours,
                    userInvoices.Count,
                    userInvoices.Sum(item => item.TotalAmount),
                    userExpenses,
                    userInvoices.Sum(item => item.TotalAmount) - userExpenses);
            })
            .OrderByDescending(item => item.Revenue)
            .ThenBy(item => item.OperatorName)
            .ToList();

        return result;
    }

    public async Task<IReadOnlyList<HeatmapCellDto>> GetHeatmapAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var sessions = await database.Sessions
            .AsNoTracking()
            .Where(item => item.StartAt <= to)
            .ToListAsync(cancellationToken);

        sessions = sessions
            .Where(item => item.StartAt >= from && item.StartAt <= to)
            .ToList();

        var ids = sessions.Select(item => item.Id).ToList();
        var invoiceRows = ids.Count == 0
            ? new List<(Guid SessionId, decimal Amount)>()
            : (await database.Invoices
                .AsNoTracking()
                .Where(item => item.Status == InvoiceStatus.Paid
                    && item.SessionId.HasValue
                    && ids.Contains(item.SessionId.Value))
                .Select(item => new { SessionId = item.SessionId!.Value, item.TotalAmount, item.IssuedAt })
                .ToListAsync(cancellationToken))
                .Where(item => item.IssuedAt >= from && item.IssuedAt <= to)
                .Select(item => (item.SessionId, item.TotalAmount))
                .ToList();

        var revenueBySession = invoiceRows
            .GroupBy(item => item.SessionId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        return sessions
            .GroupBy(item => new { Day = (int)item.StartAt.DayOfWeek, Hour = item.StartAt.Hour })
            .OrderBy(item => item.Key.Day)
            .ThenBy(item => item.Key.Hour)
            .Select(group => new HeatmapCellDto(
                group.Key.Day,
                group.Key.Hour,
                group.Count(),
                group.Sum(item => SessionTiming.GetBillableMinutes(item, item.EndAt ?? to)),
                group.Sum(item => revenueBySession.TryGetValue(item.Id, out var amount) ? amount : 0m)))
            .ToList();
    }

    public async Task<IReadOnlyList<AuditExplorerDto>> GetAuditAsync(
        AuditExplorerFilterDto filter,
        CancellationToken cancellationToken)
    {
        var rows = await database.AuditLogs
            .AsNoTracking()
            .Include(item => item.AppUser)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(filter.Limit, 1, 1000))
            .ToListAsync(cancellationToken);

        var from = filter.From;
        var to = filter.To;
        var action = filter.Action?.Trim();
        var entity = filter.EntityName?.Trim();
        var search = filter.Search?.Trim();

        return rows
            .Where(item => (!from.HasValue || item.CreatedAt >= from.Value)
                && (!to.HasValue || item.CreatedAt <= to.Value)
                && (string.IsNullOrWhiteSpace(action) || string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(entity) || string.Equals(item.EntityName, entity, StringComparison.OrdinalIgnoreCase))
                && (!filter.AppUserId.HasValue || item.AppUserId == filter.AppUserId.Value)
                && (string.IsNullOrWhiteSpace(search)
                    || (item.Details?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || item.Action.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.EntityName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.EntityId?.Contains(search, StringComparison.OrdinalIgnoreCase) == true))
            .Select(item => new AuditExplorerDto(
                item.Id,
                item.CreatedAt,
                item.AppUserId,
                item.AppUser?.FullName ?? "سیستم",
                item.Action,
                item.EntityName,
                item.EntityId,
                item.Details))
            .ToList();
    }
}
