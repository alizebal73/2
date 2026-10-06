using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class SessionReportServiceTests
{
    [Fact]
    public async Task QueryFiltersByZoneCustomerAndStateAndBuildsSummary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "PC-Test" };
        var pc = new Station
        {
            Name = "PC TEST 01",
            Zone = "pc",
            Type = "PC",
            RatePerHour = 100000,
            StationType = stationType
        };
        var consoleType = new StationType { Name = "PS-Test" };
        var console = new Station
        {
            Name = "PS TEST 01",
            Zone = "console",
            Type = "PS5",
            RatePerHour = 150000,
            StationType = consoleType
        };
        var customer = new Customer
        {
            FullName = "مشتری گزارش",
            Code = "REPORT01",
            Username = "report_customer",
        };
        var secondCustomer = new Customer
        {
            FullName = "مشتری دیگر",
            Code = "REPORT02",
            Username = "report_customer_2",
        };
        var operatorUser = new AppUser
        {
            FullName = "اپراتور گزارش",
            UserName = "report-operator",
            Email = "report-operator@gamenet.local",
            PasswordHash = PasswordSecurity.Hash("StrongPass123!"),
            Role = "Operator"
        };

        var now = DateTimeOffset.UtcNow;
        var completed = new Session
        {
            Customer = customer,
            Station = pc,
            AppUser = operatorUser,
            StartAt = now.AddMinutes(-90),
            EndAt = now.AddMinutes(-30),
            PausedMinutes = 10,
            Persons = 1,
            State = SessionState.Completed,
            TotalAmount = 80000m
        };
        var active = new Session
        {
            Customer = secondCustomer,
            Station = console,
            StartAt = now.AddMinutes(-20),
            Persons = 2,
            State = SessionState.Active,
            TotalAmount = 0m
        };

        database.AddRange(stationType, consoleType, pc, console, customer, secondCustomer, operatorUser, completed, active);
        await database.SaveChangesAsync();

        var service = new SessionReportService(database);
        var result = await service.QueryAsync(
            new SessionReportQuery(
                From: now.AddHours(-2),
                To: now,
                Station: null,
                Zone: "pc",
                Operator: "report-operator",
                State: "Completed",
                CustomerSearch: "REPORT01",
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal("PC TEST 01", row.StationName);
        Assert.Equal("مشتری گزارش", row.CustomerName);
        Assert.Equal("Completed", row.State);
        Assert.Equal(80000m, row.TotalAmount);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Summary.SessionCount);
        Assert.Equal(80000m, result.Summary.Revenue);
        Assert.True(result.Summary.BillableMinutes >= 49);
        Assert.Single(result.Summary.Stations);
    }

    [Fact]
    public async Task QueryUsesSessionInvoiceLedgerInsteadOfMutableSessionTotalAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection).Options;
        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "PC-Ledger-Test" };
        var station = new Station { Name = "PC LEDGER", Zone = "pc", Type = "PC", StationType = stationType };
        var customer = new Customer { FullName = "مشتری Ledger", Code = "LEDGER01", Username = "ledger01" };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            StartAt = DateTimeOffset.UtcNow.AddHours(-2),
            EndAt = DateTimeOffset.UtcNow.AddHours(-1),
            State = SessionState.Completed,
            TotalAmount = 20000m
        };
        var invoice = new Invoice
        {
            Customer = customer,
            Session = session,
            TotalAmount = 100000m,
            Status = InvoiceStatus.Paid,
            PaidAt = DateTimeOffset.UtcNow.AddHours(-1),
            IsCustomerAccount = true,
            AccountState = CustomerAccountState.PendingPayment
        };
        invoice.Items.Add(new InvoiceItem
        {
            Invoice = invoice,
            Session = session,
            Description = "هزینه جلسه Ledger",
            Quantity = 1,
            UnitPrice = 70000m,
            Amount = 70000m
        });
        invoice.Items.Add(new InvoiceItem
        {
            Invoice = invoice,
            Session = session,
            Description = "بوفه Ledger",
            Quantity = 1,
            UnitPrice = 30000m,
            Amount = 30000m
        });

        database.AddRange(stationType, station, customer, session, invoice);
        await database.SaveChangesAsync();

        var service = new SessionReportService(database);
        var result = await service.QueryAsync(
            new SessionReportQuery(null, null, null, null, null, "Completed", null, 1, 10),
            CancellationToken.None);

        var row = Assert.Single(result.Items);
        Assert.Equal(100000m, row.TotalAmount);
        Assert.Equal(100000m, result.Summary.Revenue);
    }

    [Fact]
    public async Task QueryPaginatesByNewestSessionWithoutChangingSummary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "PC-Paging-Test" };
        var station = new Station
        {
            Name = "PC PAGING",
            Zone = "pc",
            Type = "PC",
            RatePerHour = 90000,
            StationType = stationType
        };

        for (var index = 0; index < 23; index++)
        {
            database.Sessions.Add(new Session
            {
                Customer = new Customer
                {
                    FullName = "مشتری " + index,
                    Code = "PAGE" + index.ToString("00"),
                    Username = "page" + index
                },
                Station = station,
                StartAt = DateTimeOffset.UtcNow.AddMinutes(-index),
                EndAt = DateTimeOffset.UtcNow.AddMinutes(-index + 1),
                Persons = 1,
                State = SessionState.Completed,
                TotalAmount = 10000m + index
            });
        }

        await database.SaveChangesAsync();

        var service = new SessionReportService(database);
        var page1 = await service.QueryAsync(
            new SessionReportQuery(null, null, null, null, null, "Completed", null, 1, 10),
            CancellationToken.None);
        var page2 = await service.QueryAsync(
            new SessionReportQuery(null, null, null, null, null, "Completed", null, 2, 10),
            CancellationToken.None);

        Assert.Equal(23, page1.Total);
        Assert.Equal(23, page1.Summary.SessionCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.True(page1.Items[0].StartAt >= page2.Items[0].StartAt);
        Assert.Equal(230253m, page1.Summary.Revenue);
    }
}
