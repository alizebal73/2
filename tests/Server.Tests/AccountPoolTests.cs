using GameNetManager.Server.Data;
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
            var service = new AccountPoolService(allocationContext);
            var (account, lease) = await service.AllocateAsync(gameId, null, null, null, CancellationToken.None);

            Assert.NotNull(account);
            Assert.NotNull(lease);
            Assert.Equal(AccountPoolStatus.InUse, account!.Status);

            var released = await service.ReleaseAsync(lease!.Id, CancellationToken.None);
            Assert.NotNull(released);
            Assert.Equal(AccountLeaseState.Released, released!.State);
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
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        Guid gameId;
        await using (var setup = CreateContext(connection))
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
            await using var context = CreateContext(connection);
            return await new AccountPoolService(context).AllocateAsync(gameId, null, null, null, CancellationToken.None);
        });
        var taskB = Task.Run(async () =>
        {
            await using var context = CreateContext(connection);
            return await new AccountPoolService(context).AllocateAsync(gameId, null, null, null, CancellationToken.None);
        });

        var results = await Task.WhenAll(taskA, taskB);
        Assert.Equal(1, results.Count(result => result.Account is not null && result.Lease is not null));
        Assert.Equal(1, results.Count(result => result.Account is null && result.Lease is null));
    }
}
