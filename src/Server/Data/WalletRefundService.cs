using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record WalletRefundRequest(Guid CustomerId, decimal Amount, Guid? SourceTransactionId, string Reason);

public sealed record WalletRefundResult(
    Guid TransactionId,
    Guid CustomerId,
    decimal Amount,
    decimal Balance,
    Guid? ReferenceTransactionId,
    DateTimeOffset CreatedAt);

public sealed class WalletRefundService(GameNetDbContext database)
{
    public async Task<WalletRefundResult> RefundAsync(
        WalletRefundRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var result = await RefundWithinTransactionAsync(request, appUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<WalletRefundResult> RefundWithinTransactionAsync(
        WalletRefundRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("مبلغ بازگشت باید بیشتر از صفر باشد.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("دلیل بازگشت وجه را وارد کنید.");

        var customer = await database.Customers
            .FirstOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new KeyNotFoundException("مشتری پیدا نشد.");

        if (customer.Balance < request.Amount)
            throw new InvalidOperationException("موجودی کیف پول برای بازگشت این مبلغ کافی نیست.");

        WalletTransaction? source = null;
        if (request.SourceTransactionId is Guid sourceId)
        {
            source = await database.WalletTransactions
                .FirstOrDefaultAsync(item => item.Id == sourceId && item.CustomerId == request.CustomerId, cancellationToken);

            if (source is null)
                throw new KeyNotFoundException("تراکنش مبدأ بازگشت وجه پیدا نشد.");

            if (source.Type != WalletTransactionType.Credit)
                throw new ArgumentException("تراکنش انتخاب‌شده قابل بازگشت نیست.");

            var alreadyRefunded = await database.WalletTransactions
                .Where(item => item.ReferenceTransactionId == source.Id && item.Type == WalletTransactionType.Debit)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

            if (alreadyRefunded + request.Amount > source.Amount)
                throw new InvalidOperationException("مبلغ بازگشت از مانده قابل بازگشت تراکنش مبدأ بیشتر است.");
        }

        customer.Balance -= request.Amount;

        var ledger = new WalletTransaction
        {
            CustomerId = customer.Id,
            Amount = request.Amount,
            Type = WalletTransactionType.Debit,
            ReferenceTransactionId = source?.Id,
            Description = "بازگشت وجه · " + reason
        };

        database.WalletTransactions.Add(ledger);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "WalletRefund",
            EntityName = "CustomerWallet",
            EntityId = customer.Id.ToString(),
            Details = request.Amount.ToString("0.##") + " تومان · " + reason
                + (source is null ? "" : " · مرجع " + source.Id),
            AppUserId = appUserId
        });

        await database.SaveChangesAsync(cancellationToken);

        return new WalletRefundResult(
            ledger.Id,
            ledger.CustomerId,
            ledger.Amount,
            customer.Balance,
            ledger.ReferenceTransactionId,
            ledger.CreatedAt);
    }
}