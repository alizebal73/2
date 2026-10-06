using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ConcurrencyTests() => _connection.Open();

    [Fact]
    public async Task ConcurrentUpdatesToSameEntityAreRejected()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using (var seed = new GameNetDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Customers.Add(new Customer
            {
                FullName = "مشتری تعارض",
                Code = "CONFLICT-1",
                Username = "conflict-user",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            });
            await seed.SaveChangesAsync();
        }

        Customer first;
        Customer second;
        await using (var db1 = new GameNetDbContext(options))
        await using (var db2 = new GameNetDbContext(options))
        {
            first = await db1.Customers.SingleAsync(item => item.Code == "CONFLICT-1");
            second = await db2.Customers.SingleAsync(item => item.Code == "CONFLICT-1");

            first.Notes = "اپراتور اول";
            await db1.SaveChangesAsync();

            Assert.NotNull(first.UpdatedAt);
            second.Notes = "اپراتور دوم";

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => db2.SaveChangesAsync());

            var saved = await db1.Customers
                .AsNoTracking()
                .SingleAsync(item => item.Code == "CONFLICT-1");
            Assert.Equal("اپراتور اول", saved.Notes);
        }
    }



    [Fact]
    public async Task ConcurrentDebtSettlementWritesAreRejectedByInvoiceConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        Guid invoiceId;
        await using (var seed = new GameNetDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                FullName = "مشتری تسویه هم‌زمان",
                Code = "DEBT-CONFLICT",
                Username = "debt-conflict-user",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            var invoice = new Invoice
            {
                Customer = customer,
                TotalAmount = 80000m,
                IsCustomerAccount = true,
                AccountState = CustomerAccountState.Debt,
                Status = InvoiceStatus.Draft,
                IssuedAt = DateTimeOffset.UtcNow
            };

            seed.Add(invoice);
            await seed.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        await using var db1 = new GameNetDbContext(options);
        await using var db2 = new GameNetDbContext(options);

        var first = await db1.Invoices.SingleAsync(item => item.Id == invoiceId);
        var second = await db2.Invoices.SingleAsync(item => item.Id == invoiceId);

        first.Status = InvoiceStatus.Paid;
        first.PaidAt = DateTimeOffset.UtcNow;
        await db1.SaveChangesAsync();

        second.Status = InvoiceStatus.Paid;
        second.PaidAt = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => db2.SaveChangesAsync());

        var saved = await db1.Invoices
            .AsNoTracking()
            .SingleAsync(item => item.Id == invoiceId);

        Assert.Equal(InvoiceStatus.Paid, saved.Status);
    }

    [Fact]
    public async Task ActiveSessionsCannotShareCustomerLoginOrStation()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new GameNetDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var type = new StationType { Name = "TestPC" };
        var station = new Station
        {
            Name = "SESSION-01",
            Zone = "Test",
            Type = "PC",
            StationType = type,
            State = StationState.Occupied,
            IsActive = true
        };
        var customer = new Customer
        {
            FullName = "مشتری Session",
            Code = "SESSION-CUSTOMER",
            Username = "session-customer",
            ConcurrentLoginLimit = 1,
            VipTier = "none"
        };
        var login = new CustomerLogin
        {
            Customer = customer,
            ClientKey = "agent-session",
            IsActive = true
        };

        db.Stations.Add(station);
        db.Customers.Add(customer);
        db.CustomerLogins.Add(login);
        await db.SaveChangesAsync();

        db.Sessions.Add(new Session
        {
            CustomerId = customer.Id,
            CustomerLoginId = login.Id,
            StationId = station.Id,
            StartAt = DateTimeOffset.UtcNow,
            State = SessionState.Active
        });
        await db.SaveChangesAsync();

        db.Sessions.Add(new Session
        {
            CustomerId = customer.Id,
            CustomerLoginId = login.Id,
            StationId = station.Id,
            StartAt = DateTimeOffset.UtcNow.AddSeconds(1),
            State = SessionState.Active
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    public void Dispose() => _connection.Dispose();
}
