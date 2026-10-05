using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameNetManager.Server.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateVerifyAndRestoreBackup_RestoresDatabaseAndKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameNetManagerBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "App_Data", "gamenet.db");
        var backupTarget = Path.Combine(root, "Backups");
        var keysPath = Path.Combine(root, "App_Data", "DataProtection-Keys");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        Directory.CreateDirectory(keysPath);
        await File.WriteAllTextAsync(Path.Combine(keysPath, "key.xml"), "<key id=\"backup-test\" />");

        try
        {
            var options = new DbContextOptionsBuilder<GameNetManager.Server.Data.GameNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var seed = new GameNetManager.Server.Data.GameNetDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.Customers.Add(new GameNetManager.Server.Data.Customer
                {
                    FullName = "مشتری قبل از Restore",
                    Code = "BACKUP-1",
                    Balance = 150000
                });
                seed.AppSettings.Add(new GameNetManager.Server.Data.AppSetting
                {
                    Key = "backupTarget",
                    ScopeKey = GameNetManager.Server.Data.ServerSettingsCatalog.GlobalScope,
                    ValueJson = JsonSerializer.Serialize(backupTarget)
                });
                seed.AppSettings.Add(new GameNetManager.Server.Data.AppSetting
                {
                    Key = "backupKeep",
                    ScopeKey = GameNetManager.Server.Data.ServerSettingsCatalog.GlobalScope,
                    ValueJson = "5"
                });
                await seed.SaveChangesAsync();
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:FileName"] = databasePath,
                    ["App:ProductVersion"] = "test"
                })
                .Build();

            var environment = new TestHostEnvironment(root);
            string backupFileName;

            await using (var database = new GameNetManager.Server.Data.GameNetDbContext(options))
            {
                var service = new GameNetManager.Server.Data.DatabaseBackupService(
                    database,
                    configuration,
                    environment,
                    NullLogger<GameNetManager.Server.Data.DatabaseBackupService>.Instance);

                var backup = await service.CreateBackupAsync(CancellationToken.None);
                backupFileName = backup.FileName;
                Assert.True(backup.FileName.EndsWith(".gnbackup", StringComparison.OrdinalIgnoreCase));
                Assert.False(string.IsNullOrWhiteSpace(backup.Sha256));

                var verified = await service.VerifyBackupAsync(backup.FileName, CancellationToken.None);
                Assert.True(verified.Valid, verified.Message);

                database.Customers.Single(item => item.Code == "BACKUP-1").FullName = "مشتری بعد از Restore";
                await database.SaveChangesAsync();

                var prepared = await service.PrepareRestoreAsync(
                    backup.FileName,
                    Guid.NewGuid(),
                    CancellationToken.None);

                Assert.True(prepared.Pending);
            }

            var applied = await GameNetManager.Server.Data.DatabaseBackupService.ApplyPendingRestoreAsync(
                databasePath,
                keysPath,
                root,
                NullLogger.Instance);

            Assert.True(applied);
            Assert.True(File.Exists(Path.Combine(keysPath, "key.xml")));

            await using (var restored = new GameNetManager.Server.Data.GameNetDbContext(options))
            {
                var customer = await restored.Customers.SingleAsync(item => item.Code == "BACKUP-1");
                Assert.Equal("مشتری قبل از Restore", customer.FullName);
                Assert.Equal(150000m, customer.Balance);
            }

            Assert.True(File.Exists(Path.Combine(backupTarget, backupFileName)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            EnvironmentName = "Test";
            ApplicationName = "GameNetManager.Server.Tests";
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
