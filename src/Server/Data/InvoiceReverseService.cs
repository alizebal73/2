using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record InvoiceReverseRequest(Guid? AppUserId, string Reason);

public sealed record InvoiceReverseResult(
    Guid InvoiceId,
    string InvoiceStatus,
    decimal WalletRestored,
    decimal FreeMoneyRestored,
    bool ExternalRefundRequired,
    Guid ReversalId);

public sealed class InvoiceReverseService(GameNetDbContext database)
{
    public async Task<InvoiceReverseResult> ReverseAsync(
        Guid invoiceId,
        InvoiceReverseRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("دلیل برگشت عملیات را وارد کنید.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await database.Invoices
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);

        if (invoice is null)
            throw new KeyNotFoundException("فاکتور پیدا نشد.");

        if (invoice.Status != InvoiceStatus.Paid)
            throw new InvalidOperationException("این فاکتور قبلاً بسته یا برگشت داده شده است.");

        var existing = await database.InvoiceReversals
            .AsNoTracking()
            .AnyAsync(item => item.InvoiceId == invoiceId, cancellationToken);

        if (existing)
            throw new InvalidOperationException("برای این فاکتور قبلاً عملیات برگشت ثبت شده است.");

        var payments = await database.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .ToListAsync(cancellationToken);

        var walletRestored = payments
            .Where(item => item.Method.Equals("wallet", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Amount);

        var freeMoneyRestored = payments
            .Where(item => item.Method.Equals("gift", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Amount);

        var externalRefundRequired = payments.Any(item =>
            item.Method.Equals("cash", StringComparison.OrdinalIgnoreCase)
            || item.Method.Equals("card", StringComparison.OrdinalIgnoreCase));

        if (walletRestored > 0)
        {
            invoice.Customer.Balance += walletRestored;
            database.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = invoice.CustomerId,
                Amount = walletRestored,
                Type = WalletTransactionType.Credit,
                ReferenceInvoiceId = invoice.Id,
                Description = "برگشت تسویه از کیف پول · " + reason
            });
        }

        if (freeMoneyRestored > 0)
        {
            invoice.Customer.FreeMoney += freeMoneyRestored;
            database.BenefitTransactions.Add(new BenefitTransaction
            {
                CustomerId = invoice.CustomerId,
                Type = BenefitTransactionType.FreeMoneyCredit,
                MoneyAmount = freeMoneyRestored,
                Minutes = 0,
                ReferenceInvoiceId = invoice.Id,
                Description = "برگشت مصرف اعتبار رایگان · " + reason
            });
        }

        invoice.Status = InvoiceStatus.Cancelled;

        var reversal = new InvoiceReversal
        {
            InvoiceId = invoice.Id,
            AppUserId = request.AppUserId,
            Reason = reason,
            ExternalRefundRequired = externalRefundRequired,
        };
        database.InvoiceReversals.Add(reversal);

        database.AuditLogs.Add(new AuditLog
        {
            Action = "InvoiceReverse",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            Details = "برگشت فاکتور · " + reason
                + (externalRefundRequired ? " · بازپرداخت نقد/کارت خارج از سیستم لازم است" : ""),
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InvoiceReverseResult(
            invoice.Id,
            invoice.Status.ToString(),
            walletRestored,
            freeMoneyRestored,
            externalRefundRequired,
            reversal.Id);
    }
}
