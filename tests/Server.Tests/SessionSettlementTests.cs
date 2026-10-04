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
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            AppUser = user,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-60),
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

    public void Dispose() => _connection.Dispose();
}
