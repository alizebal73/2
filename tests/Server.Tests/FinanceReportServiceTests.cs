using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class FinanceReportServiceTests
{
    [Fact]
    public async Task FinanceReportsUseActualPaymentTimestampAndAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer { FullName = "مشتری Finance", Code = "FIN01" };
        var invoice = new Invoice
        {
            Customer = customer,
            TotalAmount = 150000m,
            Status = InvoiceStatus.Draft,
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment,
            IssuedAt = now.AddDays(-2)
        };
        invoice.Items.Add(new InvoiceItem
        {
            Invoice = invoice,
            Description = "فاکتور Finance",
            Quantity = 1,
            UnitPrice = 150000m,
            Amount = 150000m
        });

        database.Add(invoice);
        var inRangePayment = new InvoicePayment
        {
            Invoice = invoice,
            Method = "cash",
            Amount = 30000m,
            CreatedAt = now.AddHours(-2)
        };
        database.InvoicePayments.AddRange(
            inRangePayment,
            new InvoicePayment
            {
                Invoice = invoice,
                Method = "card",
                Amount = 20000m,
                CreatedAt = now.AddDays(-3)
            });
        database.Expenses.Add(new Expense
        {
            Shift = new Shift
            {
                OpenAt = now.AddHours(-3),
                CashOpening = 0m
            },
            Category = "test",
            Amount = 5000m,
            Description = "هزینه تست Finance",
            CreatedAt = now.AddHours(-1)
        });

        await database.SaveChangesAsync();

        var service = new FinanceReportService(database);
        var start = now.AddHours(-4);
        var end = now;

        var summary = await service.GetSummaryAsync(start, end, CancellationToken.None);
        var transactions = await service.GetTransactionsAsync(start, end, CancellationToken.None);

        Assert.Equal(30000m, summary.Revenue);
        Assert.Equal(5000m, summary.Expense);
        Assert.Equal(25000m, summary.OperatingProfit);

        var row = Assert.Single(transactions);
        Assert.Equal(30000m, row.Amount);
        Assert.Equal("cash", row.Method);
        Assert.Equal(inRangePayment.Id, row.Id);
        Assert.Equal(InvoiceStatus.Draft.ToString(), row.Status);
        Assert.Equal(invoice.Items.Single().Description, row.Description);
    }
}
