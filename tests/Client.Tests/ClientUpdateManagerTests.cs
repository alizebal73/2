using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using GameNetManager.Client;

namespace GameNetManager.Client.Tests;

public sealed class ClientUpdateManagerTests
{
    [Fact]
    public async Task StageActivateAndRollback_preserves_versions_and_integrity()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageBytes = CreatePackage("2.0.0");
            var hash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
            using var httpClient = new HttpClient(new InMemoryHandler(packageBytes))
            {
                BaseAddress = new Uri("https://test.local/")
            };

            var statePath = Path.Combine(root, "agent-state.json");
            var manager = new ClientUpdateManager(httpClient, root, statePath);

            var staged = await manager.StageAsync(
                new ClientUpdatePackage("2.0.0", "https://test.local/client.zip", hash, packageBytes.Length),
                CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(staged, "GameNetManager.Client.dll")));
            Assert.Equal("2.0.0", (await File.ReadAllTextAsync(Path.Combine(staged, "client-version.txt"))).Trim());

            await manager.ActivateAsync("2.0.0", "1.0.0", CancellationToken.None);
            var active = await manager.GetStateAsync(CancellationToken.None);
            Assert.Equal("2.0.0", active!.ActiveVersion);
            Assert.Equal("1.0.0", active.PreviousVersion);

            await manager.MarkHealthyAsync("2.0.0", CancellationToken.None);
            var healthy = await manager.GetStateAsync(CancellationToken.None);
            Assert.Equal("2.0.0", healthy!.HealthyVersion);
            Assert.Equal("1.0.0", healthy.PreviousVersion);

            await manager.RollbackAsync(CancellationToken.None);
            var rolledBack = await manager.GetStateAsync(CancellationToken.None);
            Assert.Equal("1.0.0", rolledBack!.ActiveVersion);
            Assert.Equal("2.0.0", rolledBack.PreviousVersion);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Stage_rejects_hash_mismatch_without_installing_version()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageBytes = CreatePackage("3.0.0");
            using var httpClient = new HttpClient(new InMemoryHandler(packageBytes));
            var manager = new ClientUpdateManager(httpClient, root, Path.Combine(root, "agent-state.json"));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                manager.StageAsync(
                    new ClientUpdatePackage(
                        "3.0.0",
                        "https://test.local/client.zip",
                        new string('0', 64),
                        packageBytes.Length),
                    CancellationToken.None));

            Assert.False(Directory.Exists(Path.Combine(root, "versions", "3.0.0")));
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task Snapshot_current_version_is_version_isolated()
    {
        var root = CreateTempDirectory();
        try
        {
            var install = Path.Combine(root, "install");
            Directory.CreateDirectory(install);
            await File.WriteAllTextAsync(Path.Combine(install, "GameNetManager.Client.dll"), "current");
            await File.WriteAllTextAsync(Path.Combine(install, "support.txt"), "support");

            using var httpClient = new HttpClient();
            var manager = new ClientUpdateManager(httpClient, root, Path.Combine(root, "agent-state.json"));

            await manager.EnsureCurrentVersionSnapshotAsync("1.0.0", install, CancellationToken.None);

            var snapshot = Path.Combine(root, "versions", "1.0.0");
            Assert.Equal("current", await File.ReadAllTextAsync(Path.Combine(snapshot, "GameNetManager.Client.dll")));
            Assert.Equal("1.0.0", (await File.ReadAllTextAsync(Path.Combine(snapshot, "client-version.txt"))).Trim());
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static byte[] CreatePackage(string version)
    {
        var root = CreateTempDirectory();
        var dll = Path.Combine(root, "GameNetManager.Client.dll");
        var marker = Path.Combine(root, "client-version.txt");

        try
        {
            File.WriteAllText(dll, "test-client");
            File.WriteAllText(marker, version);

            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                archive.CreateEntryFromFile(dll, "GameNetManager.Client.dll");
                archive.CreateEntryFromFile(marker, "client-version.txt");
            }

            return stream.ToArray();
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "gamenet-client-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class InMemoryHandler(byte[] packageBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(packageBytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            return Task.FromResult(response);
        }
    }
}
