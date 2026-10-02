using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class InvoiceReverseTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public InvoiceReverseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task ReverseRestoresWalletAndKeepsOriginalInvoice()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer { FullName = "Reverse Test", Balance = 50000m };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Paid,
            IssuedAt = DateTimeOffset.UtcNow,
            PaidAt = DateTimeOffset.UtcNow
        };
        db.Invoices.Add(invoice);
        db.WalletTransactions.Add(new WalletTransaction
        {
            Customer = customer,
            Amount = 100000m,
            Type = WalletTransactionType.Debit,
            ReferenceInvoiceId = invoice.Id,
            Description = "تسویه جلسه آزمایشی"
        });
        db.InvoicePayments.Add(new InvoicePayment { Invoice = invoice, Method = "wallet", Amount = 100000m });
        customer.Balance = 0m;
        await db.SaveChangesAsync();

        var service = new InvoiceReverseService(db);
        var result = await service.ReverseAsync(invoice.Id, new InvoiceReverseRequest(null, "آزمون برگشت"), CancellationToken.None);

        var savedInvoice = await db.Invoices.SingleAsync(item => item.Id == invoice.Id);
        var savedCustomer = await db.Customers.SingleAsync(item => item.Id == customer.Id);
        var reversal = await db.InvoiceReversals.SingleAsync(item => item.InvoiceId == invoice.Id);
        var restoration = await db.WalletTransactions.SingleAsync(item => item.ReferenceInvoiceId == invoice.Id && item.Type == WalletTransactionType.Credit);
        var audit = await db.AuditLogs.SingleAsync(item => item.Action == "InvoiceReverse");

        Assert.Equal(InvoiceStatus.Cancelled, savedInvoice.Status);
        Assert.Equal(100000m, savedCustomer.Balance);
        Assert.Equal(100000m, restoration.Amount);
        Assert.False(result.ExternalRefundRequired);
        Assert.Equal("آزمون برگشت", reversal.Reason);
        Assert.Equal("InvoiceReverse", audit.Action);
    }

    [Fact]
    public async Task ReverseMarksCashPaymentAsExternalRefund()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer { FullName = "Cash Reverse Test", Balance = 100000m };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Paid
        };
        db.Invoices.Add(invoice);
        db.InvoicePayments.Add(new InvoicePayment { Invoice = invoice, Method = "cash", Amount = 100000m });
        await db.SaveChangesAsync();

        var service = new InvoiceReverseService(db);
        var result = await service.ReverseAsync(invoice.Id, new InvoiceReverseRequest(null, "بازپرداخت نقدی"), CancellationToken.None);

        Assert.True(result.ExternalRefundRequired);
        Assert.Equal(InvoiceStatus.Cancelled, await db.Invoices.Select(item => item.Status).SingleAsync());
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ReverseRestoresBuffetStockAndFreeTime()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "Reverse-PC" };
        var tariff = new Tariff { Name = "Reverse-Tariff", HourlyRate = 95000m, DailyRate = 550000m };
        var customer = new Customer { FullName = "Reverse Buffet Test", FreeTimeMinutes = 0 };
        var station = new Station
        {
            Name = "PC-REVERSE-01",
            Zone = "PC",
            Type = "PC",
            State = StationState.Occupied,
            RatePerHour = 95000m,
            StationType = stationType,
            Tariff = tariff,
            IsActive = true
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            State = SessionState.Completed
        };
        var product = new Product
        {
            Name = "نوشابه",
            Category = "نوشیدنی",
            UnitPrice = 30000m,
            CostPrice = 12000m,
            StockQuantity = 0
        };
        var invoice = new Invoice
        {
            Customer = customer,
            SessionId = session.Id,
            TotalAmount = 60000m,
            Status = InvoiceStatus.Paid,
            Items =
            {
                new InvoiceItem
                {
                    Product = product,
                    Description = "نوشابه",
                    Quantity = 2,
                    UnitPrice = 30000m,
                    Amount = 60000m
                }
            }
        };

        db.AddRange(stationType, tariff, customer, station, session, product, invoice);
        await db.SaveChangesAsync();

        db.BenefitTransactions.Add(new BenefitTransaction
        {
            CustomerId = customer.Id,
            Type = BenefitTransactionType.FreeTimeDebit,
            Minutes = 30,
            MoneyAmount = 0m,
            ReferenceInvoiceId = invoice.Id,
            Description = "مصرف اعتبار زمانی آزمایشی"
        });
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = product.Id,
            Quantity = 2,
            UnitPrice = 30000m,
            UnitCost = 12000m,
            ReferenceInvoiceId = invoice.Id,
            Direction = TransactionDirection.Out,
            Kind = "Sale",
            Notes = "فروش آزمایشی"
        });
        await db.SaveChangesAsync();

        var service = new InvoiceReverseService(db);
        var result = await service.ReverseAsync(invoice.Id, new InvoiceReverseRequest(null, "برگشت بوفه"), CancellationToken.None);

        var savedProduct = await db.Products.SingleAsync(item => item.Id == product.Id);
        var savedCustomer = await db.Customers.SingleAsync(item => item.Id == customer.Id);
        var returnMovement = await db.InventoryTransactions.SingleAsync(item => item.ProductId == product.Id && item.Kind == "Return");

        Assert.Equal(2, result.InventoryRestored);
        Assert.Equal(30, result.FreeTimeRestored);
        Assert.Equal(2, savedProduct.StockQuantity);
        Assert.Equal(30, savedCustomer.FreeTimeMinutes);
        Assert.Equal(TransactionDirection.In, returnMovement.Direction);
        Assert.Equal(30000m, returnMovement.UnitPrice);
        Assert.Equal(12000m, returnMovement.UnitCost);
        Assert.Equal(invoice.Id, returnMovement.ReferenceInvoiceId);
    }

    public void Dispose() => _connection.Dispose();
}
