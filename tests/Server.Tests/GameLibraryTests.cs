using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Tests;

public sealed class GameLibraryTests
{
    [Fact]
    public async Task GameLibraryAndAccountPoolPersistWithoutPlaintextPassword()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection).Options;
        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var game = new Game
        {
            Name = "Stage 11 Test Game",
            Genre = "FPS",
            Launcher = "Steam",
            InstallPath = @"D:\Games\Stage11",
            ExecutablePath = @"D:\Games\Stage11\game.exe",
            TargetScope = "zone",
            TargetZone = "pc",
            ProcessNames = "game.exe"
        };
        var account = new GameAccountPoolEntry
        {
            AccountName = "stage11-test-account",
            Platform = "Steam",
            Login = "stage11-login",
            PasswordHash = PasswordSecurity.Hash("Secret!123")
        };
        account.AllowedGames.Add(new GameAccountAllowedGame { Game = game, GameAccountPoolEntry = account });
        database.Games.Add(game);
        database.GameAccountPoolEntries.Add(account);
        await database.SaveChangesAsync();

        var loadedGame = await database.Games.SingleAsync(item => item.Id == game.Id);
        var loadedAccount = await database.GameAccountPoolEntries.Include(item => item.AllowedGames).SingleAsync(item => item.Id == account.Id);

        Assert.Equal(@"D:\Games\Stage11", loadedGame.InstallPath);
        Assert.Equal("game.exe", loadedGame.ProcessNames);
        Assert.Equal(GameAccountPoolStatus.Free, loadedAccount.Status);
        Assert.NotEqual("Secret!123", loadedAccount.PasswordHash);
        Assert.True(PasswordSecurity.Verify("Secret!123", loadedAccount.PasswordHash!));
        Assert.Single(loadedAccount.AllowedGames);
    }

    [Fact]
    public async Task OnlyOneActiveLeasePerAccountIsAllowed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GameNetDbContext>().UseSqlite(connection).Options;
        await using var database = new GameNetDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var game = new Game { Name = "Lease Test Game", Genre = "FPS", IsActive = true };
        var account = new GameAccountPoolEntry { AccountName = "lease-account", Platform = "Steam" };
        database.AddRange(game, account);
        await database.SaveChangesAsync();

        database.GameAccountLeases.Add(new GameAccountLease
        {
            GameAccountPoolEntryId = account.Id,
            GameId = game.Id,
            Status = GameAccountLeaseStatus.Active,
            LeasedAt = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync();

        database.GameAccountLeases.Add(new GameAccountLease
        {
            GameAccountPoolEntryId = account.Id,
            GameId = game.Id,
            Status = GameAccountLeaseStatus.Active,
            LeasedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }
}
