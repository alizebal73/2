using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameNetManager.Client;

public sealed record ClientUpdatePackage(
    string Version,
    string PackageUrl,
    string Sha256,
    long? SizeBytes = null);

public sealed record ClientUpdateState(
    string? ActiveVersion,
    string? PreviousVersion,
    string? HealthyVersion,
    DateTimeOffset? ActivatedAt);

public sealed class ClientUpdateManager
{
    private readonly HttpClient _httpClient;
    private readonly string _dataDirectory;
    private readonly string _versionsDirectory;
    private readonly string _updatesDirectory;
    private readonly string _stateFilePath;
    private readonly string _activeStatePath;

    public ClientUpdateManager(HttpClient httpClient, string dataDirectory, string stateFilePath)
    {
        _httpClient = httpClient;
        _dataDirectory = dataDirectory;
        _versionsDirectory = Path.Combine(dataDirectory, "versions");
        _updatesDirectory = Path.Combine(dataDirectory, "updates");
        _stateFilePath = stateFilePath;
        _activeStatePath = Path.Combine(dataDirectory, "update-active.json");

        Directory.CreateDirectory(_versionsDirectory);
        Directory.CreateDirectory(_updatesDirectory);
    }

    public string VersionsDirectory => _versionsDirectory;

    public async Task<string> StageAsync(
        ClientUpdatePackage package,
        CancellationToken cancellationToken)
    {
        ValidateVersion(package.Version);

        var temporaryRoot = Path.Combine(
            _updatesDirectory,
            package.Version + "." + Guid.NewGuid().ToString("N") + ".staging");
        var zipPath = temporaryRoot + ".zip";

        Directory.CreateDirectory(temporaryRoot);

        try
        {
            using var response = await _httpClient.GetAsync(
                package.PackageUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(zipPath))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            var actualHash = await ComputeSha256Async(zipPath, cancellationToken);
            if (!actualHash.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("هش بستهٔ به‌روزرسانی با مقدار مورد انتظار یکسان نیست.");

            if (package.SizeBytes.HasValue && new FileInfo(zipPath).Length != package.SizeBytes.Value)
                throw new InvalidDataException("حجم بستهٔ به‌روزرسانی با مقدار مورد انتظار یکسان نیست.");

            var extractionRoot = Path.Combine(temporaryRoot, "payload");
            ZipFile.ExtractToDirectory(zipPath, extractionRoot, overwriteFiles: false);

            var clientAssembly = Directory
                .EnumerateFiles(extractionRoot, "GameNetManager.Client.dll", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(clientAssembly))
                throw new InvalidDataException("بستهٔ به‌روزرسانی فاقد GameNetManager.Client.dll است.");

            var versionMarker = Path.Combine(Path.GetDirectoryName(clientAssembly)!, "client-version.txt");
            if (!File.Exists(versionMarker))
                throw new InvalidDataException("بستهٔ به‌روزرسانی فاقد client-version.txt است.");

            var markedVersion = (await File.ReadAllTextAsync(versionMarker, cancellationToken)).Trim();
            ValidateVersion(markedVersion);
            if (!string.Equals(markedVersion, package.Version, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("نسخهٔ ثبت‌شده داخل بسته با نسخهٔ Manifest یکسان نیست.");

            var finalVersionRoot = Path.Combine(_versionsDirectory, package.Version);
            if (Directory.Exists(finalVersionRoot))
                Directory.Delete(finalVersionRoot, recursive: true);

            Directory.Move(extractionRoot, finalVersionRoot);

            return finalVersionRoot;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
            catch
            {
                // Cleanup is best-effort; the verified staged version is never deleted here.
            }
        }
    }

    public async Task EnsureCurrentVersionSnapshotAsync(
        string currentVersion,
        string currentInstallRoot,
        CancellationToken cancellationToken)
    {
        ValidateVersion(currentVersion);

        var target = Path.Combine(_versionsDirectory, currentVersion);
        if (Directory.Exists(target))
            return;

        var temporaryRoot = target + "." + Guid.NewGuid().ToString("N") + ".snapshot";
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            foreach (var sourceFile in Directory.EnumerateFiles(currentInstallRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(currentInstallRoot, sourceFile);
                if (relative.StartsWith("versions" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith("updates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;

                var destination = Path.Combine(temporaryRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = File.OpenRead(sourceFile);
                await using var targetStream = File.Create(destination);
                await source.CopyToAsync(targetStream, cancellationToken);
            }

            var versionMarker = Path.Combine(temporaryRoot, "client-version.txt");
            await File.WriteAllTextAsync(versionMarker, currentVersion, cancellationToken);
            Directory.Move(temporaryRoot, target);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                try
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup after cancellation/failure.
                }
            }
        }
    }

    public async Task ActivateAsync(string version, string? currentVersion, CancellationToken cancellationToken)
    {
        ValidateVersion(version);

        var versionRoot = Path.Combine(_versionsDirectory, version);
        if (!Directory.Exists(versionRoot))
            throw new DirectoryNotFoundException("نسخهٔ مورد نظر برای فعال‌سازی پیدا نشد.");

        var current = await LoadStateAsync(cancellationToken);
        var next = new ClientUpdateState(
            version,
            string.IsNullOrWhiteSpace(currentVersion)
                ? current?.ActiveVersion
                : currentVersion,
            current?.HealthyVersion,
            DateTimeOffset.UtcNow);

        await SaveStateAsync(next, cancellationToken);
    }

    public async Task MarkHealthyAsync(string version, CancellationToken cancellationToken)
    {
        ValidateVersion(version);
        var current = await LoadStateAsync(cancellationToken)
            ?? new ClientUpdateState(version, null, null, DateTimeOffset.UtcNow);

        var next = new ClientUpdateState(
            current.ActiveVersion ?? version,
            current.PreviousVersion,
            version,
            current.ActivatedAt);

        await SaveStateAsync(next, cancellationToken);
    }

    public async Task CommitHealthyAsync(string version, CancellationToken cancellationToken)
    {
        ValidateVersion(version);
        var current = await LoadStateAsync(cancellationToken)
            ?? throw new InvalidOperationException("وضعیت Update برای تثبیت سلامت وجود ندارد.");

        if (!string.Equals(current.ActiveVersion, version, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("نسخهٔ فعال با نسخهٔ سلامت همخوانی ندارد.");

        var next = new ClientUpdateState(
            current.ActiveVersion,
            current.PreviousVersion,
            version,
            current.ActivatedAt);

        await SaveStateAsync(next, cancellationToken);
    }

    public async Task<string> RollbackAsync(CancellationToken cancellationToken)
    {
        var current = await LoadStateAsync(cancellationToken)
            ?? throw new InvalidOperationException("وضعیت Update برای Rollback وجود ندارد.");

        if (string.IsNullOrWhiteSpace(current.PreviousVersion))
            throw new InvalidOperationException("نسخهٔ قبلی برای Rollback وجود ندارد.");

        ValidateVersion(current.PreviousVersion);

        var previousRoot = Path.Combine(_versionsDirectory, current.PreviousVersion);
        if (!Directory.Exists(previousRoot))
            throw new DirectoryNotFoundException("نسخهٔ قبلی برای Rollback در دسترس نیست.");

        var next = new ClientUpdateState(
            current.PreviousVersion,
            null,
            current.PreviousVersion,
            DateTimeOffset.UtcNow);

        await SaveStateAsync(next, cancellationToken);
        return current.PreviousVersion;
    }

    public async Task<ClientUpdateState?> GetStateAsync(CancellationToken cancellationToken)
        => await LoadStateAsync(cancellationToken);

    private async Task<ClientUpdateState?> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_activeStatePath))
            return null;

        var json = await File.ReadAllTextAsync(_activeStatePath, cancellationToken);
        return JsonSerializer.Deserialize<ClientUpdateState>(json);
    }

    private async Task SaveStateAsync(ClientUpdateState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            state,
            new JsonSerializerOptions { WriteIndented = true });

        var temporaryPath = _activeStatePath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);

        try
        {
            File.Move(temporaryPath, _activeStatePath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the last known active version pointer.
            }

            throw;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)
            || version.Length > 64
            || version.Contains(Path.DirectorySeparatorChar)
            || version.Contains(Path.AltDirectorySeparatorChar)
            || version.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("نسخهٔ به‌روزرسانی معتبر نیست.", nameof(version));
    }
}
