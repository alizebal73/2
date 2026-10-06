using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class CustomerHistoryService(GameNetDbContext database)
{
    public async Task<IReadOnlyList<CustomerHistoryItemDto>> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var wallet = await database.WalletTransactions.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Select(item => new CustomerHistoryItemDto(
                item.Id,
                "wallet",
                item.Description,
                item.Amount,
                item.CreatedAt,
                item.ReferenceInvoiceId))
            .ToListAsync(cancellationToken);

        var benefits = await database.BenefitTransactions.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Select(item => new CustomerHistoryItemDto(
                item.Id,
                "benefit",
                item.Description,
                item.MoneyAmount,
                item.CreatedAt,
                item.ReferenceInvoiceId))
            .ToListAsync(cancellationToken);

        var payments = await database.InvoicePayments.AsNoTracking()
            .Where(item => item.Invoice.CustomerId == customerId)
            .Select(item => new CustomerHistoryItemDto(
                item.Id,
                "payment",
                "پرداخت " + item.Method,
                item.Amount,
                item.CreatedAt,
                item.InvoiceId))
            .ToListAsync(cancellationToken);

        var invoices = await database.Invoices.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Select(item => new CustomerHistoryItemDto(
                item.Id,
                "invoice",
                item.Status.ToString(),
                item.TotalAmount,
                item.IssuedAt,
                item.SessionId))
            .ToListAsync(cancellationToken);

        var sessions = await database.Sessions.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Select(item => new CustomerHistoryItemDto(
                item.Id,
                "session",
                "جلسه",
                item.TotalAmount,
                item.StartAt,
                item.Id))
            .ToListAsync(cancellationToken);

        return wallet
            .Concat(benefits)
            .Concat(payments)
            .Concat(invoices)
            .Concat(sessions)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToList();
    }
}
