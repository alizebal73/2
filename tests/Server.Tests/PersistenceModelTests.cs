using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class PersistenceModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenceModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    [Fact]
    public async Task CanPersistCoreDomainEntitiesAndReadThemBack()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using (var db = new GameNetDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var stationType = new StationType
            {
                Name = "Console",
                Description = "PlayStation and console area"
            };

            var tariff = new Tariff
            {
                Name = "Weekend Special",
                Description = "Weekend hourly rate",
                HourlyRate = 95000m,
                DailyRate = 550000m,
                IsActive = true
            };

            var customer = new Customer
            {
                FullName = "Ali Reza",
                Phone = "09120000000",
                Email = "ali@example.com",
                IsVip = true,
                Balance = 250000m,
                Notes = "Regular VIP customer"
            };

            var product = new Product
            {
                Name = "Energy Drink",
                Category = "Beverage",
                UnitPrice = 25000m,
                CostPrice = 15000m,
                StockQuantity = 40,
                IsActive = true
            };

            var appUser = new AppUser
            {
                FullName = "System Admin",
                UserName = "admin",
                Email = "admin@example.com",
                PasswordHash = "hashed-value",
                Role = "Admin",
                IsActive = true
            };

            var vipPackage = new VipPackage
            {
                Name = "Gold",
                Price = 1200000m,
                DurationDays = 30,
                Description = "Gold VIP access"
            };

            var station = new Station
            {
                Name = "PS5-01",
                Zone = "Zone A",
                State = StationState.Available,
                RatePerHour = 120000m,
                StationType = stationType,
                Tariff = tariff,
                IsActive = true
            };

            db.StationTypes.Add(stationType);
            db.Tariffs.Add(tariff);
            db.Customers.Add(customer);
            db.Products.Add(product);
            db.AppUsers.Add(appUser);
            db.VipPackages.Add(vipPackage);
            db.Stations.Add(station);

            await db.SaveChangesAsync();

            var savedStation = await db.Stations
                .Include(s => s.StationType)
                .Include(s => s.Tariff)
                .SingleAsync();

            Assert.Equal("PS5-01", savedStation.Name);
            Assert.NotNull(savedStation.StationType);
            Assert.Equal("Console", savedStation.StationType!.Name);
            Assert.NotNull(savedStation.Tariff);
            Assert.Equal("Weekend Special", savedStation.Tariff!.Name);
            Assert.Equal(120000m, savedStation.RatePerHour);

            var savedCustomer = await db.Customers.SingleAsync(c => c.Email == "ali@example.com");
            Assert.True(savedCustomer.IsVip);
            Assert.Equal(250000m, savedCustomer.Balance);

            var savedProduct = await db.Products.SingleAsync(p => p.Name == "Energy Drink");
            Assert.Equal(40, savedProduct.StockQuantity);

            var savedUser = await db.AppUsers.SingleAsync(u => u.UserName == "admin");
            Assert.Equal("Admin", savedUser.Role);

            var savedVip = await db.VipPackages.SingleAsync(v => v.Name == "Gold");
            Assert.Equal(30, savedVip.DurationDays);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
