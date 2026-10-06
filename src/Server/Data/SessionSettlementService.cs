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
            .Where(item => item.SessionId == session.Id && item.Status == InvoiceStatus.Draft)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var buffetTotal = invoiceId.HasValue
            ? await database.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == invoiceId.Value && item.ProductId.HasValue)
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
        if (partsTotal != request.TotalAmount)
            throw new ArgumentException("جمع روش‌های پرداخت باید دقیقاً برابر مبلغ تسویه باشد.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent settlement attempts for the same Session on SQLite by
        // upgrading this transaction to a writer before reading the terminal state.
        var lockStamp = DateTimeOffset.UtcNow;
        var locked = await database.Sessions
            .Where(item => item.Id == sessionId
                && (item.State == SessionState.Active || item.State == SessionState.Ended))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.UpdatedAt, lockStamp), cancellationToken);

        if (locked != 1)
            throw new InvalidOperationException("این جلسه قبلاً بسته یا تسویه شده است.");

        // ExecuteUpdate changes UpdatedAt directly in the database, so any Session
        // entity already tracked by this scoped DbContext now carries a stale
        // concurrency token. Clear the tracker before reloading the authoritative
        // Session graph to make the terminal SaveChanges concurrency-safe.
        database.ChangeTracker.Clear();

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

        var grossBeforeDiscount = Math.Max(0m, existingBuffetTotal + timeAmount);
        if (discountAmount > grossBeforeDiscount)
            throw new InvalidOperationException("مبلغ تخفیف نمی‌تواند از مبلغ قبل از تخفیف بیشتر باشد.");

        if (request.AppUserId.HasValue)
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
                        // Keep the safe default of 10% if the stored value is malformed.
                    }
                }

                var maxOperatorDiscount = Math.Round(
                    grossBeforeDiscount * operatorDiscountPercent / 100m,
                    2,
                    MidpointRounding.ToEven);

                if (discountAmount > maxOperatorDiscount + 0.01m)
                    throw new InvalidOperationException(
                        $"تخفیف اپراتور بیش از سقف مجاز {operatorDiscountPercent}% است.");
            }
        }

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

        var invoice = await database.Invoices
            .FirstOrDefaultAsync(
                item => item.SessionId == session.Id && item.Status == InvoiceStatus.Draft,
                cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice
            {
                CustomerId = session.CustomerId,
                SessionId = session.Id,
                AppUserId = appUserId,
                TotalAmount = 0m,
                Status = InvoiceStatus.Draft,
                IssuedAt = DateTimeOffset.UtcNow
            };
            database.Invoices.Add(invoice);
            await database.SaveChangesAsync(cancellationToken);
        }

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
            throw new InvalidOperationException("جلسه فعال برای ثبت پرداخت بعداً پیدا نشد.");

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

        var invoice = await database.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(
                item => item.SessionId == session.Id && item.Status == InvoiceStatus.Draft,
                cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice
            {
                CustomerId = session.CustomerId,
                SessionId = session.Id,
                AppUserId = appUserId,
                TotalAmount = 0m,
                Status = InvoiceStatus.Draft,
                IssuedAt = now
            };
            database.Invoices.Add(invoice);
        }

        if (!invoice.Items.Any(item => item.ProductId is null && item.Description.StartsWith("هزینه جلسه", StringComparison.Ordinal)))
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = "هزینه جلسه " + session.Station.Name,
                Quantity = 1,
                UnitPrice = timeAmount,
                Amount = timeAmount
            });
        }

        var existingBenefitLine = invoice.Items
            .FirstOrDefault(item => item.ProductId is null && item.Description.StartsWith("اعتبار زمانی رایگان", StringComparison.Ordinal));

        if (freeTimeMinutes > 0 && existingBenefitLine is null)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = "اعتبار زمانی رایگان",
                Quantity = 1,
                UnitPrice = -SessionPricingService.CalculateTimeAmount(
                    session,
                    hourlyRate,
                    0,
                    now) + timeAmount,
                Amount = -Math.Max(0m, SessionPricingService.CalculateTimeAmount(
                    session,
                    hourlyRate,
                    0,
                    now) - timeAmount)
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

        var prepaidLine = invoice.Items
            .FirstOrDefault(item => item.ProductId is null && item.Description == "اعتبار شارژ جلسه");

        if (session.PrepaidAmount > 0 && prepaidLine is null)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = "اعتبار شارژ جلسه",
                Quantity = 1,
                UnitPrice = -session.PrepaidAmount,
                Amount = -session.PrepaidAmount
            });
        }

        invoice.AppUserId ??= appUserId;
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
            Details = "پرداخت بعداً · مبلغ فعلی " + invoice.TotalAmount.ToString("0.##") + " تومان"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var pending = await GetPendingAsync(cancellationToken);
        return pending.First(item => item.InvoiceId == invoice.Id);
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
            .Include(item => item.Session)
                .ThenInclude(item => item.Customer)
            .FirstOrDefaultAsync(
                item => item.Id == invoiceId && item.Status == InvoiceStatus.Draft,
                cancellationToken);

        if (invoice is null || invoice.Session is null || invoice.Session.State != SessionState.Completed)
            throw new InvalidOperationException("حساب پرداخت در انتظار پیدا نشد یا هنوز آماده تسویه نیست.");

        var discountAmount = Math.Max(0m, request.DiscountAmount ?? 0m);
        var currentTotal = Math.Max(0m, invoice.Items.Sum(item => item.Amount));
        if (discountAmount > currentTotal)
            throw new InvalidOperationException("مبلغ تخفیف نمی‌تواند از مبلغ قابل پرداخت بیشتر باشد.");

        if (discountAmount > 0)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = "تخفیف تسویه پرداخت در انتظار",
                Quantity = 1,
                UnitPrice = -discountAmount,
                Amount = -discountAmount
            });
        }

        var expectedTotal = Math.Max(0m, currentTotal - discountAmount);
        var parts = (request.Parts ?? Array.Empty<SettlementPart>())
            .Where(item => item.Amount > 0)
            .Select(item => new SettlementPart(item.Method.Trim().ToLowerInvariant(), item.Amount))
            .ToList();

        if (parts.Count > 0 && parts.Any(item => !AllowedMethods.Contains(item.Method)))
            throw new ArgumentException("روش پرداخت معتبر نیست.");

        var partsTotal = parts.Sum(item => item.Amount);
        if (expectedTotal > 0m && parts.Count == 0)
            throw new ArgumentException("حداقل یک روش پرداخت لازم است.");

        if (Math.Abs(partsTotal - expectedTotal) > 0.01m)
            throw new ArgumentException("جمع روش‌های پرداخت باید دقیقاً برابر مبلغ تسویه باشد.");

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
                Description = "تسویه حساب باز از کیف پول"
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

        invoice.TotalAmount = expectedTotal;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTimeOffset.UtcNow;
        invoice.AppUserId ??= request.AppUserId;
        invoice.Session!.TotalAmount = expectedTotal;

        foreach (var part in parts)
        {
            database.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                Method = part.Method,
                Amount = part.Amount
            });
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = "PendingSettlement",
            EntityName = "Invoice",
            EntityId = invoice.Id.ToString(),
            AppUserId = request.AppUserId,
            Details = string.Join(" · ", parts.Select(item => item.Method + " " + item.Amount.ToString("0.##") + " تومان"))
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SettlementResult(
            invoice.Id,
            invoice.Session.Id,
            expectedTotal,
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
            .Where(item =>
                item.Status == InvoiceStatus.Draft
                && item.SessionId.HasValue
                && item.Session != null
                && item.Session.State == SessionState.Completed)
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

        var chargesByInvoice = charges
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var now = DateTimeOffset.UtcNow;
        return invoices.Select(invoice =>
        {
            var chargeRows = chargesByInvoice.TryGetValue(invoice.Id, out var groupedCharges)
                ? groupedCharges
                : new List<SessionCharge>();

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
                .Where(item => item.ProductId is null && item.Amount < 0 && item.Description != "اعتبار شارژ جلسه")
                .Sum(item => item.Amount));

            var grossAmount = Math.Max(0m, timeAmount + buffetItems.Sum(item => item.Amount));
            var prepaidTotal = chargeRows.Sum(item => item.Amount);
            var prepaidApplied = Math.Min(prepaidTotal, Math.Max(0m, grossAmount - reduction));
            var prepaidRemaining = Math.Max(0m, prepaidTotal - prepaidApplied);
            var amountDue = Math.Max(0m, invoice.TotalAmount);
            var closedAt = invoice.Session!.EndAt ?? invoice.PaidAt ?? invoice.CreatedAt;
            var waitingMinutes = Math.Max(0, (int)Math.Floor((now - closedAt).TotalMinutes));

            return new PendingSettlementDto(
                invoice.Id,
                invoice.Session.Id,
                invoice.CustomerId,
                invoice.Customer.FullName,
                invoice.Customer.Code,
                invoice.Customer.Username,
                invoice.Session.Station.Name,
                closedAt,
                waitingMinutes,
                timeAmount,
                buffetItems.Sum(item => item.Amount),
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
    Guid SessionId,
    decimal TotalAmount,
    IReadOnlyList<SettlementPart> Parts,
    decimal WalletBalanceAfter,
    decimal FreeMoneyBalanceAfter,
    int FreeTimeMinutesAfter,
    string InvoiceStatus,
    DateTimeOffset PaidAt);

