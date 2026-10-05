using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record CustomerVipReportQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Search,
    string? Vip,
    string? Debt,
    string? Package,
    int Page = 1,
    int PageSize = 50);

public sealed record CustomerVipReportRowDto(
    Guid CustomerId,
    string Code,
    string Username,
    string Name,
    string VipTier,
    string? PackageName,
    DateTimeOffset? VipActivatedAt,
    DateTimeOffset? VipExpiresAt,
    int VipDailyMinutes,
    int VipTotalMinutes,
    int VipDiscountPercent,
    int UsedTodayMinutes,
    int UsedTotalMinutes,
    int RemainingTodayMinutes,
    int RemainingTotalMinutes,
    decimal WalletBalance,
    decimal Debt,
    int SessionCount,
    decimal SessionRevenue,
    DateTimeOffset? LastSessionAt,
    string Status,
    string? Notes);

public sealed record CustomerVipReportSummaryDto(
    int CustomerCount,
    int VipCount,
    int ActiveVipCount,
    int DebtorCount,
    decimal WalletTotal,
    decimal DebtTotal,
    int SessionCount,
    decimal SessionRevenue);

public sealed record CustomerVipReportPageDto(
    int Page,
    int PageSize,
    int Total,
    CustomerVipReportSummaryDto Summary,
    IReadOnlyList<CustomerVipReportRowDto> Items);

public sealed class CustomerVipReportService
{
    private readonly GameNetDbContext _database;

    public CustomerVipReportService(GameNetDbContext database)
    {
        _database = database;
    }

    public async Task<CustomerVipReportPageDto> QueryAsync(
        CustomerVipReportQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 200);
        var now = DateTimeOffset.UtcNow;

        var customers = await _database.Customers
            .AsNoTracking()
            .Include(item => item.VipPackage)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.FullName)
            .ToListAsync(cancellationToken);

        var customerIds = customers.Select(item => item.Id).ToList();

        var allSessions = customerIds.Count == 0
            ? []
            : await _database.Sessions
                .AsNoTracking()
                .Where(item => customerIds.Contains(item.CustomerId))
                .Select(item => new
                {
                    item.Id,
                    item.CustomerId,
                    item.StartAt,
                    item.EndAt,
                    item.TotalAmount
                })
                .ToListAsync(cancellationToken);

        var debts = customerIds.Count == 0
            ? []
            : await _database.Invoices
                .AsNoTracking()
                .Where(item => customerIds.Contains(item.CustomerId) && item.Status == InvoiceStatus.Draft)
                .GroupBy(item => item.CustomerId)
                .Select(group => new { CustomerId = group.Key, Amount = group.Sum(item => item.TotalAmount) })
                .ToListAsync(cancellationToken);

        var debtByCustomer = debts.ToDictionary(item => item.CustomerId, item => item.Amount);

        var filtered = customers.Where(customer =>
        {
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var value = query.Search.Trim();
                if (!customer.Code.Contains(value, StringComparison.OrdinalIgnoreCase)
                    && !customer.FullName.Contains(value, StringComparison.OrdinalIgnoreCase)
                    && !(customer.Username?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
                    && !(customer.Phone?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false))
                    return false;
            }

            var vipTier = NormalizeVip(customer.VipTier);
            var vipActive = IsVipActive(customer, now);

            if (!string.IsNullOrWhiteSpace(query.Vip))
            {
                var filter = query.Vip.Trim().ToLowerInvariant();
                if (filter == "vip" && vipTier == "none") return false;
                if (filter == "active" && !vipActive) return false;
                if (filter == "expired" && (vipTier == "none" || vipActive)) return false;
                if (filter == "none" && vipTier != "none") return false;
            }

            var debt = debtByCustomer.GetValueOrDefault(customer.Id);
            if (!string.IsNullOrWhiteSpace(query.Debt))
            {
                var filter = query.Debt.Trim().ToLowerInvariant();
                if (filter == "debtor" && debt <= 0m) return false;
                if (filter == "clear" && debt > 0m) return false;
            }

            if (!string.IsNullOrWhiteSpace(query.Package)
                && !string.Equals(customer.VipPackage?.Name, query.Package.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();

        var sessionByCustomer = allSessions
            .GroupBy(item => item.CustomerId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = filtered
            .Select(customer => BuildRow(
                customer,
                sessionByCustomer.GetValueOrDefault(customer.Id) ?? [],
                debtByCustomer.GetValueOrDefault(customer.Id),
                query.From,
                query.To,
                now))
            .OrderByDescending(item => item.SessionCount)
            .ThenByDescending(item => item.LastSessionAt)
            .ThenBy(item => item.Name)
            .ToList();

        var summary = new CustomerVipReportSummaryDto(
            rows.Count,
            rows.Count(item => item.VipTier != "none"),
            rows.Count(item => item.Status == "vip-active"),
            rows.Count(item => item.Debt > 0m),
            rows.Sum(item => item.WalletBalance),
            rows.Sum(item => item.Debt),
            rows.Sum(item => item.SessionCount),
            rows.Sum(item => item.SessionRevenue));

        var total = rows.Count;
        var items = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CustomerVipReportPageDto(page, pageSize, total, summary, items);
    }

    private static CustomerVipReportRowDto BuildRow(
        Customer customer,
        IReadOnlyList<dynamic> allSessions,
        decimal debt,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now)
    {
        var vipTier = NormalizeVip(customer.VipTier);
        var active = IsVipActive(customer, now);
        var reportSessions = allSessions
            .Where(item => (!from.HasValue || item.StartAt >= from.Value)
                && (!to.HasValue || item.StartAt <= to.Value))
            .ToList();

        var usageSessions = active
            ? allSessions
                .Where(item => item.StartAt < (customer.VipExpiresAt ?? now)
                    && (item.EndAt ?? now) > customer.VipActivatedAt!.Value)
                .ToList()
            : [];

        var usedTotal = 0;
        var usedToday = 0;
        var todayStart = now.Date;

        foreach (var session in usageSessions)
        {
            var start = session.StartAt < customer.VipActivatedAt!.Value
                ? customer.VipActivatedAt.Value
                : session.StartAt;
            var end = session.EndAt ?? now;
            if (customer.VipExpiresAt.HasValue && end > customer.VipExpiresAt.Value)
                end = customer.VipExpiresAt.Value;
            if (end <= start)
                continue;

            usedTotal += (int)Math.Ceiling((end - start).TotalMinutes);

            if (end > todayStart)
            {
                var todaySegmentStart = start < todayStart ? todayStart : start;
                if (todaySegmentStart < end)
                    usedToday += (int)Math.Ceiling((end - todaySegmentStart).TotalMinutes);
            }
        }

        var dailyLimit = customer.VipPackage?.DailyMinutes ?? 0;
        var totalLimit = customer.VipPackage?.TotalMinutes ?? 0;

        return new CustomerVipReportRowDto(
            customer.Id,
            customer.Code,
            customer.Username ?? "",
            customer.FullName,
            vipTier,
            customer.VipPackage?.Name,
            customer.VipActivatedAt,
            customer.VipExpiresAt,
            dailyLimit,
            totalLimit,
            customer.VipPackage?.DiscountPercent ?? 0,
            usedToday,
            usedTotal,
            active ? Math.Max(0, dailyLimit - usedToday) : 0,
            active ? Math.Max(0, totalLimit - usedTotal) : 0,
            customer.Balance,
            debt,
            reportSessions.Count,
            reportSessions.Sum(item => item.TotalAmount),
            reportSessions
                .OrderByDescending(item => item.StartAt)
                .Select(item => (DateTimeOffset?)item.StartAt)
                .FirstOrDefault(),
            active ? "vip-active" : vipTier != "none" ? "vip-expired" : debt > 0m ? "debtor" : "active",
            customer.Notes);
    }

    private static bool IsVipActive(Customer customer, DateTimeOffset now)
        => customer.VipPackageId.HasValue
            && customer.VipPackage is not null
            && customer.VipActivatedAt.HasValue
            && (!customer.VipExpiresAt.HasValue || customer.VipExpiresAt.Value > now);

    private static string NormalizeVip(string? value)
        => value?.Trim().ToLowerInvariant() is "bronze" or "silver" or "gold" or "custom"
            ? value!.Trim().ToLowerInvariant()
            : "none";
}
