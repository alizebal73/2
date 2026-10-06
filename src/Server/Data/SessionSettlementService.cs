using System.Text.Json;
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

public sealed record SessionSettlementPreview(
    Guid SessionId,
    DateTimeOffset ServerUtcNow,
    decimal HourlyRate,
    double BillableMinutes,
    decimal TimeAmount,
    decimal BuffetTotal,
    decimal DiscountAmount,
    decimal PrepaidAmount,
    decimal TotalAmount);

public sealed record SessionChargeResult(
    Guid ChargeId,
    Guid InvoiceId,
    decimal Amount,
    decimal PrepaidTotal,
    string Method,
    decimal WalletBalanceAfter,
    DateTimeOffset? SessionEndAt);

public sealed record PendingSettlementChargeDto(
    Guid Id,
    decimal Amount,
    string Method,
    DateTimeOffset CreatedAt);

public sealed record PendingSettlementBuffetItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal Amount);

public sealed record PendingSettlementDto(
    Guid InvoiceId,
    Guid SessionId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerCode,
    string? Username,
    string StationName,
    DateTimeOffset ClosedAt,
    int WaitingMinutes,
    decimal TimeAmount,
    decimal BuffetTotal,
    decimal OtherAmount,
    decimal GrossAmount,
    decimal PrepaidTotal,
    decimal PrepaidApplied,
    decimal PrepaidRemaining,
    decimal CreditOrBenefitReduction,
    decimal AmountDue,
    IReadOnlyList<PendingSettlementChargeDto> Charges,
    IReadOnlyList<PendingSettlementBuffetItemDto> BuffetItems);

public sealed record SessionChargeRequest(decimal Amount, string Method);
public sealed record SessionSettleLaterRequest(int FreeTimeMinutes = 0);
public sealed record PendingSettlementPaymentRequest(
    decimal TotalAmount,
    IReadOnlyList<SettlementPart> Parts,
    Guid? AppUserId,
    decimal? DiscountAmount = null);

public sealed class SessionSettlementService(GameNetDbContext database, SessionPricingService pricingService)
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash",
        "card",
        "wallet",
        "gift"
    };

    private async Task<Invoice> GetOrCreateCustomerAccountAsync(
        Session session,
        Guid? appUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await database.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.CustomerId == session.CustomerId
                    && item.Status == InvoiceStatus.Draft
                    && item.IsCustomerAccount,
                cancellationToken);

        var sessionInvoice = await database.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.SessionId == session.Id
                    && item.Status == InvoiceStatus.Draft
                    && !item.IsCustomerAccount,
                cancellationToken);

        if (account is null)
        {
            if (sessionInvoice is not null)
            {
                account = sessionInvoice;
                account.IsCustomerAccount = true;
                account.SessionId = session.Id;
                foreach (var item in account.Items)
                    item.SessionId ??= session.Id;
            }
            else
            {
                account = new Invoice
                {
                    CustomerId = session.CustomerId,
                    SessionId = session.Id,
                    AppUserId = appUserId,
                    TotalAmount = 0m,
                    Status = InvoiceStatus.Draft,
                    IsCustomerAccount = true,
                    IssuedAt = now
                };
                database.Invoices.Add(account);
                await database.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            account.SessionId = session.Id;
            account.AppUserId ??= appUserId;

            if (sessionInvoice is not null && sessionInvoice.Id != account.Id)
            {
                foreach (var item in sessionInvoice.Items)
                {
                    item.InvoiceId = account.Id;
                    if (!item.SessionId.HasValue)
                        item.SessionId = session.Id;
                    if (!account.Items.Contains(item))
                        account.Items.Add(item);
                }

                var payments = await database.InvoicePayments
                    .Where(item => item.InvoiceId == sessionInvoice.Id)
                    .ToListAsync(cancellationToken);
                foreach (var payment in payments)
                    payment.InvoiceId = account.Id;

                var charges = await database.SessionCharges
                    .Where(item => item.InvoiceId == sessionInvoice.Id)
                    .ToListAsync(cancellationToken);
                foreach (var charge in charges)
                    charge.InvoiceId = account.Id;

                var walletTransactions = await database.WalletTransactions
                    .Where(item => item.ReferenceInvoiceId == sessionInvoice.Id)
                    .ToListAsync(cancellationToken);
                foreach (var entry in walletTransactions)
                    entry.ReferenceInvoiceId = account.Id;

                var benefits = await database.BenefitTransactions
                    .Where(item => item.ReferenceInvoiceId == sessionInvoice.Id)
                    .ToListAsync(cancellationToken);
                foreach (var entry in benefits)
                    entry.ReferenceInvoiceId = account.Id;

                var inventory = await database.InventoryTransactions
                    .Where(item => item.ReferenceInvoiceId == sessionInvoice.Id)
                    .ToListAsync(cancellationToken);
                foreach (var entry in inventory)
                    entry.ReferenceInvoiceId = account.Id;

                sessionInvoice.Status = InvoiceStatus.Cancelled;
                sessionInvoice.TotalAmount = 0m;
                sessionInvoice.IsCustomerAccount = false;
            }
        }

        return account;
    }

    public async Task<SessionSettlementPreview> PreviewAsync(
        Guid sessionId,
        int freeTimeMinutes = 0,
        decimal discountAmount = 0m,
        decimal prepaidAmount = 0m,
        CancellationToken cancellationToken = default)
    {
        if (freeTimeMinutes < 0 || discountAmount < 0 || prepaidAmount < 0)
            throw new ArgumentException("جزئیات مبلغ تسویه معتبر نیست.");

        var session = await database.Sessions
            .AsNoTracking()
            .Include(item => item.Customer)
                .ThenInclude(item => item.VipPackage)
            .FirstOrDefaultAsync(
                item => item.Id == sessionId
                    && (item.State == SessionState.Active || item.State == SessionState.Ended),
                cancellationToken);

        if (session is null)
            throw new KeyNotFoundException("جلسه پیدا نشد.");

        var now = DateTimeOffset.UtcNow;
        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, now);
        if (freeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");

        if (freeTimeMinutes > session.Customer.FreeTimeMinutes)
            throw new InvalidOperationException("اعتبار زمانی رایگان مشتری برای این مصرف کافی نیست.");

        if (prepaidAmount > session.PrepaidAmount)
            throw new InvalidOperationException("پیش‌پرداخت مصرف‌شده بیشتر از اعتبار ثبت‌شدهٔ جلسه است.");

        var pricing = await pricingService.GetPricingAsync(
            session.CustomerId,
            session.StationId,
            now,
            cancellationToken);

        var authoritativeHourlyRate = session.HourlyRateOverride
            ?? session.HourlyRateSnapshot
            ?? pricing.HourlyRate;

        var authoritativeTimeAmount = SessionPricingService.CalculateTimeAmount(
            session,
            authoritativeHourlyRate,
            freeTimeMinutes,
            now);

        var invoiceId = await database.Invoices
            .AsNoTracking()
            .Where(item => item.CustomerId == session.CustomerId
                && item.Status == InvoiceStatus.Draft
                && item.IsCustomerAccount)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await database.Invoices
                .AsNoTracking()
                .Where(item => item.SessionId == session.Id
                    && item.Status == InvoiceStatus.Draft
                    && !item.IsCustomerAccount)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        var buffetTotal = invoiceId.HasValue
            ? await database.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == invoiceId.Value
                    && item.SessionId == session.Id
                    && item.ProductId.HasValue)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m
            : 0m;

        var totalAmount = Math.Max(
            0m,
            buffetTotal + authoritativeTimeAmount - discountAmount - prepaidAmount);

        return new SessionSettlementPreview(
            session.Id,
            now,
            authoritativeHourlyRate,
            elapsedMinutes,
            authoritativeTimeAmount,
            buffetTotal,
            discountAmount,
            prepaidAmount,
            totalAmount);
    }

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
        if (Math.Abs(partsTotal - request.TotalAmount) > 0.01m)
            throw new ArgumentException("جمع روش‌های پرداخت باید دقیقاً برابر مبلغ تسویه باشد.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var lockStamp = DateTimeOffset.UtcNow;
        var locked = await database.Sessions
            .Where(item => item.Id == sessionId
                && (item.State == SessionState.Active || item.State == SessionState.Ended))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, lockStamp), cancellationToken);

        if (locked != 1)
            throw new InvalidOperationException("این جلسه قبلاً بسته یا تسویه شده است.");

        database.ChangeTracker.Clear();

        var session = await database.Sessions
            .Include(item => item.Customer)
                .ThenInclude(item => item.VipPackage)
            .Include(item => item.Station)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is null)
            throw new KeyNotFoundException("جلسه پیدا نشد.");

        var now = DateTimeOffset.UtcNow;
        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, now);
        if (request.FreeTimeMinutes < 0 || request.FreeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");

        if (request.TimeAmount is < 0 || request.DiscountAmount is < 0)
            throw new ArgumentException("جزئیات مبلغ تسویه معتبر نیست.");

        var pricing = await pricingService.GetPricingAsync(
            session.CustomerId,
            session.StationId,
            now,
            cancellationToken);

        var authoritativeHourlyRate = session.HourlyRateOverride
            ?? session.HourlyRateSnapshot
            ?? pricing.HourlyRate;

        var authoritativeTimeAmount = SessionPricingService.CalculateTimeAmount(
            session,
            authoritativeHourlyRate,
            request.FreeTimeMinutes,
            now);

        if (request.TimeAmount.HasValue
            && Math.Abs(request.TimeAmount.Value - authoritativeTimeAmount) > 0.01m)
            throw new InvalidOperationException("مبلغ زمان جلسه با محاسبهٔ سرور مطابقت ندارد.");

        var invoice = await GetOrCreateCustomerAccountAsync(
            session,
            request.AppUserId,
            now,
            cancellationToken);

        var existingTimeLine = invoice.Items.FirstOrDefault(item =>
            item.SessionId == session.Id
            && item.ProductId is null
            && item.Description.StartsWith("هزینه جلسه", StringComparison.Ordinal));

        if (existingTimeLine is null && authoritativeTimeAmount > 0)
        {
            invoice.Items.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                SessionId = session.Id,
                Description = "هزینه جلسه " + session.Station.Name,
                Quantity = 1,
                UnitPrice = authoritativeTimeAmount,
                Amount = authoritativeTimeAmount
            });
        }

        if (request.FreeTimeMinutes > 0
            && !invoice.Items.Any(item =>
                item.SessionId == session.Id
                && item.Description.StartsWith("اعتبار زمانی رایگان", StringComparison.Ordinal)))
        {
            var rawAmount = SessionPricingService.CalculateTimeAmount(
                session,
                authoritativeHourlyRate,
                0,
                now);
            var reduction = Math.Max(0m, rawAmount - authoritativeTimeAmount);

            if (reduction > 0)
            {
                invoice.Items.Add(new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    SessionId = session.Id,
                    Description = "اعتبار زمانی رایگان",
                    Quantity = 1,
                    UnitPrice = -reduction,
                    Amount = -reduction
                });

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
        }

        var currentSessionGross = invoice.Items
            .Where(item => item.SessionId == session.Id)
            .Sum(item => item.Amount);

        var currentSessionPaid = await database.InvoicePayments
            .Where(item => item.InvoiceId == invoice.Id && item.SessionId == session.Id)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var discountAmount = Math.Max(0m, request.DiscountAmount ?? 0m);
        var currentDueBeforeDiscount = Math.Max(0m, currentSessionGross - currentSessionPaid);

        if (discountAmount > currentDueBeforeDiscount)
            throw new InvalidOperationException("مبلغ تخفیف نمی‌تواند از مبلغ قابل پرداخت این جلسه بیشتر باشد.");

        if (request.AppUserId.HasValue && discountAmount > 0)
        {
            var settlementUser = await database.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.AppUserId.Value, cancellationToken);

            if (settlementUser is not null
                && string.Equals(settlementUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
            {
                var operatorDiscountPercent = 10;
                var storedSetting = await database.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.ScopeKey == ServerSettingsCatalog.GlobalScope
                            && item.Key == "operatorDiscount",
                        cancellationToken);

                if (storedSetting is not null)
                {
                    try
                    {
                        using var settingJson = JsonDocument.Parse(storedSetting.ValueJson);
                        if (settingJson.RootElement.TryGetInt32(out var configuredPercent))
                            operatorDiscountPercent = Math.Clamp(configuredPercent, 0, 100);
                    }
                    catch (JsonException)
                    {
                    }
                }

                var maxOperatorDiscount = Math.Round(
                    currentDueBeforeDiscount * operatorDiscountPercent / 100m,
                    2,
                    MidpointRounding.ToEven);

                if (discountAmount > maxOperatorDiscount + 0.01m)
                    throw new InvalidOperationException(
                        $"تخفیف اپراتور بیش از سقف مجاز {operatorDiscountPercent}% است.");
            }

            invoice.Items.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                SessionId = session.Id,
                Description = "تخفیف تسویه جلسه",
                Quantity = 1,
                UnitPrice = -discountAmount,
                Amount = -discountAmount
            });

            currentSessionGross -= discountAmount;
        }

        var currentDue = Math.Max(0m, currentSessionGross - currentSessionPaid);

        if (Math.Abs(request.TotalAmount - currentDue) > 0.01m)
            throw new InvalidOperationException(
                $"مبلغ نهایی تسویه با محاسبهٔ سرور مطابقت ندارد. مبلغ معتبر: {currentDue:0.##}.");

        var walletPart = normalizedParts
            .Where(item => item.Method == "wallet")
            .Sum(item => item.Amount);
        var giftPart = normalizedParts
            .Where(item => item.Method == "gift")
            .Sum(item => item.Amount);

        if (walletPart > session.Customer.Balance)
            throw new InvalidOperationException("موجودی کیف پول برای سهم انتخاب‌شده کافی نیست.");

        if (giftPart > session.Customer.FreeMoney)
            throw new InvalidOperationException("اعتبار مالی رایگان مشتری برای این سهم کافی نیست.");

        if (walletPart > 0)
        {
            session.Customer.Balance -= walletPart;
            database.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = session.CustomerId,
                Amount = walletPart,
                Type = WalletTransactionType.Debit,
                ReferenceInvoiceId = invoice.Id,
                Description = "تسویه جلسه " + session.Station.Name + " از کیف پول"
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

        foreach (var part in normalizedParts)
        {
            database.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                SessionId = session.Id,
                Method = part.Method,
                Amount = part.Amount
            });
        }

        invoice.AppUserId ??= request.AppUserId;
        invoice.TotalAmount = Math.Max(0m, invoice.Items.Sum(item => item.Amount));

        var allPayments = await database.InvoicePayments
            .Where(item => item.InvoiceId == invoice.Id)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var accountDueAfterPayment = Math.Max(0m, invoice.TotalAmount - allPayments);
        var hasOtherActiveSessions = await database.Sessions
            .AsNoTracking()
            .AnyAsync(
                item => item.CustomerId == session.CustomerId
                    && item.Id != session.Id
                    && item.State == SessionState.Active,
                cancellationToken);

        invoice.Status = accountDueAfterPayment <= 0.01m && !hasOtherActiveSessions
            ? InvoiceStatus.Paid
            : InvoiceStatus.Draft;
        invoice.PaidAt = invoice.Status == InvoiceStatus.Paid
            ? DateTimeOffset.UtcNow
            : null;

        session.TotalAmount = currentDue;
        session.EndAt ??= now;
        session.State = SessionState.Completed;
        session.Station.State = StationState.Available;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "SessionSettlement",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            Details = string.Join(" · ", normalizedParts.Select(item => item.Method + " " + item.Amount.ToString("0.##") + " تومان"))
                + " · مانده حساب مشتری " + accountDueAfterPayment.ToString("0.##") + " تومان",
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResult(
            invoice.Id,
            session.Id,
            currentDue,
            normalizedParts,
            session.Customer.Balance,
            session.Customer.FreeMoney,
            session.Customer.FreeTimeMinutes,
            invoice.Status.ToString(),
            invoice.PaidAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<SessionChargeResult> ChargeAsync(
        Guid sessionId,
        decimal amount,
        string method,
        Guid? appUserId,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new ArgumentException("مبلغ شارژ باید بیشتر از صفر باشد.");

        var normalizedMethod = (method ?? "").Trim().ToLowerInvariant();
        if (normalizedMethod is not ("cash" or "card" or "wallet"))
            throw new ArgumentException("روش پرداخت شارژ معتبر نیست.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var session = await database.Sessions
            .Include(item => item.Customer)
                .ThenInclude(item => item.VipPackage)
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.Id == sessionId && item.State == SessionState.Active,
                cancellationToken);

        if (session is null)
            throw new InvalidOperationException("جلسه فعال پیدا نشد.");

        var invoice = await GetOrCreateCustomerAccountAsync(
            session,
            appUserId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (normalizedMethod == "wallet")
        {
            if (session.Customer.Balance < amount)
                throw new InvalidOperationException("موجودی کیف پول برای شارژ زمان کافی نیست.");

            session.Customer.Balance -= amount;
            database.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = session.CustomerId,
                Amount = amount,
                Type = WalletTransactionType.Debit,
                ReferenceInvoiceId = invoice.Id,
                Description = "شارژ زمان جلسه از کیف پول"
            });
        }

        var pricing = await pricingService.GetPricingAsync(
            session.CustomerId,
            session.StationId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        var hourlyRate = session.HourlyRateOverride
            ?? session.HourlyRateSnapshot
            ?? pricing.HourlyRate;
        if (hourlyRate <= 0)
            throw new InvalidOperationException("نرخ جلسه برای محاسبه شارژ معتبر نیست.");

        var chargeNow = DateTimeOffset.UtcNow;
        var extraMinutes = amount / (hourlyRate / 60m);
        var currentEnd = session.EndAt.HasValue && session.EndAt.Value > chargeNow
            ? session.EndAt.Value
            : chargeNow;
        session.EndAt = currentEnd.AddMinutes((double)extraMinutes);
        session.PrepaidAmount += amount;

        var charge = new SessionCharge
        {
            SessionId = session.Id,
            InvoiceId = invoice.Id,
            AppUserId = appUserId,
            Amount = amount,
            Method = normalizedMethod
        };
        database.SessionCharges.Add(charge);
        database.InvoicePayments.Add(new InvoicePayment
        {
            InvoiceId = invoice.Id,
            SessionId = session.Id,
            Method = normalizedMethod,
            Amount = amount
        });
        database.AuditLogs.Add(new AuditLog
        {
            Action = "SessionCharge",
            EntityName = "Session",
            EntityId = session.Id.ToString(),
            AppUserId = appUserId,
            Details = "شارژ زمان · " + amount.ToString("0.##") + " تومان · " + normalizedMethod
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SessionChargeResult(
            charge.Id,
            invoice.Id,
            amount,
            session.PrepaidAmount,
            normalizedMethod,
            session.Customer.Balance,
            session.EndAt);
    }

    public async Task<PendingSettlementDto> PreparePendingAsync(
        Guid sessionId,
        int freeTimeMinutes,
        Guid? appUserId,
        CancellationToken cancellationToken)
    {
        if (freeTimeMinutes < 0)
            throw new ArgumentException("اعتبار زمانی رایگان معتبر نیست.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var session = await database.Sessions
            .Include(item => item.Customer)
                .ThenInclude(item => item.VipPackage)
            .Include(item => item.Station)
            .FirstOrDefaultAsync(
                item => item.Id == sessionId
                    && (item.State == SessionState.Active || item.State == SessionState.Ended),
                cancellationToken);

        if (session is null)
            throw new InvalidOperationException("جلسه برای ثبت پرداخت بعداً پیدا نشد.");

        var now = DateTimeOffset.UtcNow;
        var elapsedMinutes = SessionTiming.GetBillableMinutes(session, now);
        if (freeTimeMinutes > Math.Ceiling(elapsedMinutes))
            throw new InvalidOperationException("دقیقه اعتبار رایگان مصرف‌شده با زمان جلسه سازگار نیست.");
        if (freeTimeMinutes > session.Customer.FreeTimeMinutes)
            throw new InvalidOperationException("اعتبار زمانی رایگان مشتری برای این مصرف کافی نیست.");

        var pricing = await pricingService.GetPricingAsync(
            session.CustomerId,
            session.StationId,
            now,
            cancellationToken);

        var hourlyRate = session.HourlyRateOverride
            ?? session.HourlyRateSnapshot
            ?? pricing.HourlyRate;

        var timeAmount = SessionPricingService.CalculateTimeAmount(
            session,
            hourlyRate,
            freeTimeMinutes,
            now);

        var invoice = await GetOrCreateCustomerAccountAsync(
            session,
            appUserId,
            now,
            cancellationToken);

        var existingSessionTime = invoice.Items.Any(item =>
            item.SessionId == session.Id
            && item.ProductId is null
            && item.Description.StartsWith("هزینه جلسه", StringComparison.Ordinal));

        if (!existingSessionTime && timeAmount > 0)
        {
            invoice.Items.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                SessionId = session.Id,
                Description = "هزینه جلسه " + session.Station.Name,
                Quantity = 1,
                UnitPrice = timeAmount,
                Amount = timeAmount
            });
        }

        if (freeTimeMinutes > 0
            && !invoice.Items.Any(item =>
                item.SessionId == session.Id
                && item.Description.StartsWith("اعتبار زمانی رایگان", StringComparison.Ordinal)))
        {
            var rawAmount = SessionPricingService.CalculateTimeAmount(
                session,
                hourlyRate,
                0,
                now);
            var reduction = Math.Max(0m, rawAmount - timeAmount);
            if (reduction > 0)
            {
                invoice.Items.Add(new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    SessionId = session.Id,
                    Description = "اعتبار زمانی رایگان",
                    Quantity = 1,
                    UnitPrice = -reduction,
                    Amount = -reduction
                });

                session.Customer.FreeTimeMinutes -= freeTimeMinutes;
                database.BenefitTransactions.Add(new BenefitTransaction
                {
                    CustomerId = session.CustomerId,
                    Type = BenefitTransactionType.FreeTimeDebit,
                    Minutes = freeTimeMinutes,
                    MoneyAmount = 0m,
                    Description = "مصرف اعتبار زمانی رایگان در پرداخت بعداً " + session.Station.Name,
                    ReferenceInvoiceId = invoice.Id
                });
            }
        }

        invoice.AppUserId ??= appUserId;
        invoice.SessionId = session.Id;
        invoice.AccountState = CustomerAccountState.PendingPayment;
        invoice.TotalAmount = Math.Max(0m, invoice.Items.Sum(item => item.Amount));

        session.EndAt ??= now;
        session.State = SessionState.Completed;
        session.Station.State = StationState.Available;

        if (session.CustomerLoginId.HasValue)
        {
            var login = await database.CustomerLogins
                .FirstOrDefaultAsync(item => item.Id == session.CustomerLoginId.Value && item.IsActive, cancellationToken);
            if (login is not null)
            {
                login.IsActive = false;
                login.LoggedOutAt = now;
            }
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = "SessionPaymentPending",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            AppUserId = appUserId,
            Details = "پرداخت بعداً · حساب مشتری " + session.Customer.FullName + " · مبلغ ناخالص فعلی " + invoice.TotalAmount.ToString("0.##") + " تومان"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var pending = await GetPendingAsync(cancellationToken);
        return pending.First(item => item.InvoiceId == invoice.Id);
    }

    public async Task<object> MarkPendingAsDebtAsync(
        Guid invoiceId,
        Guid? appUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await database.Invoices
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(
                item => item.Id == invoiceId
                    && item.Status == InvoiceStatus.Draft
                    && item.IsCustomerAccount
                    && item.AccountState == CustomerAccountState.PendingPayment,
                cancellationToken);

        if (invoice is null)
            throw new InvalidOperationException("حساب در انتظار پرداخت پیدا نشد یا قبلاً به بدهی منتقل شده است.");

        var paid = await database.InvoicePayments
            .Where(item => item.InvoiceId == invoice.Id)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var due = Math.Max(0m, invoice.TotalAmount - paid);
        invoice.AccountState = CustomerAccountState.Debt;
        invoice.AppUserId ??= appUserId;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "PendingAccountMarkedAsDebt",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            AppUserId = appUserId,
            Details = "انتقال حساب در انتظار پرداخت به بدهی مشتری · " + invoice.Customer.FullName + " · " + due.ToString("0.##") + " تومان"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new
        {
            invoiceId = invoice.Id,
            customerId = invoice.CustomerId,
            customerName = invoice.Customer.FullName,
            amountDue = due,
            accountState = invoice.AccountState.ToString()
        };
    }

    public async Task<SettlementResult> SettlePendingAsync(
        Guid invoiceId,
        PendingSettlementPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TotalAmount < 0)
            throw new ArgumentException("مبلغ تسویه نمی‌تواند منفی باشد.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await database.Invoices
            .Include(item => item.Items)
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(
                item => item.Id == invoiceId
                    && item.Status == InvoiceStatus.Draft
                    && item.IsCustomerAccount
                    && item.AccountState == CustomerAccountState.PendingPayment,
                cancellationToken);

        if (invoice is null)
            throw new InvalidOperationException("حساب مشتری در انتظار پرداخت پیدا نشد یا قبلاً بسته شده است.");

        var alreadyPaid = await database.InvoicePayments
            .Where(item => item.InvoiceId == invoice.Id)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var grossBeforeDiscount = Math.Max(0m, invoice.Items.Where(item => item.Amount > 0).Sum(item => item.Amount));
        var existingReductions = Math.Abs(Math.Min(0m, invoice.Items.Where(item => item.Amount < 0).Sum(item => item.Amount)));
        var currentDue = Math.Max(0m, invoice.TotalAmount - alreadyPaid);

        var discountAmount = Math.Max(0m, request.DiscountAmount ?? 0m);
        if (discountAmount > currentDue)
            throw new InvalidOperationException("مبلغ تخفیف نمی‌تواند از مبلغ قابل پرداخت بیشتر باشد.");

        if (request.AppUserId.HasValue && discountAmount > 0)
        {
            var settlementUser = await database.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.AppUserId.Value, cancellationToken);

            if (settlementUser is not null
                && string.Equals(settlementUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
            {
                var operatorDiscountPercent = 10;
                var storedSetting = await database.AppSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        item => item.ScopeKey == ServerSettingsCatalog.GlobalScope
                            && item.Key == "operatorDiscount",
                        cancellationToken);

                if (storedSetting is not null)
                {
                    try
                    {
                        using var settingJson = JsonDocument.Parse(storedSetting.ValueJson);
                        if (settingJson.RootElement.TryGetInt32(out var configuredPercent))
                            operatorDiscountPercent = Math.Clamp(configuredPercent, 0, 100);
                    }
                    catch (JsonException)
                    {
                        // Keep safe default.
                    }
                }

                var maxOperatorDiscount = Math.Round(
                    currentDue * operatorDiscountPercent / 100m,
                    2,
                    MidpointRounding.ToEven);

                if (discountAmount > maxOperatorDiscount + 0.01m)
                    throw new InvalidOperationException(
                        $"تخفیف اپراتور بیش از سقف مجاز {operatorDiscountPercent}% است.");
            }

            invoice.Items.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                Description = "تخفیف تسویه حساب مشتری",
                Quantity = 1,
                UnitPrice = -discountAmount,
                Amount = -discountAmount
            });
            invoice.TotalAmount = Math.Max(0m, invoice.TotalAmount - discountAmount);
            currentDue = Math.Max(0m, currentDue - discountAmount);
        }

        var parts = (request.Parts ?? Array.Empty<SettlementPart>())
            .Where(item => item.Amount > 0)
            .Select(item => new SettlementPart(item.Method.Trim().ToLowerInvariant(), item.Amount))
            .ToList();

        if (parts.Count > 0 && parts.Any(item => !AllowedMethods.Contains(item.Method)))
            throw new ArgumentException("روش پرداخت معتبر نیست.");

        if (currentDue > 0m && parts.Count == 0)
            throw new ArgumentException("حداقل یک روش پرداخت لازم است.");

        var partsTotal = parts.Sum(item => item.Amount);
        if (Math.Abs(partsTotal - currentDue) > 0.01m)
            throw new ArgumentException("جمع روش‌های پرداخت باید دقیقاً برابر مبلغ قابل پرداخت باشد.");

        var walletPart = parts.Where(item => item.Method == "wallet").Sum(item => item.Amount);
        var giftPart = parts.Where(item => item.Method == "gift").Sum(item => item.Amount);

        if (walletPart > invoice.Customer.Balance)
            throw new InvalidOperationException("موجودی کیف پول برای سهم انتخاب‌شده کافی نیست.");
        if (giftPart > invoice.Customer.FreeMoney)
            throw new InvalidOperationException("اعتبار مالی رایگان برای سهم انتخاب‌شده کافی نیست.");

        if (walletPart > 0)
        {
            invoice.Customer.Balance -= walletPart;
            database.WalletTransactions.Add(new WalletTransaction
            {
                CustomerId = invoice.CustomerId,
                Amount = walletPart,
                Type = WalletTransactionType.Debit,
                ReferenceInvoiceId = invoice.Id,
                Description = "تسویه حساب باز مشتری از کیف پول"
            });
        }

        if (giftPart > 0)
        {
            invoice.Customer.FreeMoney -= giftPart;
            database.BenefitTransactions.Add(new BenefitTransaction
            {
                CustomerId = invoice.CustomerId,
                Type = BenefitTransactionType.FreeMoneyDebit,
                Minutes = 0,
                MoneyAmount = giftPart,
                Description = "مصرف اعتبار مالی رایگان در تسویه حساب باز",
                ReferenceInvoiceId = invoice.Id
            });
        }

        foreach (var part in parts)
        {
            database.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                Method = part.Method,
                Amount = part.Amount
            });
        }

        var finalDue = currentDue;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTimeOffset.UtcNow;
        invoice.AppUserId ??= request.AppUserId;

        var latestSession = invoice.SessionId.HasValue
            ? await database.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == invoice.SessionId.Value, cancellationToken)
            : null;

        if (latestSession is not null)
            latestSession = latestSession.State == SessionState.Completed ? latestSession : null;

        database.AuditLogs.Add(new AuditLog
        {
            Action = "PendingSettlement",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            AppUserId = request.AppUserId,
            Details = "حساب مشتری " + invoice.Customer.FullName + " · " +
                      string.Join(" · ", parts.Select(item => item.Method + " " + item.Amount.ToString("0.##") + " تومان"))
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResult(
            invoice.Id,
            latestSession?.Id,
            finalDue,
            parts,
            invoice.Customer.Balance,
            invoice.Customer.FreeMoney,
            invoice.Customer.FreeTimeMinutes,
            invoice.Status.ToString(),
            invoice.PaidAt ?? DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<PendingSettlementDto>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var invoices = await database.Invoices
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Session)
                .ThenInclude(item => item.Station)
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
            .Include(item => item.Items)
                .ThenInclude(item => item.Session)
                    .ThenInclude(item => item!.Station)
            .Where(item => item.Status == InvoiceStatus.Draft
                && item.IsCustomerAccount
                && item.AccountState == CustomerAccountState.PendingPayment
                && !database.Sessions.Any(session =>
                    session.CustomerId == item.CustomerId
                    && session.State == SessionState.Active))
            .OrderBy(item => item.IssuedAt)
            .ToListAsync(cancellationToken);

        var ids = invoices.Select(item => item.Id).ToList();
        var charges = ids.Count == 0
            ? new List<SessionCharge>()
            : await database.SessionCharges
                .AsNoTracking()
                .Where(item => ids.Contains(item.InvoiceId))
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

        var chargeSessionIds = charges.Select(item => item.SessionId).Distinct().ToList();
        var chargeSessions = chargeSessionIds.Count == 0
            ? new List<Session>()
            : await database.Sessions
                .AsNoTracking()
                .Include(item => item.Station)
                .Where(item => chargeSessionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var paymentRows = ids.Count == 0
            ? new List<InvoicePayment>()
            : await database.InvoicePayments
                .AsNoTracking()
                .Where(item => ids.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var chargesByInvoice = charges
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var paymentsByInvoice = paymentRows
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var chargeSessionsById = chargeSessions.ToDictionary(item => item.Id);

        var now = DateTimeOffset.UtcNow;

        return invoices.Select(invoice =>
        {
            var chargeRows = chargesByInvoice.TryGetValue(invoice.Id, out var groupedCharges)
                ? groupedCharges
                : new List<SessionCharge>();

            var paymentRowsForInvoice = paymentsByInvoice.TryGetValue(invoice.Id, out var groupedPayments)
                ? groupedPayments
                : new List<InvoicePayment>();

            var sessions = new List<Session>();
            if (invoice.Session is not null)
                sessions.Add(invoice.Session);

            sessions.AddRange(
                invoice.Items
                    .Where(item => item.Session is not null)
                    .Select(item => item.Session!)
                    .GroupBy(item => item.Id)
                    .Select(group => group.First()));

            sessions.AddRange(
                chargeRows
                    .Select(item => chargeSessionsById.TryGetValue(item.SessionId, out var session) ? session : null)
                    .Where(item => item is not null)
                    .Select(item => item!));

            sessions = sessions
                .GroupBy(item => item.Id)
                .Select(group => group.First())
                .ToList();

            var buffetItems = invoice.Items
                .Where(item => item.ProductId.HasValue && item.Product is not null)
                .GroupBy(item => new { item.ProductId, Name = item.Product!.Name })
                .Select(group => new PendingSettlementBuffetItemDto(
                    group.Key.ProductId!.Value,
                    group.Key.Name,
                    group.Sum(item => item.Quantity),
                    group.Sum(item => item.Amount)))
                .OrderBy(item => item.ProductName)
                .ToList();

            var timeAmount = invoice.Items
                .Where(item =>
                    item.ProductId is null
                    && item.Amount > 0
                    && item.Description.StartsWith("هزینه جلسه", StringComparison.Ordinal))
                .Sum(item => item.Amount);

            var reduction = Math.Abs(invoice.Items
                .Where(item => item.ProductId is null && item.Amount < 0)
                .Sum(item => item.Amount));

            var otherAmount = invoice.Items
                .Where(item =>
                    item.Amount > 0
                    && (item.ProductId.HasValue
                        ? false
                        : !item.Description.StartsWith("هزینه جلسه", StringComparison.Ordinal)))
                .Sum(item => item.Amount);

            var grossAmount = Math.Max(0m, timeAmount + buffetItems.Sum(item => item.Amount) + otherAmount);
            var prepaidTotal = chargeRows.Sum(item => item.Amount);
            var prepaidApplied = Math.Min(prepaidTotal, Math.Max(0m, grossAmount - reduction));
            var prepaidRemaining = Math.Max(0m, prepaidTotal - prepaidApplied);
            var invoicePaid = paymentRowsForInvoice.Sum(item => item.Amount);
            var amountDue = Math.Max(0m, invoice.TotalAmount - invoicePaid);

            var closedAt = sessions
                .Select(item => item.EndAt ?? item.UpdatedAt ?? item.CreatedAt)
                .DefaultIfEmpty(invoice.CreatedAt)
                .Max();

            var waitingMinutes = Math.Max(0, (int)Math.Floor((now - closedAt).TotalMinutes));
            var stationNames = sessions
                .Select(item => item.Station?.Name)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .OrderBy(item => item, StringComparer.CurrentCulture)
                .ToList();

            var primarySessionId = sessions
                .OrderByDescending(item => item.EndAt ?? item.CreatedAt)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefault();

            return new PendingSettlementDto(
                invoice.Id,
                primarySessionId ?? Guid.Empty,
                invoice.CustomerId,
                invoice.Customer.FullName,
                invoice.Customer.Code,
                invoice.Customer.Username,
                stationNames.Count == 0 ? "بدون رایانه" : string.Join(" · ", stationNames),
                closedAt,
                waitingMinutes,
                timeAmount,
                buffetItems.Sum(item => item.Amount),
                otherAmount,
                grossAmount,
                prepaidTotal,
                prepaidApplied,
                prepaidRemaining,
                reduction,
                amountDue,
                chargeRows.Select(item => new PendingSettlementChargeDto(
                    item.Id,
                    item.Amount,
                    item.Method,
                    item.CreatedAt)).ToList(),
                buffetItems);
        }).ToList();
    }


}

public sealed record SettlementResult(
    Guid InvoiceId,
    Guid? SessionId,
    decimal TotalAmount,
    IReadOnlyList<SettlementPart> Parts,
    decimal WalletBalanceAfter,
    decimal FreeMoneyBalanceAfter,
    int FreeTimeMinutesAfter,
    string InvoiceStatus,
    DateTimeOffset PaidAt);

