using GameNetManager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record SettlementPart(string Method, decimal Amount);
public sealed record SessionSettlementRequest(decimal TotalAmount, IReadOnlyList<SettlementPart> Parts, Guid? AppUserId, int FreeTimeMinutes = 0);

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

        if (session.State != SessionState.Active)
            throw new InvalidOperationException("این جلسه قبلاً بسته شده یا قابل تسویه نیست.");

        var walletPart = normalizedParts
            .Where(item => item.Method == "wallet")
            .Sum(item => item.Amount);

        var giftPart = normalizedParts
            .Where(item => item.Method == "gift")
            .Sum(item => item.Amount);

        var elapsedMinutes = Math.Max(0, (DateTimeOffset.UtcNow - session.StartAt).TotalMinutes);
        if (request.FreeTimeMinutes < 0 || request.FreeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");
        var invoice = new Invoice
        {
            CustomerId = session.CustomerId,
            SessionId = session.Id,
            AppUserId = request.AppUserId,
            TotalAmount = request.TotalAmount,
            Status = InvoiceStatus.Paid,
            IssuedAt = DateTimeOffset.UtcNow,
            PaidAt = DateTimeOffset.UtcNow,
            Items =
            {
                new InvoiceItem
                {
                    Description = "تسویه جلسه " + session.Station.Name,
                    Quantity = 1,
                    UnitPrice = request.TotalAmount,
                    Amount = request.TotalAmount
                }
            }
        };

        database.Invoices.Add(invoice);


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
        session.EndAt = DateTimeOffset.UtcNow;
        session.State = SessionState.Completed;

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

