using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class CustomerLoginServiceTests
{
    [Fact]
    public async Task ConcurrentAcquireHonorsCustomerLimit()
    {
        var databaseName = $"customer-login-race-{Guid.NewGuid():N}";
        await using var keeper = new SqliteConnection(
            $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
        await keeper.OpenAsync();

        Guid customerId;
        await using (var setup = CreateContext(keeper))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Customers.Add(new Customer
            {
                FullName = "Race Customer",
                Code = "RACE-1",
                Username = "race-customer",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            });
            await setup.SaveChangesAsync();
            customerId = await setup.Customers.Select(item => item.Id).SingleAsync();
        }

        async Task<object> Acquire(string clientKey)
        {
            await using var connection = new SqliteConnection(
                $"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
            await connection.OpenAsync();
            await using var context = CreateContext(connection);
            var service = new CustomerLoginService(context);
            try
            {
                return await service.AcquireAsync(customerId, clientKey, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(
            Acquire("agent-01"),
            Acquire("agent-02"));

        Assert.Equal(1, results.Count(item => item is ConcurrentLoginResultDto));
        Assert.Equal(1, results.Count(item => item is InvalidOperationException ex
            && ex.Message.StartsWith("CONCURRENT_LOGIN_LIMIT:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ActiveLoginResolutionRequiresExactCustomerLoginAndClientBinding()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer
        {
            FullName = "Auth State Customer",
            Code = "AUTH-1",
            Username = "auth-state",
            Balance = 250000m,
            FreeMoney = 50000m
        };
        var matchingLogin = new CustomerLogin
        {
            Customer = customer,
            ClientKey = "agent-01",
            IsActive = true
        };
        var otherDeviceLogin = new CustomerLogin
        {
            Customer = customer,
            ClientKey = "agent-02",
            IsActive = true
        };

        db.AddRange(customer, matchingLogin, otherDeviceLogin);
        await db.SaveChangesAsync();

        var service = new CustomerLoginService(db);

        var wrongLogin = await service.ResolveActiveAsync(
            customer.Id,
            Guid.NewGuid(),
            "agent-01",
            CancellationToken.None);
        var wrongDevice = await service.ResolveActiveAsync(
            customer.Id,
            matchingLogin.Id,
            "agent-02",
            CancellationToken.None);
        var valid = await service.ResolveActiveAsync(
            customer.Id,
            matchingLogin.Id,
            "agent-01",
            CancellationToken.None);

        Assert.Null(wrongLogin);
        Assert.Null(wrongDevice);
        Assert.NotNull(valid);
        Assert.Equal(customer.Id, valid!.CustomerId);
        Assert.Equal(250000m, valid.Customer.Balance);
        Assert.Equal(50000m, valid.Customer.FreeMoney);
    }

    private static GameNetDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
