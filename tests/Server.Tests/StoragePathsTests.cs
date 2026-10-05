using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace GameNetManager.Server.Tests;

public sealed class StoragePathsTests
{
    [Fact]
    public void ProductionWithoutExplicitRoot_UsesCommonApplicationData()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestEnvironment("Production", Path.Combine(Path.GetTempPath(), "GameNetManagerTestContentRoot", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(environment.ContentRootPath);
        var root = GameNetManager.Server.Data.StoragePaths.ResolveDataRoot(configuration, environment);

        Assert.EndsWith(
            Path.Combine("GameNetManager"),
            root,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitDataRoot_IsUsedForProductionDatabaseAndKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameNetManagerStorageTests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:DataRoot"] = root,
                ["Database:FileName"] = "App_Data/gamenet.production.db"
            })
            .Build();
        var environment = new TestEnvironment("Production", Path.Combine(Path.GetTempPath(), "GameNetManagerTestContentRoot", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(environment.ContentRootPath);

        try
        {
            var resolvedRoot = GameNetManager.Server.Data.StoragePaths.ResolveDataRoot(configuration, environment);
            var database = GameNetManager.Server.Data.StoragePaths.ResolveDatabasePath(configuration, environment, resolvedRoot);
            var keys = GameNetManager.Server.Data.StoragePaths.ResolveDataProtectionKeysPath(resolvedRoot);
            var recovery = GameNetManager.Server.Data.StoragePaths.ResolveBackupRecoveryRoot(resolvedRoot);

            Assert.Equal(Path.GetFullPath(root), resolvedRoot);
            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), "gamenet.production.db"),
                database);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "DataProtection-Keys"), keys);
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "BackupRecovery"), recovery);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
            try { Directory.Delete(environment.ContentRootPath, recursive: true); } catch { }
        }
    }

    private sealed class TestEnvironment(string environmentName, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GameNetManager.Server.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
