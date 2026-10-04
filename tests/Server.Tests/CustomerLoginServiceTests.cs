using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class CustomerLoginServiceTests
{
    [Fact]
    public async Task ConcurrentAcquireHonorsCustomerLimit()
    {
        var databaseName = "customer-login-race";
        await using (var setupConnection = new SqliteConnection($"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5"))
        {
            await setupConnection.OpenAsync();
            await using var setup = CreateContext(setupConnection);
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
        }

        Guid customerId;
        await using (var readConnection = new SqliteConnection($"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5"))
        {
            await readConnection.OpenAsync();
            await using var read = CreateContext(readConnection);
            customerId = await read.Customers.Select(item => item.Id).SingleAsync();
        }

        async Task<object> Acquire(string clientKey)
        {
            await using var connection = new SqliteConnection($"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
            await connection.OpenAsync();
            await using var context = CreateContext(connection);
            var service = new CustomerLoginService(context);
            try
            {
                var result = await service.AcquireAsync(customerId, clientKey, CancellationToken.None);
                return result;
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

    private static GameNetDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
