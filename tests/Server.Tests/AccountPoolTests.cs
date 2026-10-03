using GameNetManager.Server.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class AccountPoolTests
{
    private static GameNetDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite(connection)
            .Options;
        return new GameNetDbContext(options);
    }

    private static GameCredentialProtectionService CreateProtection()
        => new(DataProtectionProvider.Create("GameNetManager.Server.Tests"));

    private static SqliteConnection CreateSharedMemoryConnection(string databaseName)
    {
        return new SqliteConnection($"Data Source=file:{databaseName};Mode=Memory;Cache=Shared;Default Timeout=5");
    }

    [Fact]
    public async Task AllocateAndRelease_IsServerAuthoritative()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var setup = CreateContext(connection))
        {
            await setup.Database.EnsureCreatedAsync();
            var game = new Game { Name = "Stage11 Game", IsActive = true };
            setup.Games.Add(game);
            await setup.SaveChangesAsync();

            setup.AccountPoolEntries.Add(new AccountPoolEntry
            {
                Title = "Pool-01",
                Platform = "Steam",
                AllowedGameIdsCsv = game.Id.ToString(),
                Status = AccountPoolStatus.Free,
                IsActive = true
            });
            await setup.SaveChangesAsync();
        }

        Guid gameId;
        await using (var read = CreateContext(connection))
            gameId = await read.Games.Select(item => item.Id).SingleAsync();

        await using (var allocationContext = CreateContext(connection))
        {
            var service = new AccountPoolService(allocationContext, CreateProtection());
            var (account, lease) = await service.AllocateAsync(gameId, null, null, null, CancellationToken.None);

            Assert.NotNull(account);
            Assert.NotNull(lease);
            Assert.Equal(AccountPoolStatus.InUse, account!.Status);

            var released = await service.ReleaseAsync(lease!.Id, "test release", CancellationToken.None);
            Assert.NotNull(released);
            Assert.Equal(AccountLeaseState.Released, released!.State);
            Assert.Equal("test release", released.ReleaseReason);
        }

        await using (var verify = CreateContext(connection))
        {
            var account = await verify.AccountPoolEntries.SingleAsync();
            Assert.Equal(AccountPoolStatus.Free, account.Status);
            Assert.Null(account.AssignedAgentDeviceId);
        }
    }

    [Fact]
    public async Task ConcurrentAllocation_CannotLeaseTheSamePoolEntryTwice()
    {
        var databaseName = $"stage11-concurrent-{Guid.NewGuid():N}";
        await using var keeper = CreateSharedMemoryConnection(databaseName);
        await keeper.OpenAsync();

        Guid gameId;
        await using (var setup = CreateContext(keeper))
        {
            await setup.Database.EnsureCreatedAsync();
            var game = new Game { Name = "Concurrent Game", IsActive = true };
            setup.Games.Add(game);
            await setup.SaveChangesAsync();
            gameId = game.Id;

            setup.AccountPoolEntries.Add(new AccountPoolEntry
            {
                Title = "Pool-Concurrent",
                Platform = "Steam",
                AllowedGameIdsCsv = game.Id.ToString(),
                Status = AccountPoolStatus.Free,
                IsActive = true
            });
            await setup.SaveChangesAsync();
        }

        var taskA = Task.Run(async () =>
        {
            await using var connectionA = CreateSharedMemoryConnection(databaseName);
            await connectionA.OpenAsync();
            await using var context = CreateContext(connectionA);
            return await new AccountPoolService(context, CreateProtection()).AllocateAsync(gameId, null, null, null, CancellationToken.None);
        });
        var taskB = Task.Run(async () =>
        {
            await using var connectionB = CreateSharedMemoryConnection(databaseName);
            await connectionB.OpenAsync();
            await using var context = CreateContext(connectionB);
            return await new AccountPoolService(context, CreateProtection()).AllocateAsync(gameId, null, null, null, CancellationToken.None);
        });

        var results = await Task.WhenAll(taskA, taskB);
        Assert.Equal(1, results.Count(result => result.Account is not null && result.Lease is not null));
        Assert.Equal(1, results.Count(result => result.Account is null && result.Lease is null));
    }

    [Fact]
    public async Task Allocation_SkipsExpiredFreeAccounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        Guid gameId;
        await using (var setup = CreateContext(connection))
        {
            await setup.Database.EnsureCreatedAsync();
            var game = new Game { Name = "Expiry Game", IsActive = true };
            setup.Games.Add(game);
            await setup.SaveChangesAsync();
            gameId = game.Id;

            setup.AccountPoolEntries.AddRange(
                new AccountPoolEntry
                {
                    Title = "Expired",
                    Platform = "Steam",
                    AllowedGameIdsCsv = game.Id.ToString(),
                    Status = AccountPoolStatus.Free,
                    IsActive = true,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new AccountPoolEntry
                {
                    Title = "Valid",
                    Platform = "Steam",
                    AllowedGameIdsCsv = game.Id.ToString(),
                    Status = AccountPoolStatus.Free,
                    IsActive = true,
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });
            await setup.SaveChangesAsync();
        }

        await using var context = CreateContext(connection);
        var service = new AccountPoolService(context, CreateProtection());
        var result = await service.AllocateAsync(gameId, null, null, null, CancellationToken.None);

        Assert.NotNull(result.Account);
        Assert.NotNull(result.Lease);
        Assert.Equal("Valid", result.Account!.Title);
    }

    [Fact]
    public async Task OperationalSession_AllocatesLeaseAndCredentialIsBoundToActiveLease()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var protection = new GameCredentialProtectionService(
            DataProtectionProvider.Create("GameNetManager.Server.Tests.Operational"));

        Guid sessionId;
        Guid agentId;

        await using (var setup = CreateContext(connection))
        {
            await setup.Database.EnsureCreatedAsync();

            var stationType = new StationType { Name = "PC" };
            var tariff = new Tariff { Name = "Stage12", HourlyRate = 1000m, DailyRate = 5000m, IsActive = true };
            var station = new Station
            {
                Name = "ST12-01",
                Zone = "Stage12",
                Type = "PC",
                StationType = stationType,
                Tariff = tariff,
                RatePerHour = 1000m,
                State = StationState.Occupied,
                IsActive = true
            };
            var customer = new Customer { FullName = "Stage12 Customer", Username = "stage12", ConcurrentLoginLimit = 1 };
            var game = new Game { Name = "Stage12 Game", IsActive = true, Status = "online" };
            var agent = new AgentDevice
            {
                DeviceId = "stage12-agent",
                Name = "Stage12 Agent",
                AgentTokenHash = PasswordSecurity.HashToken("stage12-token"),
                Station = station,
                IsOnline = true,
                IsActive = true
            };
            var login = new CustomerLogin { Customer = customer, ClientKey = agent.DeviceId, IsActive = true };

            setup.Stations.Add(station);
            setup.Customers.Add(customer);
            setup.Games.Add(game);
            setup.AgentDevices.Add(agent);
            setup.CustomerLogins.Add(login);
            await setup.SaveChangesAsync();

            var session = new Session
            {
                CustomerId = customer.Id,
                StationId = station.Id,
                AgentDeviceId = agent.Id,
                GameId = game.Id,
                StartAt = DateTimeOffset.UtcNow,
                State = SessionState.Active
            };
            setup.Sessions.Add(session);

            setup.AccountPoolEntries.Add(new AccountPoolEntry
            {
                Title = "Stage12 Pool",
                Platform = "Steam",
                Login = "stage12-login",
                SecretHash = PasswordSecurity.Hash("Stage12Secret!"),
                SecretCiphertext = protection.Protect("Stage12Secret!"),
                AllowedGameIdsCsv = game.Id.ToString(),
                Status = AccountPoolStatus.Free,
                IsActive = true
            });

            await setup.SaveChangesAsync();
            sessionId = session.Id;
            agentId = agent.Id;
        }

        Guid leaseId;
        string leaseToken;

        await using (var context = CreateContext(connection))
        {
            var service = new AccountPoolService(context, protection);
            var credential = await service.AcquireCredentialForOperationalSessionAsync(
                sessionId,
                agentId,
                CancellationToken.None);

            Assert.NotNull(credential);
            leaseId = credential!.LeaseId;
            leaseToken = await context.AccountLeases
                .Where(item => item.Id == leaseId)
                .Select(item => item.LeaseToken)
                .SingleAsync();

            Assert.NotNull(credential);
            Assert.Equal("Stage12Secret!", credential!.Secret);
            Assert.Equal("stage12-login", credential.Login);
            Assert.Null(await service.GetCredentialAsync(
                leaseId, "wrong-token", agentId, CancellationToken.None));
            Assert.Null(await service.GetCredentialAsync(
                leaseId, leaseToken, Guid.NewGuid(), CancellationToken.None));
        }

        await using (var context = CreateContext(connection))
        {
            var service = new AccountPoolService(context, protection);
            Assert.Equal(1, await service.ReleaseActiveForSessionAsync(
                sessionId,
                "Stage12 test release",
                CancellationToken.None));
        }

        await using (var verify = CreateContext(connection))
        {
            var account = await verify.AccountPoolEntries.SingleAsync();
            var lease = await verify.AccountLeases.SingleAsync();

            Assert.Equal(AccountPoolStatus.Free, account.Status);
            Assert.Null(account.AssignedAgentDeviceId);
            Assert.Equal(AccountLeaseState.Released, lease.State);
            Assert.Null(lease.CredentialAccessExpiresAt);
            Assert.Equal("Stage12 test release", lease.ReleaseReason);
        }
    }
}
