using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class DatabaseSeederTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);

    [Fact]
    public async Task ProductionSeed_DoesNotCreateDemoCustomersProductsOrStations()
    {
        await EnvironmentGate.WaitAsync();
        var previous = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", "ProductionSeedTest!123");
            await RunProductionSeedAssertionAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", previous);
            EnvironmentGate.Release();
        }
    }

    [Fact]
    public async Task DemoSeed_CreatesReferenceDemoDataWithoutDuplicateCustomer()
    {
        await EnvironmentGate.WaitAsync();
        var previous = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", "DemoSeedTest!123");
            await RunDemoSeedAssertionAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", previous);
            EnvironmentGate.Release();
        }
    }

    private static async Task RunProductionSeedAssertionAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = CreateContext(connection);
        await database.Database.EnsureCreatedAsync();

        await DatabaseSeeder.SeedAsync(database, includeDemoData: false);

        Assert.Empty(await database.Customers.ToListAsync());
        Assert.Empty(await database.Products.ToListAsync());
        Assert.Empty(await database.Stations.ToListAsync());
        Assert.Single(await database.AppUsers.ToListAsync());
        Assert.NotEmpty(await database.Permissions.ToListAsync());
        Assert.Contains(
            await database.Permissions.ToListAsync(),
            item => item.Name == "client.power");
    }

    private static async Task RunDemoSeedAssertionAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = CreateContext(connection);
        await database.Database.EnsureCreatedAsync();

        await DatabaseSeeder.SeedAsync(database, includeDemoData: true);

        Assert.Equal(4, await database.Customers.CountAsync());
        Assert.Single(await database.Products.ToListAsync());
        Assert.Equal(61, await database.Stations.CountAsync());
        Assert.Single(await database.Customers.Where(item => item.Code == "1050").ToListAsync());
        Assert.Single(await database.AppUsers.Where(item => item.UserName == "admin").ToListAsync());
    }

    private static GameNetDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
