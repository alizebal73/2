using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record UsersShiftReportQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? UserSearch,
    string? ShiftState,
    int Page = 1,
    int PageSize = 50);

public sealed record UsersShiftReportRowDto(
    Guid UserId,
    string FullName,
    string UserName,
    string Role,
    bool IsActive,
    string PayType,
    decimal EmployeePayable,
    decimal OwnerReceivable,
    decimal PaidThisPeriod,
    decimal BonusThisPeriod,
    decimal DeductionThisPeriod,
    int ShiftCount,
    int ClosedShiftCount,
    decimal ShiftRevenue,
    decimal ShiftCashSales,
    decimal ShiftExpenses,
    decimal ShiftDifference,
    int SessionCount,
    decimal SessionRevenue,
    DateTimeOffset? LastLoginAt);

public sealed record UsersShiftReportSummaryDto(
    int UserCount,
    int ActiveUserCount,
    int ShiftCount,
    int ClosedShiftCount,
    decimal ShiftRevenue,
    decimal ShiftCashSales,
    decimal ShiftExpenses,
    decimal ShiftDifference,
    int SessionCount,
    decimal SessionRevenue,
    decimal PayrollPaid,
    decimal PayrollEmployeePayable);

public sealed record UsersShiftReportPageDto(
    int Page,
    int PageSize,
    int Total,
    UsersShiftReportSummaryDto Summary,
    IReadOnlyList<UsersShiftReportRowDto> Items);

public sealed class UsersShiftReportService
{
    private readonly GameNetDbContext _database;

    public UsersShiftReportService(GameNetDbContext database)
    {
        _database = database;
    }

    public async Task<UsersShiftReportPageDto> QueryAsync(
        UsersShiftReportQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var now = DateTimeOffset.UtcNow;

        var users = await _database.AppUsers
            .AsNoTracking()
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);

        var profiles = await _database.EmployeeProfiles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var shifts = await _database.Shifts
            .AsNoTracking()
            .Where(item => !query.From.HasValue || item.OpenAt >= query.From.Value)
            .Where(item => !query.To.HasValue || item.OpenAt <= query.To.Value)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.ShiftState))
        {
            shifts = query.ShiftState.Trim().Equals("open", StringComparison.OrdinalIgnoreCase)
                ? shifts.Where(item => item.CloseAt is null).ToList()
                : shifts.Where(item => item.CloseAt is not null).ToList();
        }

        var shiftIds = shifts.Select(item => item.Id).ToList();
        var expenses = shiftIds.Count == 0
            ? []
            : await _database.Expenses
                .AsNoTracking()
                .Where(item => shiftIds.Contains(item.ShiftId))
                .Select(item => new { item.ShiftId, item.Amount })
                .ToListAsync(cancellationToken);

        var payments = await _database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.Invoice.Status == InvoiceStatus.Paid
                && item.Invoice.PaidAt != null
                && (!query.From.HasValue || item.Invoice.PaidAt >= query.From.Value)
                && (!query.To.HasValue || item.Invoice.PaidAt <= query.To.Value))
            .Select(item => new
            {
                item.Method,
                item.Amount,
                PaidAt = item.Invoice.PaidAt!.Value
            })
            .ToListAsync(cancellationToken);

        var sessions = await _database.Sessions
            .AsNoTracking()
            .Where(item => item.AppUserId.HasValue)
            .Where(item => !query.From.HasValue || item.StartAt >= query.From.Value)
            .Where(item => !query.To.HasValue || item.StartAt <= query.To.Value)
            .Select(item => new
            {
                AppUserId = item.AppUserId!.Value,
                item.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var userIds = users.Select(item => item.Id).ToList();
        var payrollRows = userIds.Count == 0
            ? []
            : await _database.PayrollLedgerEntries
                .AsNoTracking()
                .Where(item => userIds.Contains(item.EmployeeProfile.AppUserId)
                    && item.Status == ApprovalStatus.Approved
                    && (!query.From.HasValue || item.CreatedAt >= query.From.Value)
                    && (!query.To.HasValue || item.CreatedAt <= query.To.Value))
                .Include(item => item.EmployeeProfile)
                .ToListAsync(cancellationToken);

        var profileByUser = profiles.ToDictionary(item => item.AppUserId);
        var shiftsByUser = shifts.GroupBy(item => item.AppUserId).ToDictionary(group => group.Key, group => group.ToList());
        var sessionsByUser = sessions.GroupBy(item => item.AppUserId).ToDictionary(group => group.Key, group => group.ToList());
        var payrollByUser = payrollRows.GroupBy(item => item.EmployeeProfile.AppUserId).ToDictionary(group => group.Key, group => group.ToList());
        var expenseByShift = expenses.GroupBy(item => item.ShiftId).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var filteredUsers = users
            .Where(user => string.IsNullOrWhiteSpace(query.UserSearch)
                || user.FullName.Contains(query.UserSearch.Trim(), StringComparison.OrdinalIgnoreCase)
                || user.UserName.Contains(query.UserSearch.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rows = filteredUsers.Select(user =>
        {
            profileByUser.TryGetValue(user.Id, out var profile);
            var userShifts = shiftsByUser.GetValueOrDefault(user.Id) ?? [];
            var userSessions = sessionsByUser.GetValueOrDefault(user.Id) ?? [];
            var userPayroll = payrollByUser.GetValueOrDefault(user.Id) ?? [];

            decimal totalShiftRevenue = 0m;
            decimal totalShiftCashSales = 0m;
            decimal totalShiftExpenses = 0m;
            decimal totalShiftDifference = 0m;

            foreach (var shift in userShifts)
            {
                var end = shift.CloseAt ?? now;
                var shiftPayments = payments
                    .Where(item => item.PaidAt >= shift.OpenAt && item.PaidAt <= end)
                    .ToList();
                var revenue = shiftPayments.Sum(item => item.Amount);
                var cashSales = shiftPayments
                    .Where(item => string.Equals(item.Method, "cash", StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.Amount);
                var shiftExpense = expenseByShift.GetValueOrDefault(shift.Id);

                totalShiftRevenue += revenue;
                totalShiftCashSales += cashSales;
                totalShiftExpenses += shiftExpense;

                if (shift.CloseAt.HasValue && shift.CashClosing.HasValue)
                {
                    totalShiftDifference += shift.CashClosing.Value
                        - (shift.CashOpening + cashSales - shiftExpense);
                }
            }

            return new UsersShiftReportRowDto(
                user.Id,
                user.FullName,
                user.UserName,
                user.Role,
                profile?.IsActive ?? user.IsActive,
                profile?.PayType ?? "hourly",
                userPayroll.Sum(item => item.EmployeePayableDelta),
                userPayroll.Sum(item => item.OwnerReceivableDelta),
                userPayroll.Where(item => item.Kind == "SalaryPayment").Sum(item => item.Amount),
                userPayroll.Where(item => item.Kind == "Bonus").Sum(item => item.Amount),
                userPayroll.Where(item => item.Kind == "Deduction").Sum(item => item.Amount),
                userShifts.Count,
                userShifts.Count(item => item.CloseAt.HasValue),
                totalShiftRevenue,
                totalShiftCashSales,
                totalShiftExpenses,
                totalShiftDifference,
                userSessions.Count,
                userSessions.Sum(item => item.TotalAmount),
                user.LastLoginAt);
        })
        .OrderByDescending(item => item.ShiftCount)
        .ThenByDescending(item => item.SessionRevenue)
        .ThenBy(item => item.FullName)
        .ToList();

        var summary = new UsersShiftReportSummaryDto(
            rows.Count,
            rows.Count(item => item.IsActive),
            rows.Sum(item => item.ShiftCount),
            rows.Sum(item => item.ClosedShiftCount),
            rows.Sum(item => item.ShiftRevenue),
            rows.Sum(item => item.ShiftCashSales),
            rows.Sum(item => item.ShiftExpenses),
            rows.Sum(item => item.ShiftDifference),
            rows.Sum(item => item.SessionCount),
            rows.Sum(item => item.SessionRevenue),
            rows.Sum(item => item.PaidThisPeriod),
            rows.Sum(item => item.EmployeePayable));

        var items = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new UsersShiftReportPageDto(
            page,
            pageSize,
            rows.Count,
            summary,
            items);
    }
}
