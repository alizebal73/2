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

            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var database = CreateContext(connection);
            await database.Database.EnsureCreatedAsync();

            await DatabaseSeeder.SeedAsync(database, includeDemoData: false);

            Assert.Empty(await database.Customers.ToListAsync());
            Assert.Empty(await database.Products.ToListAsync());
            Assert.Empty(await database.Stations.ToListAsync());
            Assert.Single(await database.AppUsers.ToListAsync());
            Assert.Contains(await database.Permissions.ToListAsync(), item => item.Name == "client.power");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", previous);
            EnvironmentGate.Release();
        }
    }

    [Fact]
    public async Task DemoSeed_CreatesReferenceDemoData()
    {
        await EnvironmentGate.WaitAsync();
        var previous = Environment.GetEnvironmentVariable("GAMENET_ADMIN_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", "DemoSeedTest!123");

            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var database = CreateContext(connection);
            await database.Database.EnsureCreatedAsync();

            await DatabaseSeeder.SeedAsync(database, includeDemoData: true);

            Assert.Equal(4, await database.Customers.CountAsync());
            Assert.Single(await database.Products.ToListAsync());
            Assert.Equal(61, await database.Stations.CountAsync());
            Assert.Equal(20, await database.Stations.CountAsync(item => item.Type == "PC" && item.Network == 1));
            Assert.Equal(20, await database.Stations.CountAsync(item => item.Type == "PC" && item.Network == 2));
            Assert.Equal("PC-21", (await database.Stations.OrderBy(item => item.Name).SingleAsync(item => item.Name == "PC-21")).Name);
            Assert.Single(await database.Customers.Where(item => item.Code == "1050").ToListAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GAMENET_ADMIN_PASSWORD", previous);
            EnvironmentGate.Release();
        }
    }

    private static GameNetDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
