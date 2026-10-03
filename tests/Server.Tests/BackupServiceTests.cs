using GameNetManager.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;

namespace GameNetManager.Server.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GameNetBackupTests", Guid.NewGuid().ToString("N"));
    private readonly string _dbPath;

    public BackupServiceTests()
    {
        _dbPath = Path.Combine(_root, "App_Data", "gamenet.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    [Fact]
    public async Task BackupIsVerifiedAndRestoreCandidateIsPrepared()
    {
        var options = new DbContextOptionsBuilder<GameNetDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        await using (var db = new GameNetDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Customers.Add(new Customer
            {
                FullName = "Backup Customer",
                Code = "BACKUP-001",
                Username = "backup",
                ConcurrentLoginLimit = 1,
                VipTier = "none"
            });
            await db.SaveChangesAsync();
        }

        var keys = Path.Combine(_root, "App_Data", "DataProtection-Keys");
        Directory.CreateDirectory(keys);
        await File.WriteAllTextAsync(Path.Combine(keys, "test-key.xml"), "test");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:FileName"] = _dbPath,
                ["App:ProductVersion"] = "test"
            })
            .Build();

        var environment = new TestWebHostEnvironment(_root);
        await using var verifyDb = new GameNetDbContext(options);
        var backupService = new BackupService(environment, configuration, verifyDb);

        var created = await backupService.CreateBackupAsync("test", CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(_root, "App_Data", "Backups", created.FileName)));

        var verified = await backupService.VerifyAsync(created.FileName, CancellationToken.None);
        Assert.NotNull(verified);
        Assert.Single(verified!.AppliedMigrations);

        var prepared = await backupService.PrepareRestoreAsync(
            created.FileName,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(prepared.RequiresRestart);
        Assert.True(File.Exists(Path.Combine(_root, "App_Data", "restore.pending.json")));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch
        {
        }
    }

    private sealed class TestWebHostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "GameNetManager.Server.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
