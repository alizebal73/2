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
    string Method,
    string Status);

public sealed class FinanceReportService(GameNetDbContext database)
{
    public async Task<FinanceSummaryResult> GetSummaryAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var revenue = await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var reversedInvoiceIds = await database.InvoiceReversals
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .Select(item => item.InvoiceId)
            .ToListAsync(cancellationToken);

        var reversedAmount = reversedInvoiceIds.Count == 0
            ? 0m
            : await database.InvoicePayments
                .AsNoTracking()
                .Where(item => reversedInvoiceIds.Contains(item.InvoiceId))
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var expense = await database.Expenses
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        return new FinanceSummaryResult(
            start,
            end,
            revenue - reversedAmount,
            expense,
            revenue - reversedAmount - expense);
    }

    public async Task<IReadOnlyList<FinanceTransactionResult>> GetTransactionsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var payments = await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .Select(item => new FinanceTransactionResult(
                item.Id,
                item.CreatedAt,
                item.Invoice.Items
                    .OrderBy(child => child.Id)
                    .Select(child => child.Description)
                    .FirstOrDefault() ?? "تراکنش مالی",
                item.Amount,
                item.Method,
                item.Invoice.Status.ToString()))
            .ToListAsync(cancellationToken);

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
                    .FirstOrDefault() ?? "برگشت فاکتور",
                Amount = database.InvoicePayments
                    .Where(payment => payment.InvoiceId == item.InvoiceId)
                    .Select(payment => (decimal?)payment.Amount)
                    .Sum() ?? 0m
            })
            .ToListAsync(cancellationToken);

        var reversals = reversalRows
            .Where(item => item.Amount > 0m)
            .Select(item => new FinanceTransactionResult(
                item.Id,
                item.CreatedAt,
                "برگشت: " + item.Description,
                -item.Amount,
                "refund",
                InvoiceStatus.Cancelled.ToString()))
            .ToList();

        return payments
            .Concat(reversals)
            .OrderByDescending(item => item.ClosedAt)
            .ThenByDescending(item => item.Id)
            .Take(500)
            .ToList();
    }
}
