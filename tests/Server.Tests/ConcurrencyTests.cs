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
        Guid secondLoginId;
        Guid stationId;
        Guid secondStationId;

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
            var secondStation = new Station
            {
                Name = "CONCURRENCY-ST-2",
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
                ClientKey = "concurrency-agent-1",
                IsActive = true
            };
            var secondLogin = new CustomerLogin
            {
                Customer = customer,
                ClientKey = "concurrency-agent-2",
                IsActive = true
            };

            seed.StationTypes.Add(stationType);
            seed.Stations.AddRange(station, secondStation);
            seed.Customers.Add(customer);
            seed.CustomerLogins.AddRange(login, secondLogin);
            await seed.SaveChangesAsync();

            customerId = customer.Id;
            loginId = login.Id;
            secondLoginId = secondLogin.Id;
            stationId = station.Id;
            secondStationId = secondStation.Id;
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
                StationId = secondStationId,
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
                CustomerLoginId = secondLoginId,
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
    public async Task ConcurrentCustomerLoginAcquire_RespectsConfiguredLimit()
    {
        await using var connection1 = new SqliteConnection("DataSource=file:customer-login-concurrency;Mode=Memory;Cache=Shared");
        await using var connection2 = new SqliteConnection("DataSource=file:customer-login-concurrency;Mode=Memory;Cache=Shared");
        await connection1.OpenAsync();
        await connection2.OpenAsync();

        var options1 = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection1)
            .Options;
        var options2 = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection2)
            .Options;

        Guid customerId;
        await using (var seed = new GameNetDbContext(options1))
        {
            await seed.Database.EnsureCreatedAsync();
            var customer = new Customer
            {
                FullName = "Concurrent Login Customer",
                Code = "CONCURRENT-LOGIN-1",
                Username = "concurrent-login",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            };
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            customerId = customer.Id;
        }

        async Task<(bool Success, string? Error)> AcquireAsync(
            GameNetDbContext context,
            string clientKey)
        {
            try
            {
                var result = await new CustomerLoginService(context).AcquireAsync(
                    customerId,
                    clientKey,
                    CancellationToken.None);
                return (result.Acquired, null);
            }
            catch (Exception exception)
            {
                return (false, exception.Message);
            }
        }

        await using var db1 = new GameNetDbContext(options1);
        await using var db2 = new GameNetDbContext(options2);

        var results = await Task.WhenAll(
            AcquireAsync(db1, "customer-login-agent-1"),
            AcquireAsync(db2, "customer-login-agent-2"));

        Assert.Single(results, result => result.Success);
        Assert.Single(results, result => result.Error?.StartsWith("CONCURRENT_LOGIN_LIMIT:", StringComparison.Ordinal) == true);

        await using var verify = new GameNetDbContext(options1);
        Assert.Equal(
            1,
            await verify.CustomerLogins.CountAsync(
                item => item.CustomerId == customerId && item.IsActive));
    }

    [Fact]
    public async Task ConcurrentFreeBenefitDebit_CannotOverspend()
    {
        await using var connection1 = new SqliteConnection("DataSource=file:free-benefit-concurrency;Mode=Memory;Cache=Shared");
        await using var connection2 = new SqliteConnection("DataSource=file:free-benefit-concurrency;Mode=Memory;Cache=Shared");
        await connection1.OpenAsync();
        await connection2.OpenAsync();

        var options1 = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection1).Options;
        var options2 = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection2).Options;

        Guid customerId;
        await using (var seed = new GameNetDbContext(options1))
        {
            await seed.Database.EnsureCreatedAsync();
            var customer = new Customer
            {
                FullName = "Concurrent Free Benefit",
                Code = "FREE-BENEFIT-1",
                Username = "free-benefit-concurrent",
                ConcurrentLoginLimit = 1,
                VipTier = "none",
                FreeMoney = 100m
            };
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
            customerId = customer.Id;
        }

        async Task<bool> DebitAsync(GameNetDbContext context)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var updated = await context.Customers
                .Where(item => item.Id == customerId && item.FreeMoney >= 100m)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FreeMoney, item => item.FreeMoney - 100m)
                    .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow));
            if (updated != 1)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await transaction.CommitAsync();
            return true;
        }

        var results = await Task.WhenAll(
            DebitAsync(new GameNetDbContext(options1)),
            DebitAsync(new GameNetDbContext(options2)));

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);

        await using var verify = new GameNetDbContext(options1);
        Assert.Equal(0m, await verify.Customers
            .Where(item => item.Id == customerId)
            .Select(item => item.FreeMoney)
            .SingleAsync());
    }

    [Fact]
    public async Task AtomicBuffetStockClaim_CannotOversell()
    {
        await using var connection1 = new SqliteConnection("DataSource=file:buffet-stock-concurrency;Mode=Memory;Cache=Shared");
        await using var connection2 = new SqliteConnection("DataSource=file:buffet-stock-concurrency;Mode=Memory;Cache=Shared");
        await connection1.OpenAsync();
        await connection2.OpenAsync();

        var options1 = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection1).Options;
        var options2 = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection2).Options;

        Guid productId;
        await using (var seed = new GameNetDbContext(options1))
        {
            await seed.Database.EnsureCreatedAsync();
            var product = new Product
            {
                Name = "Concurrent Stock",
                Category = "Test",
                UnitPrice = 100m,
                CostPrice = 50m,
                StockQuantity = 1,
                IsActive = true
            };
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            productId = product.Id;
        }

        async Task<int> ClaimAsync(GameNetDbContext context)
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var updated = await context.Products
                .Where(item => item.Id == productId && item.IsActive && item.StockQuantity >= 1)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.StockQuantity, item => item.StockQuantity - 1)
                    .SetProperty(item => item.UpdatedAt, DateTimeOffset.UtcNow));
            await transaction.CommitAsync();
            return updated;
        }

        var claims = await Task.WhenAll(
            ClaimAsync(new GameNetDbContext(options1)),
            ClaimAsync(new GameNetDbContext(options2)));

        Assert.Equal(1, claims.Sum());
        await using var verify = new GameNetDbContext(options1);
        Assert.Equal(0, await verify.Products.Where(item => item.Id == productId).Select(item => item.StockQuantity).SingleAsync());
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
