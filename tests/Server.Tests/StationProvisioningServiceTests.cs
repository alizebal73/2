using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class StationProvisioningServiceTests
{
    [Fact]
    public async Task ProvisionRange_CreatesStationsWithUniqueNamesAndNetwork()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        try
        {
            var options = new DbContextOptionsBuilder<GameNetManager.Server.Data.GameNetDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var database = new GameNetManager.Server.Data.GameNetDbContext(options);
            await database.Database.EnsureCreatedAsync();
            var appUserId = await SeedAppUserAsync(database);

            var service = new GameNetManager.Server.Data.StationProvisioningService(database);

            var result = await service.ProvisionRangeAsync(
                new GameNetManager.Server.Data.ProvisionStationRangeRequest(
                    "PC",
                    1,
                    3,
                    "pc",
                    "PC",
                    95000m,
                    2),
                appUserId,
                CancellationToken.None);

            Assert.Null(result.ErrorCode);
            Assert.Equal(3, result.Created);
            Assert.Equal(new[] { "PC-01", "PC-02", "PC-03" }, result.Names);

            var stations = await database.Stations
                .Include(item => item.StationType)
                .ToListAsync();

            Assert.Equal(3, stations.Count);
            Assert.All(stations, station =>
            {
                Assert.Equal("pc", station.Zone);
                Assert.Equal("PC", station.Type);
                Assert.Equal(95000m, station.RatePerHour);
                Assert.Equal(2, station.Network);
                Assert.True(station.IsActive);
                Assert.Equal("PC", station.StationType!.Name);
                Assert.Equal(GameNetManager.Server.Data.StationState.Available, station.State);
            });
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Archive_IsRejectedWhileActiveAgentOwnsStation()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        try
        {
            var options = new DbContextOptionsBuilder<GameNetManager.Server.Data.GameNetDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var database = new GameNetManager.Server.Data.GameNetDbContext(options);
            await database.Database.EnsureCreatedAsync();
            var appUserId = await SeedAppUserAsync(database);

            var service = new GameNetManager.Server.Data.StationProvisioningService(database);
            var created = await service.CreateAsync(
                new GameNetManager.Server.Data.CreateStationRequest(
                    "PC-01",
                    "pc",
                    "PC",
                    95000m,
                    1),
                appUserId,
                CancellationToken.None);

            Assert.Null(created.ErrorCode);
            var station = await database.Stations.SingleAsync();

            database.AgentDevices.Add(new GameNetManager.Server.Data.AgentDevice
            {
                DeviceId = "physical-test-pc-01",
                Name = "PC-01",
                AgentTokenHash = "test-hash",
                StationId = station.Id,
                IsActive = true
            });
            await database.SaveChangesAsync();

            var result = await service.UpdateAsync(
                station.Id,
                new GameNetManager.Server.Data.UpdateStationRequest(
                    station.Name,
                    station.Zone,
                    station.Type,
                    station.RatePerHour,
                    station.Network,
                    null,
                    false),
                appUserId,
                CancellationToken.None);

            Assert.Equal("station_has_agent", result.ErrorCode);
            Assert.True(await database.Stations.Where(item => item.Id == station.Id).Select(item => item.IsActive).SingleAsync());
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
    private static async Task<Guid> SeedAppUserAsync(GameNetManager.Server.Data.GameNetDbContext database)
    {
        var user = new GameNetManager.Server.Data.AppUser
        {
            FullName = "Test Owner",
            UserName = "test-owner",
            Email = "test-owner@gamenet.local",
            PasswordHash = "test-hash",
            Role = "Owner",
            IsActive = true
        };
        database.AppUsers.Add(user);
        await database.SaveChangesAsync();
        return user.Id;
    }

}
