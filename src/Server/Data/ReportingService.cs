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

        var paidInvoices = await LoadInvoicesInRangeAsync(from, to, cancellationToken);
        var expenses = await LoadExpensesInRangeAsync(from, to, cancellationToken);
        var periodSessions = await LoadSessionsInRangeAsync(from, to, cancellationToken);

        var expense = expenses.Sum(item => item.Amount);

        return new ReportSummaryDto(
            from,
            to,
            paidInvoices.Sum(item => item.TotalAmount),
            expense,
            paidInvoices.Sum(item => item.TotalAmount) - expense,
            periodSessions.Count,
            paidInvoices.Count,
            periodSessions.Select(item => item.CustomerId).Distinct().Count());
    }

    public async Task<IReadOnlyList<StationPerformanceDto>> GetStationPerformanceAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var sessions = await LoadSessionsInRangeAsync(
            from,
            to,
            cancellationToken,
            includeStation: true);

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
                .Select(item => (SessionId: item.SessionId, Amount: item.TotalAmount))
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
                .Select(item => (SessionId: item.SessionId, Amount: item.TotalAmount, IssuedAt: item.IssuedAt))
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
                    if (customer?.VipPackage is null)
                        return 0;

                    var activation = customer.VipActivatedAt;
                    var expiry = customer.VipExpiresAt;
                    if (!activation.HasValue || !expiry.HasValue
                        || item.StartAt < activation.Value
                        || item.StartAt >= expiry.Value)
                        return 0;

                    return Math.Max(0, SessionTiming.GetBillableMinutes(item, item.EndAt ?? to));
                });

                return new CustomerPerformanceDto(
                    group.Key,
                    customer?.Code ?? customer?.Username ?? group.Key.ToString(),
                    customer?.FullName ?? "مشتری",
                    group.Count(),
                    minutes,
                    revenue,
                    (int)Math.Round(vipMinutes),
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
        var shifts = await LoadShiftsOverlappingRangeAsync(from, to, cancellationToken);
        var invoices = (await LoadInvoicesInRangeAsync(from, to, cancellationToken))
            .Where(item => item.AppUserId.HasValue)
            .ToList();
        var expenses = await LoadExpensesInRangeAsync(from, to, cancellationToken);

        var result = shifts
            .GroupBy(item => new { item.AppUserId, Name = item.AppUser.FullName })
            .Select(group =>
            {
                var userInvoices = invoices.Where(item => item.AppUserId == group.Key.AppUserId).ToList();
                var userExpenses = expenses
                    .Where(item => group.Any(shift => shift.Id == item.ShiftId))
                    .Sum(item => item.Amount);
                var hours = group.Sum(item =>
                    Math.Max(0, ((item.CloseAt ?? to) - item.OpenAt).TotalHours));

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
        var sessions = await LoadSessionsInRangeAsync(from, to, cancellationToken);

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
                .Select(item => (SessionId: item.SessionId, Amount: item.TotalAmount))
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
        var from = filter.From;
        var to = filter.To;
        var action = filter.Action?.Trim();
        var entity = filter.EntityName?.Trim();
        var search = filter.Search?.Trim();

        var query = database.AuditLogs
            .AsNoTracking()
            .Include(item => item.AppUser)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(item => item.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(item => item.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(item => item.Action == action);
        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(item => item.EntityName == entity);
        if (filter.AppUserId.HasValue)
            query = query.Where(item => item.AppUserId == filter.AppUserId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Action, pattern)
                || EF.Functions.Like(item.EntityName, pattern)
                || (item.EntityId != null && EF.Functions.Like(item.EntityId, pattern))
                || (item.Details != null && EF.Functions.Like(item.Details, pattern)));
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(filter.Limit, 1, 1000))
            .Select(item => new AuditExplorerDto(
                item.Id,
                item.CreatedAt,
                item.AppUserId,
                item.AppUser?.FullName ?? "سیستم",
                item.Action,
                item.EntityName,
                item.EntityId,
                item.Details))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Invoice>> LoadInvoicesInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var fromText = from.UtcDateTime.ToString("O");
        var toText = to.UtcDateTime.ToString("O");
        return await database.Invoices
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Invoices"
                WHERE "Status" = 'Paid'
                  AND datetime("IssuedAt") >= datetime({fromText})
                  AND datetime("IssuedAt") <= datetime({toText})
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Expense>> LoadExpensesInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var fromText = from.UtcDateTime.ToString("O");
        var toText = to.UtcDateTime.ToString("O");
        return await database.Expenses
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Expenses"
                WHERE datetime("CreatedAt") >= datetime({fromText})
                  AND datetime("CreatedAt") <= datetime({toText})
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Session>> LoadSessionsInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken,
        bool includeStation = false)
    {
        var fromText = from.UtcDateTime.ToString("O");
        var toText = to.UtcDateTime.ToString("O");
        var query = database.Sessions
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Sessions"
                WHERE datetime("StartAt") >= datetime({fromText})
                  AND datetime("StartAt") <= datetime({toText})
                """)
            .AsNoTracking();

        if (includeStation)
            query = query.Include(item => item.Station);

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<Shift>> LoadShiftsOverlappingRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var fromText = from.UtcDateTime.ToString("O");
        var toText = to.UtcDateTime.ToString("O");
        return await database.Shifts
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Shifts"
                WHERE datetime("OpenAt") <= datetime({toText})
                  AND ("CloseAt" IS NULL OR datetime("CloseAt") >= datetime({fromText}))
                """)
            .Include(item => item.AppUser)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

