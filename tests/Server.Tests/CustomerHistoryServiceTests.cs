using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class CustomerHistoryServiceTests
{
    [Fact]
    public async Task CustomerHistoryIncludesInvoicePayments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { FullName = "مشتری History", Code = "HIST01" };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 90000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment,
            IssuedAt = now.AddHours(-2)
        };
        database.Add(invoice);
        var payment = new InvoicePayment
        {
            Invoice = invoice,
            Method = "card",
            Amount = 40000m,
            CreatedAt = now.AddMinutes(-20)
        };
        database.InvoicePayments.Add(payment);
        await database.SaveChangesAsync();

        var service = new CustomerHistoryService(database);
        var history = await service.GetAsync(customer.Id, CancellationToken.None);

        var row = Assert.Single(history.Where(item => item.Type == "payment"));
        Assert.Equal(payment.Id, row.Id);
        Assert.Equal(40000m, row.Amount);
        Assert.Equal("پرداخت card", row.Description);
        Assert.Equal(invoice.Id, row.ReferenceId);
        Assert.Equal(payment.CreatedAt, row.CreatedAt);
    }
}
