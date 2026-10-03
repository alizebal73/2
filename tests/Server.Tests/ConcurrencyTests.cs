using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ConcurrencyTests() => _connection.Open();

    [Fact]
    public async Task ActiveSessionsCannotShareCustomerLoginOrStation()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(_connection)
            .Options;

        Guid customerId;
        Guid loginId;
        Guid stationId;

        await using (var seed = new GameNetDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();

            var stationType = new StationType { Name = "Concurrency PC" };
            var station = new Station
            {
                Name = "CONCURRENCY-ST-1",
                Zone = "Concurrency",
                Type = "PC",
                StationType = stationType,
                State = StationState.Available,
                IsActive = true
            };
            var customer = new Customer
            {
                FullName = "Concurrency Customer",
                Code = "CONCURRENCY-SESSION-1",
                Username = "concurrency-session",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            var login = new CustomerLogin
            {
                Customer = customer,
                ClientKey = "concurrency-agent",
                IsActive = true
            };

            seed.StationTypes.Add(stationType);
            seed.Stations.Add(station);
            seed.Customers.Add(customer);
            seed.CustomerLogins.Add(login);
            await seed.SaveChangesAsync();

            customerId = customer.Id;
            loginId = login.Id;
            stationId = station.Id;
        }

        await using (var db = new GameNetDbContext(options))
        {
            db.Sessions.Add(new Session
            {
                CustomerId = customerId,
                CustomerLoginId = loginId,
                StationId = stationId,
                StartAt = DateTimeOffset.UtcNow,
                State = SessionState.Active
            });
            await db.SaveChangesAsync();
        }

        await using (var duplicateLogin = new GameNetDbContext(options))
        {
            duplicateLogin.Sessions.Add(new Session
            {
                CustomerId = customerId,
                CustomerLoginId = loginId,
                StationId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow,
                State = SessionState.Active
            });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateLogin.SaveChangesAsync());
        }

        await using (var duplicateStation = new GameNetDbContext(options))
        {
            duplicateStation.Sessions.Add(new Session
            {
                CustomerId = customerId,
                CustomerLoginId = null,
                StationId = stationId,
                StartAt = DateTimeOffset.UtcNow,
                State = SessionState.Active
            });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateStation.SaveChangesAsync());
        }

        await using (var ended = new GameNetDbContext(options))
        {
            var active = await ended.Sessions.SingleAsync(item => item.CustomerLoginId == loginId);
            active.State = SessionState.Ended;
            active.EndAt = DateTimeOffset.UtcNow;
            await ended.SaveChangesAsync();

            ended.Sessions.Add(new Session
            {
                CustomerId = customerId,
                CustomerLoginId = loginId,
                StationId = stationId,
                StartAt = DateTimeOffset.UtcNow,
                State = SessionState.Active
            });

            await ended.SaveChangesAsync();
        }
    }

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

    public void Dispose() => _connection.Dispose();
}
