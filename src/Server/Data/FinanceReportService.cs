using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record FinanceSummaryResult(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal Revenue,
    decimal Expense,
    decimal OperatingProfit);

public sealed record FinanceTransactionResult(
    Guid Id,
    DateTimeOffset ClosedAt,
    string Description,
    decimal Amount,
    decimal FinancialImpact,
    string Method,
    string Status,
    string Kind);

public sealed class FinanceReportService(GameNetDbContext database)
{
    private static bool IsExternalPayment(string method)
        => method.Equals("cash", StringComparison.OrdinalIgnoreCase)
            || method.Equals("card", StringComparison.OrdinalIgnoreCase);

    public async Task<FinanceSummaryResult> GetSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var paymentRevenue = await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start
                && item.CreatedAt <= end
                && (item.Method == "cash" || item.Method == "card"))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var walletTopUps = await database.WalletTransactions
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start
                && item.CreatedAt <= end
                && item.Type == WalletTransactionType.Credit
                && item.ReferenceInvoiceId == null)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var reversalInvoiceIds = await database.InvoiceReversals
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .Select(item => item.InvoiceId)
            .ToListAsync(cancellationToken);

        var reversedExternalPayments = reversalInvoiceIds.Count == 0
            ? 0m
            : await database.InvoicePayments
                .AsNoTracking()
                .Where(item => reversalInvoiceIds.Contains(item.InvoiceId)
                    && (item.Method == "cash" || item.Method == "card"))
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var expense = await database.Expenses
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var revenue = paymentRevenue + walletTopUps - reversedExternalPayments;

        return new FinanceSummaryResult(
            start,
            end,
            revenue,
            expense,
            revenue - expense);
    }

    public async Task<IReadOnlyList<FinanceTransactionResult>> GetTransactionsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var paymentRows = await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .Select(item => new
            {
                item.Id,
                item.CreatedAt,
                Description = item.Invoice.Items
                    .OrderBy(child => child.Id)
                    .Select(child => child.Description)
                    .FirstOrDefault() ?? "تراکنش مالی",
                item.Amount,
                item.Method,
                Status = item.Invoice.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        var payments = paymentRows.Select(item => new FinanceTransactionResult(
            item.Id,
            item.CreatedAt,
            item.Description,
            item.Amount,
            IsExternalPayment(item.Method) ? item.Amount : 0m,
            item.Method,
            item.Status,
            IsExternalPayment(item.Method)
                ? "payment"
                : item.Method.Equals("wallet", StringComparison.OrdinalIgnoreCase)
                    ? "wallet-settlement"
                    : "gift-settlement"));

        var walletTopUpRows = await database.WalletTransactions
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start
                && item.CreatedAt <= end
                && item.Type == WalletTransactionType.Credit
                && item.ReferenceInvoiceId == null)
            .Select(item => new
            {
                item.Id,
                item.CreatedAt,
                item.Amount,
                item.Description
            })
            .ToListAsync(cancellationToken);

        var walletTopUps = walletTopUpRows.Select(item => new FinanceTransactionResult(
            item.Id,
            item.CreatedAt,
            "شارژ کیف پول: " + item.Description,
            item.Amount,
            item.Amount,
            "wallet",
            "Wallet",
            "wallet-topup"));

        var reversalRows = await database.InvoiceReversals
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .Select(item => new
            {
                item.Id,
                item.CreatedAt,
                item.InvoiceId,
                Description = item.Invoice.Items
                    .OrderBy(child => child.Id)
                    .Select(child => child.Description)
                    .FirstOrDefault() ?? "برگشت فاکتور"
            })
            .ToListAsync(cancellationToken);

        var reversalInvoiceIds = reversalRows.Select(item => item.InvoiceId).Distinct().ToList();
        var externalRefundByInvoice = reversalInvoiceIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await database.InvoicePayments
                .AsNoTracking()
                .Where(item => reversalInvoiceIds.Contains(item.InvoiceId)
                    && (item.Method == "cash" || item.Method == "card"))
                .GroupBy(item => item.InvoiceId)
                .Select(group => new
                {
                    InvoiceId = group.Key,
                    Amount = group.Sum(item => item.Amount)
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.InvoiceId, item => item.Amount);

        var reversals = reversalRows
            .Select(item => externalRefundByInvoice.GetValueOrDefault(item.InvoiceId))
            .Zip(reversalRows, (amount, item) => new { item, amount })
            .Where(row => row.amount > 0m)
            .Select(row => new FinanceTransactionResult(
                row.item.Id,
                row.item.CreatedAt,
                "برگشت: " + row.item.Description,
                -row.amount,
                -row.amount,
                "refund",
                InvoiceStatus.Cancelled.ToString(),
                "refund"));

        return payments
            .Concat(walletTopUps)
            .Concat(reversals)
            .OrderByDescending(item => item.ClosedAt)
            .ThenByDescending(item => item.Id)
            .Take(500)
            .ToList();
    }
}
