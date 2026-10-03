using GameNetManager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record SettlementPart(string Method, decimal Amount);
public sealed record SessionSettlementRequest(
    decimal TotalAmount,
    IReadOnlyList<SettlementPart> Parts,
    Guid? AppUserId,
    int FreeTimeMinutes = 0,
    decimal? TimeAmount = null,
    decimal? DiscountAmount = null,
    decimal? PrepaidAmount = null);

public sealed class SessionSettlementService(GameNetDbContext database)
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash",
        "card",
        "wallet",
        "gift"
    };

    public async Task<SettlementResult> SettleAsync(
        Guid sessionId,
        SessionSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TotalAmount < 0)
            throw new ArgumentException("مبلغ تسویه نمی‌تواند منفی باشد.");

        if (request.Parts is null || request.Parts.Count == 0)
            throw new ArgumentException("حداقل یک روش پرداخت لازم است.");

        var normalizedParts = request.Parts
            .Where(item => item.Amount > 0)
            .Select(item => new SettlementPart(item.Method.Trim().ToLowerInvariant(), item.Amount))
            .ToList();

        if (normalizedParts.Count == 0 || normalizedParts.Any(item => !AllowedMethods.Contains(item.Method)))
            throw new ArgumentException("روش پرداخت معتبر نیست.");

        var partsTotal = normalizedParts.Sum(item => item.Amount);
        if (partsTotal != request.TotalAmount)
            throw new ArgumentException("جمع روش‌های پرداخت باید دقیقاً برابر مبلغ تسویه باشد.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var session = await database.Sessions
            .Include(item => item.Customer)
            .Include(item => item.Station)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is null)
            throw new KeyNotFoundException("جلسه پیدا نشد.");

        if (session.State is not SessionState.Active and not SessionState.Ended)
            throw new InvalidOperationException("این جلسه قبلاً بسته شده یا قابل تسویه نیست.");

        var walletPart = normalizedParts
            .Where(item => item.Method == "wallet")
            .Sum(item => item.Amount);

        var giftPart = normalizedParts
            .Where(item => item.Method == "gift")
            .Sum(item => item.Amount);

        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, DateTimeOffset.UtcNow);
        if (request.FreeTimeMinutes < 0 || request.FreeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");
        if (request.TimeAmount is < 0 || request.DiscountAmount is < 0 || request.PrepaidAmount is < 0)
            throw new ArgumentException("جزئیات مبلغ تسویه معتبر نیست.");

        var invoice = await database.Invoices
            .FirstOrDefaultAsync(item => item.SessionId == session.Id && item.Status == InvoiceStatus.Draft, cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice
            {
                CustomerId = session.CustomerId,
                SessionId = session.Id,
                AppUserId = request.AppUserId,
                TotalAmount = 0m,
                Status = InvoiceStatus.Draft,
                IssuedAt = DateTimeOffset.UtcNow
            };
            database.Invoices.Add(invoice);
        }
        else if (invoice.CustomerId != session.CustomerId)
        {
            throw new InvalidOperationException("فاکتور بوفه متعلق به این مشتری نیست.");
        }

        var existingBuffetTotal = await database.InvoiceItems
            .Where(item => item.InvoiceId == invoice.Id && item.ProductId.HasValue)
            .SumAsync(item => item.Amount, cancellationToken);
        var timeAmount = request.TimeAmount ?? Math.Max(0m, request.TotalAmount - existingBuffetTotal);
        if (timeAmount > 0)
        {
            database.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                Description = "هزینه جلسه " + session.Station.Name,
                Quantity = 1,
                UnitPrice = timeAmount,
                Amount = timeAmount
            });
        }

        var discountAmount = Math.Max(0m, request.DiscountAmount ?? 0m);
        if (discountAmount > 0)
        {
            database.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                Description = "تخفیف تسویه جلسه",
                Quantity = 1,
                UnitPrice = -discountAmount,
                Amount = -discountAmount
            });
        }

        var prepaidAmount = Math.Max(0m, request.PrepaidAmount ?? 0m);
        if (prepaidAmount > 0)
        {
            database.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                Description = "اعتبار پیش‌پرداخت جلسه",
                Quantity = 1,
                UnitPrice = -prepaidAmount,
                Amount = -prepaidAmount
            });
        }

        var itemSubtotal = existingBuffetTotal + timeAmount - discountAmount - prepaidAmount;
        var roundingAdjustment = request.TotalAmount - itemSubtotal;
        if (Math.Abs(roundingAdjustment) >= 0.01m)
        {
            database.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                Description = "تعدیل نهایی تسویه",
                Quantity = 1,
                UnitPrice = roundingAdjustment,
                Amount = roundingAdjustment
            });
        }

        invoice.AppUserId ??= request.AppUserId;
        invoice.TotalAmount = request.TotalAmount;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTimeOffset.UtcNow;


        if (request.FreeTimeMinutes > session.Customer.FreeTimeMinutes)
            throw new InvalidOperationException("اعتبار زمانی رایگان مشتری برای این مصرف کافی نیست.");

        if (giftPart > session.Customer.FreeMoney)
            throw new InvalidOperationException("اعتبار مالی رایگان مشتری برای این سهم کافی نیست.");

        if (request.FreeTimeMinutes > 0)
        {
            session.Customer.FreeTimeMinutes -= request.FreeTimeMinutes;
            database.BenefitTransactions.Add(new BenefitTransaction
            {
                CustomerId = session.CustomerId,
                Type = BenefitTransactionType.FreeTimeDebit,
                Minutes = request.FreeTimeMinutes,
                MoneyAmount = 0m,
                Description = "مصرف اعتبار زمانی رایگان در تسویه " + session.Station.Name,
                ReferenceInvoiceId = invoice.Id
            });
        }

        if (giftPart > 0)
        {
            session.Customer.FreeMoney -= giftPart;
            database.BenefitTransactions.Add(new BenefitTransaction
            {
                CustomerId = session.CustomerId,
                Type = BenefitTransactionType.FreeMoneyDebit,
                Minutes = 0,
                MoneyAmount = giftPart,
                Description = "مصرف اعتبار مالی رایگان در تسویه " + session.Station.Name,
                ReferenceInvoiceId = invoice.Id
            });
        }


        if (walletPart > session.Customer.Balance)
            throw new InvalidOperationException("موجودی کیف پول برای سهم انتخاب‌شده کافی نیست.");

        if (walletPart > 0)
        {
            session.Customer.Balance -= walletPart;
            database.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = session.CustomerId,
                Amount = walletPart,
                Type = WalletTransactionType.Debit,
                ReferenceInvoiceId = invoice.Id,
                Description = "تسویه جلسه " + session.Station.Name
            });
        }



        foreach (var part in normalizedParts)
        {
            database.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                Method = part.Method,
                Amount = part.Amount,
            });
        }

        session.TotalAmount = request.TotalAmount;
        session.EndAt ??= DateTimeOffset.UtcNow;
        session.State = SessionState.Completed;

        var activeLeases = await database.AccountLeases
            .AsNoTracking()
            .Where(item => item.SessionId == session.Id && item.State == AccountLeaseState.Active)
            .ToListAsync(cancellationToken);

        foreach (var lease in activeLeases)
        {
            var now = DateTimeOffset.UtcNow;
            var leaseUpdated = await database.AccountLeases
                .Where(item => item.Id == lease.Id && item.State == AccountLeaseState.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, AccountLeaseState.Released)
                    .SetProperty(item => item.ReleasedAt, now)
                    .SetProperty(item => item.CredentialAccessExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(item => item.ReleaseReason, "تسویه جلسه")
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);

            if (leaseUpdated != 1)
                continue;

            var accountUpdated = await database.AccountPoolEntries
                .Where(item => item.Id == lease.AccountPoolEntryId && item.Status == AccountPoolStatus.InUse)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, AccountPoolStatus.Free)
                    .SetProperty(item => item.AssignedAgentDeviceId, (Guid?)null)
                    .SetProperty(item => item.UpdatedAt, now), cancellationToken);

            if (accountUpdated != 1)
                throw new InvalidOperationException("آزادسازی اکانت جلسه کامل نشد.");

            database.AuditLogs.Add(new AuditLog
            {
                Action = "AccountLeaseReleased",
                EntityName = "AccountLease",
                EntityId = lease.Id.ToString(),
                Details = "آزادسازی خودکار Lease هنگام تسویه جلسه · " + session.Id
            });
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = "SessionSettlement",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            Details = string.Join(" · ", normalizedParts.Select(item => item.Method + " " + item.Amount.ToString("0.##") + " تومان")),
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResult(
            invoice.Id,
            session.Id,
            invoice.TotalAmount,
            normalizedParts,
            session.Customer.Balance,
            session.Customer.FreeMoney,
            session.Customer.FreeTimeMinutes,
            invoice.Status.ToString(),
            invoice.PaidAt ?? DateTimeOffset.UtcNow);
    }
}

public sealed record SettlementResult(
    Guid InvoiceId,
    Guid SessionId,
    decimal TotalAmount,
    IReadOnlyList<SettlementPart> Parts,
    decimal WalletBalanceAfter,
    decimal FreeMoneyBalanceAfter,
    int FreeTimeMinutesAfter,
    string InvoiceStatus,
    DateTimeOffset PaidAt);

