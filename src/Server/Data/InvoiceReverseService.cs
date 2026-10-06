using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record InvoiceReverseRequest(Guid? AppUserId, string Reason);

public sealed record InvoiceReverseResult(
    Guid InvoiceId,
    string InvoiceStatus,
    decimal WalletRestored,
    decimal FreeMoneyRestored,
    int FreeTimeRestored,
    int InventoryRestored,
    bool ExternalRefundRequired,
    Guid ReversalId);

public sealed class InvoiceReverseService(GameNetDbContext database)
{
    public async Task<InvoiceReverseResult> ReverseAsync(
        Guid invoiceId,
        InvoiceReverseRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var result = await ReverseWithinTransactionAsync(invoiceId, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InvoiceReverseResult> ReverseWithinTransactionAsync(
        Guid invoiceId,
        InvoiceReverseRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("دلیل برگشت عملیات را وارد کنید.");

        var invoice = await database.Invoices
            .Include(item => item.Customer)
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
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

        var freeTimeRestored = await database.BenefitTransactions
            .AsNoTracking()
            .Where(item => item.ReferenceInvoiceId == invoice.Id && item.Type == BenefitTransactionType.FreeTimeDebit)
            .Select(item => (int?)item.Minutes)
            .SumAsync(cancellationToken) ?? 0;

        if (freeTimeRestored > 0)
        {
            invoice.Customer.FreeTimeMinutes += freeTimeRestored;
            database.BenefitTransactions.Add(new BenefitTransaction
            {
                CustomerId = invoice.CustomerId,
                Type = BenefitTransactionType.FreeTimeCredit,
                MoneyAmount = 0m,
                Minutes = freeTimeRestored,
                ReferenceInvoiceId = invoice.Id,
                Description = "برگشت اعتبار زمانی مصرف‌شده · " + reason
            });
        }

        var inventoryRestored = 0;
        var invoiceItemsByProduct = invoice.Items
            .Where(item => item.ProductId.HasValue && item.Quantity > 0 && item.Product is not null)
            .GroupBy(item => item.ProductId!.Value);

        foreach (var productGroup in invoiceItemsByProduct)
        {
            var product = productGroup.First().Product!;
            var productId = productGroup.Key;
            var saleMovements = await database.InventoryTransactions.AsNoTracking()
                .Where(movement => movement.ReferenceInvoiceId == invoice.Id
                    && movement.ProductId == productId
                    && movement.Kind == "Sale"
                    && movement.Direction == TransactionDirection.Out)
                .ToListAsync(cancellationToken);

            var totalItemQuantity = productGroup.Sum(item => item.Quantity);
            var totalItemAmount = productGroup.Sum(item => item.Quantity * item.UnitPrice);
            var remainingQuantity = totalItemQuantity;

            var areaBuckets = saleMovements
                .GroupBy(movement => movement.StockArea)
                .Select(group => new
                {
                    Area = group.Key,
                    Quantity = group.Sum(movement => movement.Quantity),
                    UnitCost = group.Sum(movement => movement.Quantity * movement.UnitCost) / Math.Max(1, group.Sum(movement => movement.Quantity))
                })
                .OrderByDescending(item => item.Area == StockArea.Showcase)
                .ToList();

            if (areaBuckets.Count == 0)
            {
                areaBuckets.Add(new
                {
                    Area = StockArea.Showcase,
                    Quantity = totalItemQuantity,
                    UnitCost = product.CostPrice
                });
            }

            foreach (var bucket in areaBuckets)
            {
                if (remainingQuantity <= 0)
                    break;

                var quantity = Math.Min(remainingQuantity, bucket.Quantity);
                if (quantity <= 0)
                    continue;

                if (bucket.Area == StockArea.Showcase)
                    product.ShowcaseStockQuantity += quantity;
                else
                    product.StockQuantity += quantity;

                database.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = totalItemQuantity > 0 ? totalItemAmount / totalItemQuantity : product.UnitPrice,
                    UnitCost = bucket.UnitCost,
                    ReferenceInvoiceId = invoice.Id,
                    Direction = TransactionDirection.In,
                    StockArea = bucket.Area,
                    Kind = "Return",
                    AppUserId = request.AppUserId,
                    Notes = "برگشت خودکار فروش فاکتور · " + reason
                });

                inventoryRestored += quantity;
                remainingQuantity -= quantity;
            }

            if (remainingQuantity > 0)
            {
                product.ShowcaseStockQuantity += remainingQuantity;
                database.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = productId,
                    Quantity = remainingQuantity,
                    UnitPrice = totalItemQuantity > 0 ? totalItemAmount / totalItemQuantity : product.UnitPrice,
                    UnitCost = product.CostPrice,
                    ReferenceInvoiceId = invoice.Id,
                    Direction = TransactionDirection.In,
                    StockArea = StockArea.Showcase,
                    Kind = "Return",
                    AppUserId = request.AppUserId,
                    Notes = "برگشت خودکار فروش فاکتور · " + reason
                });
                inventoryRestored += remainingQuantity;
            }
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
                + " · زمان " + freeTimeRestored.ToString()
                + " دقیقه · موجودی " + inventoryRestored.ToString()
                + (externalRefundRequired ? " · بازپرداخت نقد/کارت خارج از سیستم لازم است" : ""),
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);

        return new InvoiceReverseResult(
            invoice.Id,
            invoice.Status.ToString(),
            walletRestored,
            freeMoneyRestored,
            freeTimeRestored,
            inventoryRestored,
            externalRefundRequired,
            reversal.Id);
    }
}
