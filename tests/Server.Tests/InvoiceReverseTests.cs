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

    public void Dispose() => _connection.Dispose();
}
