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

        var expense = await database.Expenses
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

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
        return await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Take(500)
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
    }
}
