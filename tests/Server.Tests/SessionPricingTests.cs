using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class SessionPricingTests
{
    [Fact]
    public async Task SettlementUsesSessionHourlyRateSnapshotAfterTariffChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var database = CreateContext(connection);
        await database.Database.EnsureCreatedAsync();

        var stationType = new StationType { Name = "PC" };
        var tariff = new Tariff
        {
            Name = "Initial",
            HourlyRate = 100m,
            DailyRate = 0m,
            IsActive = true
        };
        var station = new Station
        {
            Name = "PRICING-01",
            Zone = "Test",
            Type = "PC",
            StationType = stationType,
            Tariff = tariff,
            RatePerHour = 100m,
            State = StationState.Occupied,
            IsActive = true
        };
        var customer = new Customer
        {
            FullName = "Pricing Customer",
            Code = "PRICING-1",
            Username = "pricing-customer",
            ConcurrentLoginLimit = 1,
            VipTier = "none"
        };
        var session = new Session
        {
            Customer = customer,
            Station = station,
            Tariff = tariff,
            StartAt = DateTimeOffset.UtcNow.AddHours(-1),
            State = SessionState.Active,
            HourlyRateSnapshot = 100m,
            HourlyRateOverride = null,
            Persons = 1
        };

        database.Stations.Add(station);
        database.Customers.Add(customer);
        database.Sessions.Add(session);
        await database.SaveChangesAsync();

        tariff.HourlyRate = 200m;
        await database.SaveChangesAsync();

        var settlement = new SessionSettlementService(
            database,
            new SessionPricingService(database));

        var result = await settlement.SettleAsync(
            session.Id,
            new SessionSettlementRequest(
                TotalAmount: 100m,
                Parts: new[] { new SettlementPart("cash", 100m) },
                AppUserId: null,
                FreeTimeMinutes: 0,
                TimeAmount: 100m),
            CancellationToken.None);

        Assert.Equal(100m, result.TotalAmount);
        var savedSession = await database.Sessions.AsNoTracking().SingleAsync();
        Assert.Equal(100m, savedSession.TotalAmount);
        Assert.Equal(100m, savedSession.HourlyRateSnapshot);
    }

    private static GameNetDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options);
}
