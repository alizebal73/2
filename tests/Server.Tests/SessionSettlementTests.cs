using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class SessionSettlementTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SessionSettlementTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task SettlementAtomicallyCreatesPaidInvoiceAndWalletDebit()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC" };
        var tariff = new Tariff { Name = "Normal", HourlyRate = 95000m, DailyRate = 550000m };
        var customer = new Customer { FullName = "Settlement Test", Balance = 200000m };
        var user = new AppUser
        {
            FullName = "Operator",
            UserName = "operator",
            Email = "operator@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var station = new Station
        {
            Name = "PC-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 95000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var sessionStart = DateTimeOffset.UtcNow.AddMinutes(-60);
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            AppUser = user,
            StartAt = sessionStart,
            EndAt = sessionStart.AddMinutes(60),
            State = SessionState.Active,
            HourlyRateSnapshot = 150000m
        };

        db.AddRange(type, tariff, customer, user, station, session);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        var result = await service.SettleAsync(
            session.Id,
            new SessionSettlementRequest(
                150000m,
                new[]
                {
                    new SettlementPart("cash", 50000m),
                    new SettlementPart("wallet", 100000m)
                },
                user.Id),
            CancellationToken.None);

        var savedSession = await db.Sessions.SingleAsync(item => item.Id == session.Id);
        var invoice = await db.Invoices
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == result.InvoiceId);
        var walletDebit = await db.WalletTransactions.SingleAsync();
        var audit = await db.AuditLogs.SingleAsync(item => item.Action == "SessionSettlement");
        var savedCustomer = await db.Customers.SingleAsync(item => item.Id == customer.Id);

        Assert.Equal(SessionState.Completed, savedSession.State);
        Assert.Equal(150000m, savedSession.TotalAmount);
        Assert.Equal(150000m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Single(invoice.Items);
        Assert.Equal(100000m, walletDebit.Amount);
        Assert.Equal(WalletTransactionType.Debit, walletDebit.Type);
        Assert.Equal(100000m, savedCustomer.Balance);
        Assert.Equal("SessionSettlement", audit.Action);
    }

    [Fact]
    public async Task OperatorCannotExceedConfiguredDiscountLimit()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-Discount" };
        var tariff = new Tariff { Name = "Discount", HourlyRate = 100000m, DailyRate = 500000m };
        var customer = new Customer { FullName = "Discount Test", Balance = 200000m };
        var user = new AppUser
        {
            FullName = "Operator",
            UserName = "discount-operator",
            Email = "discount-operator@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var station = new Station
        {
            Name = "PC-DISCOUNT-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 100000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            AppUser = user,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Active,
            HourlyRateSnapshot = 100000m
        };

        db.AddRange(type, tariff, customer, user, station, session);
        db.AppSettings.Add(new AppSetting
        {
            Key = "operatorDiscount",
            ScopeKey = ServerSettingsCatalog.GlobalScope,
            ValueJson = "10"
        });
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SettleAsync(
                session.Id,
                new SessionSettlementRequest(
                    80000m,
                    new[] { new SettlementPart("cash", 80000m) },
                    user.Id,
                    0,
                    100000m,
                    20000m,
                    0m),
                CancellationToken.None));

        var savedSession = await db.Sessions.SingleAsync(item => item.Id == session.Id);
        Assert.Equal(SessionState.Active, savedSession.State);
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task InvalidSplitTotalDoesNotChangeSessionOrWallet()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC" };
        var tariff = new Tariff { Name = "Normal", HourlyRate = 95000m, DailyRate = 550000m };
        var customer = new Customer { FullName = "Invalid Split Test", Balance = 200000m };
        var station = new Station
        {
            Name = "PC-02",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 95000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            State = SessionState.Active
        };

        db.AddRange(type, tariff, customer, station, session);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SettleAsync(
                session.Id,
                new SessionSettlementRequest(
                    150000m,
                    new[] { new SettlementPart("wallet", 100000m) },
                    null),
                CancellationToken.None));

        var savedSession = await db.Sessions.SingleAsync(item => item.Id == session.Id);
        var savedCustomer = await db.Customers.SingleAsync(item => item.Id == customer.Id);

        Assert.Equal(SessionState.Active, savedSession.State);
        Assert.Equal(0m, savedSession.TotalAmount);
        Assert.Equal(200000m, savedCustomer.Balance);
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task SettlementReusesDraftBuffetInvoiceAndKeepsProductLine()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-Buffet" };
        var tariff = new Tariff { Name = "Normal-Buffet", HourlyRate = 95000m, DailyRate = 550000m };
        var customer = new Customer { FullName = "Buffet Settlement Test", Balance = 200000m };
        var station = new Station
        {
            Name = "PC-BUFFET-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 95000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var product = new Product
        {
            Name = "آب",
            Category = "نوشیدنی",
            UnitPrice = 50000m,
            CostPrice = 20000m,
            StockQuantity = 2
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            EndAt = DateTimeOffset.UtcNow,
            State = SessionState.Active,
            HourlyRateSnapshot = 100000m
        };
        var draft = new Invoice
        {
            Customer = customer,
            SessionId = session.Id,
            TotalAmount = 50000m,
            Status = InvoiceStatus.Draft,
            Items =
            {
                new InvoiceItem
                {
                    Product = product,
                    Description = "آب",
                    Quantity = 1,
                    UnitPrice = 50000m,
                    Amount = 50000m
                }
            }
        };

        db.AddRange(type, tariff, customer, station, product, session, draft);
        await db.SaveChangesAsync();

        await db.DisposeAsync();

        await using var settlementDb = new GameNetDbContext(options);
        var service = new SessionSettlementService(settlementDb, new SessionPricingService(settlementDb));
        var result = await service.SettleAsync(
            session.Id,
            new SessionSettlementRequest(
                150000m,
                new[] { new SettlementPart("cash", 150000m) },
                null,
                0,
                100000m,
                0m,
                0m),
            CancellationToken.None);

        var invoice = await settlementDb.Invoices
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == draft.Id);

        Assert.Equal(draft.Id, result.InvoiceId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(150000m, invoice.TotalAmount);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Contains(invoice.Items, item => item.ProductId == product.Id && item.Amount == 50000m);
        Assert.Contains(invoice.Items, item => item.ProductId == null && item.Amount == 100000m);
    }


    [Fact]
    public async Task PendingPaymentConsolidatesChargesAndKeepsInvoiceOpenUntilFinalSettlement()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-Pending" };
        var tariff = new Tariff { Name = "Pending", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Pending Test", Balance = 100000m, FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "Operator",
            UserName = "pending-operator",
            Email = "pending@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var station = new Station
        {
            Name = "PC-PENDING-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 60000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };

        db.AddRange(type, tariff, customer, user, station, session);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        await service.ChargeAsync(session.Id, 90000m, "cash", user.Id, CancellationToken.None);
        await service.ChargeAsync(session.Id, 20000m, "card", user.Id, CancellationToken.None);
        await service.ChargeAsync(session.Id, 50000m, "cash", user.Id, CancellationToken.None);

        var pending = await service.PreparePendingAsync(session.Id, 0, user.Id, CancellationToken.None);
        Assert.Equal(160000m, pending.PrepaidTotal);
        Assert.Equal(3, pending.Charges.Count);
        Assert.Equal(SessionState.Completed, await db.Sessions.Where(item => item.Id == session.Id).Select(item => item.State).SingleAsync());
        Assert.Equal(InvoiceStatus.Draft, await db.Invoices.Where(item => item.Id == pending.InvoiceId).Select(item => item.Status).SingleAsync());

        var settled = await service.SettlePendingAsync(
            pending.InvoiceId,
            new PendingSettlementPaymentRequest(
                pending.AmountDue,
                new[] { new SettlementPart("cash", pending.AmountDue) },
                user.Id),
            CancellationToken.None);

        Assert.Equal(InvoiceStatus.Paid.ToString(), settled.InvoiceStatus);
        Assert.Empty(await service.GetPendingAsync(CancellationToken.None));
    }



    [Fact]
    public async Task SettlingOneSessionDoesNotCloseAccountForAnotherActiveSession()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type1 = new StationType { Name = "PC-ALLOC-01" };
        var type2 = new StationType { Name = "PC-ALLOC-02" };
        var tariff = new Tariff { Name = "Allocation", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "مشتری تخصیص پرداخت" };
        var user = new AppUser
        {
            FullName = "مدیر تخصیص",
            UserName = "allocation-admin",
            Email = "allocation-admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station1 = new Station
        {
            Name = "PC-ALLOC-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 60000m,
            StationType = type1,
            Tariff = tariff,
            IsActive = true
        };
        var station2 = new Station
        {
            Name = "PC-ALLOC-02",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 60000m,
            StationType = type2,
            Tariff = tariff,
            IsActive = true
        };
        var session1 = new Session
        {
            Customer = customer,
            Station = station1,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var session2 = new Session
        {
            Customer = customer,
            Station = station2,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };

        db.AddRange(type1, type2, tariff, customer, user, station1, station2, session1, session2);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        await service.ChargeAsync(session1.Id, 10000m, "cash", user.Id, CancellationToken.None);
        await service.ChargeAsync(session2.Id, 5000m, "cash", user.Id, CancellationToken.None);

        var preview = await service.PreviewAsync(session1.Id, 0, 0, 10000m, CancellationToken.None);
        var result = await service.SettleAsync(
            session1.Id,
            new SessionSettlementRequest(
                preview.TotalAmount,
                new[] { new SettlementPart("cash", preview.TotalAmount) },
                user.Id,
                0,
                preview.TimeAmount,
                0,
                10000m),
            CancellationToken.None);

        Assert.Equal("Draft", result.InvoiceStatus);
        Assert.NotEqual(Guid.Empty, result.InvoiceId);

        var account = await db.Invoices.SingleAsync(item => item.Id == result.InvoiceId);
        Assert.True(account.IsCustomerAccount);
        Assert.Equal(5000m, await db.InvoicePayments
            .Where(item => item.InvoiceId == account.Id && item.SessionId == session2.Id)
            .SumAsync(item => item.Amount));
        Assert.Equal(30000m, await db.InvoicePayments
            .Where(item => item.InvoiceId == account.Id && item.SessionId == session1.Id)
            .SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task TwoSessionsForSameCustomerCollapseIntoOnePendingAccount()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type1 = new StationType { Name = "PC-MULTI-01" };
        var type2 = new StationType { Name = "PC-MULTI-02" };
        var tariff = new Tariff { Name = "Multi Pending", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "مشتری چند دستگاه", FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "مدیر تست",
            UserName = "multi-pending-admin",
            Email = "multi-pending-admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station1 = new Station
        {
            Name = "PC-MULTI-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 60000m,
            StationType = type1,
            Tariff = tariff,
            IsActive = true
        };
        var station2 = new Station
        {
            Name = "PC-MULTI-02",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 60000m,
            StationType = type2,
            Tariff = tariff,
            IsActive = true
        };

        var session1 = new Session
        {
            Customer = customer,
            Station = station1,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var session2 = new Session
        {
            Customer = customer,
            Station = station2,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };

        db.AddRange(type1, type2, tariff, customer, user, station1, station2, session1, session2);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        await service.ChargeAsync(session1.Id, 60000m, "cash", user.Id, CancellationToken.None);

        var pending1 = await service.PreparePendingAsync(session1.Id, 0, user.Id, CancellationToken.None);

        var session2Invoice = new Invoice
        {
            CustomerId = customer.Id,
            SessionId = session2.Id,
            AppUserId = user.Id,
            TotalAmount = 25000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = false,
            IssuedAt = DateTimeOffset.UtcNow,
            Items =
            {
                new InvoiceItem
                {
                    SessionId = session2.Id,
                    Description = "هزینه جلسه PC-MULTI-02",
                    Quantity = 1,
                    UnitPrice = 25000m,
                    Amount = 25000m
                }
            }
        };
        db.Invoices.Add(session2Invoice);
        await db.SaveChangesAsync();

        var pending2 = await service.PreparePendingAsync(session2.Id, 0, user.Id, CancellationToken.None);
        var pending = await service.GetPendingAsync(CancellationToken.None);

        Assert.Equal(pending1.InvoiceId, pending2.InvoiceId);
        Assert.Single(pending);
        Assert.Contains("PC-MULTI-01", pending[0].StationName);
        Assert.Contains("PC-MULTI-02", pending[0].StationName);
        Assert.Equal(1, pending[0].Charges.Count);
        Assert.Equal(75000m, pending[0].GrossAmount);
        Assert.Equal(15000m, pending[0].AmountDue);

        var account = await db.Invoices.SingleAsync(item => item.Id == pending[0].InvoiceId);
        Assert.True(account.IsCustomerAccount);
        Assert.Equal(3, await db.InvoiceItems.CountAsync(item => item.InvoiceId == account.Id));
    }

    [Fact]
    public async Task OperatorCannotExceedConfiguredDiscountLimitOnPendingSettlement()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-Pending-Discount" };
        var tariff = new Tariff { Name = "Pending-Discount", HourlyRate = 100000m, DailyRate = 500000m };
        var customer = new Customer { FullName = "Pending Discount Test" };
        var user = new AppUser
        {
            FullName = "Operator",
            UserName = "pending-discount-operator",
            Email = "pending-discount-operator@test.local",
            PasswordHash = "hash",
            Role = "Operator"
        };
        var station = new Station
        {
            Name = "PC-PENDING-DISCOUNT-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Available,
            RatePerHour = 100000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            EndAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            State = SessionState.Completed,
            HourlyRateSnapshot = 100000m
        };
        var invoice = new Invoice
        {
            Customer = customer,
            Session = session,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Items =
            {
                new InvoiceItem
                {
                    Description = "هزینه جلسه PC-PENDING-DISCOUNT-01",
                    Quantity = 1,
                    UnitPrice = 100000m,
                    Amount = 100000m
                }
            }
        };

        db.AddRange(type, tariff, customer, user, station, session, invoice);
        db.AppSettings.Add(new AppSetting
        {
            Key = "operatorDiscount",
            ScopeKey = ServerSettingsCatalog.GlobalScope,
            ValueJson = "10"
        });
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SettlePendingAsync(
                invoice.Id,
                new PendingSettlementPaymentRequest(
                    80000m,
                    new[] { new SettlementPart("cash", 80000m) },
                    user.Id,
                    20000m),
                CancellationToken.None));

        var savedInvoice = await db.Invoices.SingleAsync(item => item.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Draft, savedInvoice.Status);
        Assert.Equal(100000m, savedInvoice.TotalAmount);
    }

    [Fact]
    public async Task ReparentingLegacySessionInvoicePreservesPaymentSessionAllocation()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-LEGACY-PAYMENT" };
        var tariff = new Tariff { Name = "Legacy Payment", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Legacy Payment Allocation", FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "Legacy Admin",
            UserName = "legacy-payment-admin",
            Email = "legacy-payment-admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station = new Station
        {
            Name = "PC-LEGACY-PAYMENT-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var legacyInvoice = new Invoice
        {
            Customer = customer,
            Session = session,
            TotalAmount = 40000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = false,
            Items =
            {
                new InvoiceItem
                {
                    Session = session,
                    Description = "هزینه جلسه قدیمی",
                    Quantity = 1,
                    UnitPrice = 30000m,
                    Amount = 30000m
                },
                new InvoiceItem
                {
                    Session = session,
                    Description = "بوفه قدیمی",
                    Quantity = 1,
                    UnitPrice = 10000m,
                    Amount = 10000m
                }
            }
        };

        db.AddRange(type, tariff, customer, user, station, session, legacyInvoice);
        await db.SaveChangesAsync();

        db.InvoicePayments.Add(new InvoicePayment
        {
            InvoiceId = legacyInvoice.Id,
            SessionId = null,
            Method = "cash",
            Amount = 10000m
        });
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        await service.PreparePendingAsync(session.Id, 0, user.Id, CancellationToken.None);

        var payment = await db.InvoicePayments.SingleAsync();
        var account = await db.Invoices.SingleAsync(item => item.IsCustomerAccount && item.CustomerId == customer.Id);

        Assert.Equal(account.Id, payment.InvoiceId);
        Assert.Equal(session.Id, payment.SessionId);
    }

    [Fact]
    public async Task SettlementPreviewUsesOnlySelectedSessionBuffetWithinCustomerAccount()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type1 = new StationType { Name = "PC-PREVIEW-01" };
        var type2 = new StationType { Name = "PC-PREVIEW-02" };
        var tariff = new Tariff { Name = "Preview Allocation", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Preview Allocation Customer", FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "Preview Admin",
            UserName = "preview-allocation-admin",
            Email = "preview-allocation-admin@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station1 = new Station
        {
            Name = "PC-PREVIEW-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type1,
            Tariff = tariff,
            IsActive = true
        };
        var station2 = new Station
        {
            Name = "PC-PREVIEW-02",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type2,
            Tariff = tariff,
            IsActive = true
        };
        var session1 = new Session
        {
            Customer = customer,
            Station = station1,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var session2 = new Session
        {
            Customer = customer,
            Station = station2,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var product1 = new Product { Name = "Preview Cola 1", UnitPrice = 35000m, CostPrice = 10000m, StockQuantity = 10 };
        var product2 = new Product { Name = "Preview Cola 2", UnitPrice = 45000m, CostPrice = 12000m, StockQuantity = 10 };

        db.AddRange(type1, type2, tariff, customer, user, station1, station2, session1, session2, product1, product2);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        await service.ChargeAsync(session1.Id, 10000m, "cash", user.Id, CancellationToken.None);
        await service.ChargeAsync(session2.Id, 5000m, "cash", user.Id, CancellationToken.None);

        var account = await db.Invoices.SingleAsync(item =>
            item.CustomerId == customer.Id
            && item.Status == InvoiceStatus.Draft
            && item.IsCustomerAccount);

        db.InvoiceItems.AddRange(
            new InvoiceItem
            {
                InvoiceId = account.Id,
                SessionId = session1.Id,
                ProductId = product1.Id,
                Description = product1.Name,
                Quantity = 1,
                UnitPrice = product1.UnitPrice,
                Amount = product1.UnitPrice
            },
            new InvoiceItem
            {
                InvoiceId = account.Id,
                SessionId = session2.Id,
                ProductId = product2.Id,
                Description = product2.Name,
                Quantity = 1,
                UnitPrice = product2.UnitPrice,
                Amount = product2.UnitPrice
            });

        await db.SaveChangesAsync();

        var preview = await service.PreviewAsync(session1.Id, 0, 0m, 0m, CancellationToken.None);

        Assert.Equal(product1.UnitPrice, preview.BuffetTotal);
        Assert.NotEqual(product2.UnitPrice, preview.BuffetTotal);
        Assert.Equal(
            preview.TimeAmount + product1.UnitPrice,
            preview.TotalAmount);
    }

    [Fact]
    public async Task SettlementPreviewIgnoresDraftDebtAccount()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-PREVIEW-DEBT" };
        var tariff = new Tariff { Name = "Preview Debt", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Preview Debt Customer", FreeTimeMinutes = 0 };
        var station = new Station
        {
            Name = "PC-PREVIEW-DEBT-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var product = new Product
        {
            Name = "Debt Buffet Item",
            Category = "Test",
            UnitPrice = 90000m,
            CostPrice = 30000m,
            StockQuantity = 10
        };
        var debtAccount = new Invoice
        {
            Customer = customer,
            TotalAmount = 90000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.Debt,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items =
            {
                new InvoiceItem
                {
                    Session = session,
                    Product = product,
                    Description = product.Name,
                    Quantity = 1,
                    UnitPrice = product.UnitPrice,
                    Amount = product.UnitPrice
                }
            }
        };

        db.AddRange(type, tariff, customer, station, session, product, debtAccount);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        var preview = await service.PreviewAsync(session.Id, 0, 0m, 0m, CancellationToken.None);

        Assert.Equal(0m, preview.BuffetTotal);
        Assert.Equal(preview.TimeAmount, preview.TotalAmount);
    }

    [Fact]
    public async Task PendingAccountCannotBeMarkedAsDebtWhileCustomerHasActiveSession()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-DEBT-GUARD" };
        var tariff = new Tariff { Name = "Debt Guard", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Debt Guard Customer", FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "Debt Guard Admin",
            UserName = "debt-guard-admin",
            Email = "debt-guard@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station = new Station
        {
            Name = "PC-DEBT-GUARD-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var invoice = new Invoice
        {
            Customer = customer,
            Session = session,
            TotalAmount = 50000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment,
            IssuedAt = DateTimeOffset.UtcNow,
            Items =
            {
                new InvoiceItem
                {
                    Session = session,
                    Description = "هزینه جلسه Debt Guard",
                    Quantity = 1,
                    UnitPrice = 50000m,
                    Amount = 50000m
                }
            }
        };

        db.AddRange(type, tariff, customer, user, station, session, invoice);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        var guardError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnsurePendingAccountCanBecomeDebtAsync(customer.Id, CancellationToken.None));

        Assert.Equal("تا وقتی جلسه فعالی برای این مشتری وجود دارد، حساب را نمی‌توان به بدهی منتقل کرد.", guardError.Message);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkPendingAsDebtAsync(invoice.Id, user.Id, CancellationToken.None));

        Assert.Equal("تا وقتی جلسه فعالی برای این مشتری وجود دارد، حساب را نمی‌توان به بدهی منتقل کرد.", error.Message);
        var saved = await db.Invoices.SingleAsync(item => item.Id == invoice.Id);
        Assert.Equal(CustomerAccountState.PendingPayment, saved.AccountState);
        Assert.Equal(InvoiceStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task PendingAccountBuffetSaleIsIncludedOnceInPendingTotal()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer
        {
            FullName = "Pending Buffet Customer",
            FreeTimeMinutes = 0
        };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment,
            IssuedAt = DateTimeOffset.UtcNow,
            Items =
            {
                new InvoiceItem
                {
                    Description = "هزینه جلسه Pending Buffet",
                    Quantity = 1,
                    UnitPrice = 100000m,
                    Amount = 100000m
                }
            }
        };
        var product = new Product
        {
            Name = "Pending Cola",
            UnitPrice = 30000m,
            CostPrice = 10000m,
            StockQuantity = 10,
            ShowcaseStockQuantity = 10,
            IsActive = true
        };

        db.AddRange(customer, invoice, product);
        db.InvoicePayments.Add(new InvoicePayment
        {
            InvoiceId = invoice.Id,
            Method = "cash",
            Amount = 60000m
        });
        await db.SaveChangesAsync();

        invoice.Items.Add(new InvoiceItem
        {
            InvoiceId = invoice.Id,
            ProductId = product.Id,
            Description = product.Name,
            Quantity = 1,
            UnitPrice = product.UnitPrice,
            Amount = product.UnitPrice
        });
        invoice.TotalAmount += product.UnitPrice;
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        var pending = await service.GetPendingAsync(CancellationToken.None);

        var row = Assert.Single(pending);
        Assert.Equal(invoice.Id, row.InvoiceId);
        Assert.Equal(30000m, row.BuffetTotal);
        Assert.Equal(130000m, row.GrossAmount);
        Assert.Equal(70000m, row.AmountDue);
        Assert.Single(row.BuffetItems);
        Assert.Equal(product.Id, row.BuffetItems[0].ProductId);
    }

    [Fact]
    public async Task ActiveSessionChargeDoesNotAttachToExistingCustomerDebtAccount()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "PC-DEBT-SEPARATION" };
        var tariff = new Tariff { Name = "Debt Separation", HourlyRate = 60000m, DailyRate = 300000m };
        var customer = new Customer { FullName = "Debt Separation Customer", FreeTimeMinutes = 0 };
        var user = new AppUser
        {
            FullName = "Debt Separation Admin",
            UserName = "debt-separation-admin",
            Email = "debt-separation@test.local",
            PasswordHash = "hash",
            Role = "Admin"
        };
        var station = new Station
        {
            Name = "PC-DEBT-SEPARATION-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 60000m,
            StationType = type,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            State = SessionState.Active,
            HourlyRateSnapshot = 60000m
        };
        var debt = new Invoice
        {
            Customer = customer,
            TotalAmount = 50000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.Debt,
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-1),
            Items =
            {
                new InvoiceItem
                {
                    Description = "بدهی قبلی",
                    Quantity = 1,
                    UnitPrice = 50000m,
                    Amount = 50000m
                }
            }
        };

        db.AddRange(type, tariff, customer, user, station, session, debt);
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));
        var charge = await service.ChargeAsync(
            session.Id,
            30000m,
            "cash",
            user.Id,
            CancellationToken.None);

        Assert.NotEqual(debt.Id, charge.InvoiceId);

        var accounts = await db.Invoices
            .Where(item => item.CustomerId == customer.Id && item.Status == InvoiceStatus.Draft && item.IsCustomerAccount)
            .ToListAsync();

        Assert.Equal(2, accounts.Count);
        Assert.Single(accounts.Where(item => item.AccountState == CustomerAccountState.Debt));
        Assert.Single(accounts.Where(item => item.AccountState == CustomerAccountState.PendingPayment));

        var savedDebt = accounts.Single(item => item.AccountState == CustomerAccountState.Debt);
        Assert.Equal(50000m, savedDebt.TotalAmount);

        var pendingAccount = accounts.Single(item => item.AccountState == CustomerAccountState.PendingPayment);
        Assert.Equal(pendingAccount.Id, charge.InvoiceId);
        Assert.Equal(30000m, pendingAccount.TotalAmount);
    }

    [Fact]
    public async Task FullyPaidPendingAccountCannotBecomeDebtAndIsHiddenFromPending()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer
        {
            FullName = "Zero Due Pending Customer",
            FreeTimeMinutes = 0
        };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment,
            IssuedAt = DateTimeOffset.UtcNow,
            Items =
            {
                new InvoiceItem
                {
                    Description = "هزینه جلسه تسویه‌شده",
                    Quantity = 1,
                    UnitPrice = 100000m,
                    Amount = 100000m
                }
            }
        };

        db.AddRange(customer, invoice);
        db.InvoicePayments.Add(new InvoicePayment
        {
            InvoiceId = invoice.Id,
            Method = "cash",
            Amount = 100000m
        });
        await db.SaveChangesAsync();

        var service = new SessionSettlementService(db, new SessionPricingService(db));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkPendingAsDebtAsync(invoice.Id, null, CancellationToken.None));

        Assert.Equal("این حساب مبلغ بدهی قابل انتقال ندارد.", error.Message);
        Assert.Equal(CustomerAccountState.PendingPayment,
            await db.Invoices.Where(item => item.Id == invoice.Id).Select(item => item.AccountState).SingleAsync());

        var pending = await service.GetPendingAsync(CancellationToken.None);
        Assert.Empty(pending);
    }

    public void Dispose() => _connection.Dispose();
}
