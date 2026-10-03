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

public sealed class SessionSettlementService(GameNetDbContext database, SessionPricingService pricingService)
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
                .ThenInclude(item => item.VipPackage)
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

        var now = DateTimeOffset.UtcNow;
        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, now);
        if (request.FreeTimeMinutes < 0 || request.FreeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");
        if (request.TimeAmount is < 0 || request.DiscountAmount is < 0 || request.PrepaidAmount is < 0)
            throw new ArgumentException("جزئیات مبلغ تسویه معتبر نیست.");

        var pricing = await pricingService.GetPricingAsync(
            session.CustomerId,
            session.StationId,
            now,
            cancellationToken);
        var authoritativeTimeAmount = SessionPricingService.CalculateTimeAmount(
            session,
            pricing.HourlyRate,
            request.FreeTimeMinutes,
            now);

        if (request.TimeAmount.HasValue
            && Math.Abs(request.TimeAmount.Value - authoritativeTimeAmount) > 0.01m)
            throw new InvalidOperationException("مبلغ زمان جلسه با محاسبهٔ سرور مطابقت ندارد.");

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
        var timeAmount = authoritativeTimeAmount;
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
        if (prepaidAmount > session.PrepaidAmount)
            throw new InvalidOperationException("پیش‌پرداخت مصرف‌شده بیشتر از اعتبار ثبت‌شدهٔ جلسه است.");

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

        var expectedTotal = Math.Max(
            0m,
            existingBuffetTotal + timeAmount - discountAmount - prepaidAmount);

        if (Math.Abs(request.TotalAmount - expectedTotal) > 0.01m)
            throw new InvalidOperationException(
                $"مبلغ نهایی تسویه با محاسبهٔ سرور مطابقت ندارد. مبلغ معتبر: {expectedTotal:0.##}.");

        var itemSubtotal = existingBuffetTotal + timeAmount - discountAmount - prepaidAmount;

        invoice.AppUserId ??= request.AppUserId;
        invoice.TotalAmount = expectedTotal;
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

        session.TotalAmount = expectedTotal;
        session.EndAt ??= DateTimeOffset.UtcNow;
        session.State = SessionState.Completed;
        // A completed Session is terminal for the Station too. Keep the
        // Dashboard/read model derived from the same server-side invariant,
        // regardless of whether the session arrived here through Agent EndSession
        // or through an operator settlement path.
        session.Station.State = StationState.Available;

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

