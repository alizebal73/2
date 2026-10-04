using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record BackupSettings(
    bool Enabled = true,
    string Hour = "04:00",
    int Keep = 30,
    string TargetDirectory = "App_Data/Backups",
    string? LastAutoBackupDate = null);

public sealed record BackupManifest(
    string ProductVersion,
    string DatabaseFileName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> AppliedMigrations,
    string DatabaseSha256);

public sealed record BackupFileDto(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    string ProductVersion,
    string DatabaseSha256,
    string ArchiveSha256,
    IReadOnlyList<string> AppliedMigrations);

public sealed record RestorePreparationResult(
    string FileName,
    DateTimeOffset PreparedAt,
    bool RequiresRestart,
    string Message);

public sealed class BackupService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    GameNetDbContext database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string AppDataDirectory => Path.Combine(environment.ContentRootPath, "App_Data");

    public string DatabasePath
    {
        get
        {
            var configured = configuration["Database:FileName"] ?? "App_Data/gamenet.db";
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);
        }
    }

    public string DataProtectionKeysDirectory => Path.Combine(AppDataDirectory, "DataProtection-Keys");
    public string RestoreCandidatesDirectory => Path.Combine(AppDataDirectory, "RestoreCandidates");
    public string SettingsPath => Path.Combine(AppDataDirectory, "backup-settings.json");
    public string PendingRestorePath => Path.Combine(AppDataDirectory, "restore.pending.json");

    public async Task<BackupSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppDataDirectory);
        if (!File.Exists(SettingsPath))
            return new BackupSettings();

        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<BackupSettings>(stream, JsonOptions, cancellationToken)
            ?? new BackupSettings();
    }

    public async Task SaveSettingsAsync(
        bool enabled,
        string hour,
        int keep,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        if (!TimeOnly.TryParse(hour, out _))
            throw new ArgumentException("ساعت بکاپ معتبر نیست.");
        if (keep < 1 || keep > 3650)
            throw new ArgumentException("تعداد نسخه‌های نگهداری باید بین ۱ تا ۳۶۵۰ باشد.");

        var directory = string.IsNullOrWhiteSpace(targetDirectory)
            ? "App_Data/Backups"
            : targetDirectory.Trim();

        var settings = new BackupSettings(enabled, hour.Trim(), keep, directory, (await GetSettingsAsync(cancellationToken)).LastAutoBackupDate);
        await WriteSettingsAsync(settings, cancellationToken);
    }

    public async Task<BackupFileDto> CreateBackupAsync(
        string? productVersion,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(DatabasePath))
            throw new FileNotFoundException("دیتابیس سرور پیدا نشد.", DatabasePath);

        var settings = await GetSettingsAsync(cancellationToken);
        var backupDirectory = ResolveBackupDirectory(settings.TargetDirectory);
        Directory.CreateDirectory(backupDirectory);

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var archiveName = $"gamenet-backup-{stamp}-{Guid.NewGuid():N}.zip";
        var archivePath = Path.Combine(backupDirectory, archiveName);
        var tempRoot = Path.Combine(Path.GetTempPath(), "GameNetBackup", Guid.NewGuid().ToString("N"));
        var tempDb = Path.Combine(tempRoot, "gamenet.db");
        var tempKeys = Path.Combine(tempRoot, "DataProtection-Keys");
        var tempManifest = Path.Combine(tempRoot, "manifest.json");

        Directory.CreateDirectory(tempRoot);
        try
        {
            var applied = (await database.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
            {
                await connection.OpenAsync(cancellationToken);
                var safeTempDb = tempDb.Replace("'", "''", StringComparison.Ordinal);
                await using var command = connection.CreateCommand();
                command.CommandText = $"VACUUM INTO '{safeTempDb}'";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (!File.Exists(tempDb))
                throw new InvalidOperationException("فایل پایدار دیتابیس برای Backup ساخته نشد.");

            Directory.CreateDirectory(tempKeys);
            if (Directory.Exists(DataProtectionKeysDirectory))
                CopyDirectory(DataProtectionKeysDirectory, tempKeys);

            var databaseHash = await Sha256FileAsync(tempDb, cancellationToken);
            var manifest = new BackupManifest(
                productVersion ?? configuration["App:ProductVersion"] ?? "unknown",
                Path.GetFileName(DatabasePath),
                DateTimeOffset.UtcNow,
                applied,
                databaseHash);

            await File.WriteAllTextAsync(
                tempManifest,
                JsonSerializer.Serialize(manifest, JsonOptions),
                Encoding.UTF8,
                cancellationToken);

            ZipFile.CreateFromDirectory(tempRoot, archivePath, CompressionLevel.Optimal, false);

            var verified = await VerifyBackupAsync(archivePath, cancellationToken);
            if (verified is null)
                throw new InvalidOperationException("Backup پس از ساخت قابل اعتبارسنجی نیست.");

            await PruneAsync(backupDirectory, settings.Keep, cancellationToken);
            await SetLastAutoBackupDateAsync(DateTimeOffset.Now.Date, settings, cancellationToken);

            return verified;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public async Task<IReadOnlyList<BackupFileDto>> ListAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var directory = ResolveBackupDirectory(settings.TargetDirectory);
        if (!Directory.Exists(directory))
            return Array.Empty<BackupFileDto>();

        var result = new List<BackupFileDto>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(item => item, StringComparer.OrdinalIgnoreCase)
                     .Take(100))
        {
            var manifest = await ReadManifestAsync(file, cancellationToken);
            if (manifest is null)
                continue;

            result.Add(new BackupFileDto(
                Path.GetFileName(file),
                new FileInfo(file).Length,
                manifest.CreatedAt,
                manifest.ProductVersion,
                manifest.DatabaseSha256,
                await Sha256FileAsync(file, cancellationToken),
                manifest.AppliedMigrations));
        }

        return result;
    }

    public async Task<BackupFileDto?> VerifyAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = await ResolveBackupFileAsync(fileName, cancellationToken);
        return await VerifyBackupAsync(path, cancellationToken);
    }

    public async Task<RestorePreparationResult> PrepareRestoreAsync(
        string fileName,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var source = await ResolveBackupFileAsync(fileName, cancellationToken);
        var verified = await VerifyBackupAsync(source, cancellationToken)
            ?? throw new InvalidOperationException("Backup قابل بازیابی نیست.");

        Directory.CreateDirectory(RestoreCandidatesDirectory);
        var candidate = Path.Combine(RestoreCandidatesDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(candidate);

        ZipFile.ExtractToDirectory(source, candidate, true);

        var candidateDb = Path.Combine(candidate, Path.GetFileName(DatabasePath));
        var candidateKeys = Path.Combine(candidate, "DataProtection-Keys");
        if (!File.Exists(candidateDb) || !Directory.Exists(candidateKeys))
        {
            TryDeleteDirectory(candidate);
            throw new InvalidOperationException("Backup شامل دیتابیس یا کلیدهای DataProtection نیست.");
        }

        var marker = new
        {
            sourceFile = Path.GetFullPath(source),
            candidateDirectory = Path.GetFullPath(candidate),
            databaseFileName = Path.GetFileName(DatabasePath),
            preparedAt = DateTimeOffset.UtcNow,
            requestedByUserId = appUserId,
            verifiedArchive = verified
        };

        await File.WriteAllTextAsync(
            PendingRestorePath,
            JsonSerializer.Serialize(marker, JsonOptions),
            Encoding.UTF8,
            cancellationToken);

        return new RestorePreparationResult(
            Path.GetFileName(source),
            DateTimeOffset.UtcNow,
            true,
            "بازیابی با موفقیت اعتبارسنجی و برای راه‌اندازی بعدی آماده شد.");
    }

    public async Task SaveAutoSettingsAsync(BackupSettings settings, CancellationToken cancellationToken)
        => await WriteSettingsAsync(settings, cancellationToken);

    public static async Task ApplyPendingRestoreAsync(
        string contentRootPath,
        string databasePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var appData = Path.Combine(contentRootPath, "App_Data");
        var markerPath = Path.Combine(appData, "restore.pending.json");
        if (!File.Exists(markerPath))
            return;

        try
        {
            await using var stream = File.OpenRead(markerPath);
            var marker = await JsonSerializer.DeserializeAsync<PendingRestoreMarker>(stream, JsonOptions, cancellationToken);
            if (marker is null)
                throw new InvalidOperationException("نشانگر Restore معتبر نیست.");

            var candidateDb = Path.Combine(marker.CandidateDirectory, marker.DatabaseFileName);
            var candidateKeys = Path.Combine(marker.CandidateDirectory, "DataProtection-Keys");
            if (!File.Exists(candidateDb) || !Directory.Exists(candidateKeys))
                throw new FileNotFoundException("Restore candidate ناقص است.");

            var currentBackupDir = Path.Combine(appData, "Backups");
            Directory.CreateDirectory(currentBackupDir);
            if (File.Exists(databasePath))
            {
                var safety = Path.Combine(currentBackupDir, $"pre-restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
                File.Copy(databasePath, safety, true);
            }

            var dbDirectory = Path.GetDirectoryName(databasePath)!;
            Directory.CreateDirectory(dbDirectory);
            File.Copy(candidateDb, databasePath, true);

            var liveKeys = Path.Combine(appData, "DataProtection-Keys");
            TryDeleteDirectory(liveKeys);
            CopyDirectory(candidateKeys, liveKeys);

            TryDeleteDirectory(marker.CandidateDirectory);
            File.Delete(markerPath);
            logger.LogWarning("Pending GameNet restore applied from {SourceFile}.", marker.SourceFile);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Pending GameNet restore could not be applied.");
            throw;
        }
    }

    private async Task<BackupFileDto?> VerifyBackupAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        var temp = Path.Combine(Path.GetTempPath(), "GameNetBackupVerify", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(path, temp, true);

            var manifestPath = Path.Combine(temp, "manifest.json");
            var dbPath = Path.Combine(temp, Path.GetFileName(DatabasePath));
            var keysPath = Path.Combine(temp, "DataProtection-Keys");
            if (!File.Exists(manifestPath) || !File.Exists(dbPath) || !Directory.Exists(keysPath))
                return null;

            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken);
            if (manifest is null)
                return null;

            var dbHash = await Sha256FileAsync(dbPath, cancellationToken);
            if (!string.Equals(dbHash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            var options = new DbContextOptionsBuilder<GameNetDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            await using var verifyDatabase = new GameNetDbContext(options);
            var applied = (await verifyDatabase.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!manifest.AppliedMigrations.All(applied.Contains))
                return null;

            return new BackupFileDto(
                Path.GetFileName(path),
                new FileInfo(path).Length,
                manifest.CreatedAt,
                manifest.ProductVersion,
                manifest.DatabaseSha256,
                await Sha256FileAsync(path, cancellationToken),
                manifest.AppliedMigrations);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    private string ResolveBackupDirectory(string configured)
    {
        var path = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        return Path.GetFullPath(path);
    }

    private async Task<string> ResolveBackupFileAsync(string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("نام فایل Backup الزامی است.");

        var settings = await GetSettingsAsync(cancellationToken);
        var directory = ResolveBackupDirectory(settings.TargetDirectory);
        var full = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!string.Equals(Path.GetDirectoryName(full), directory, StringComparison.OrdinalIgnoreCase)
            || !full.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("فایل Backup نامعتبر است.");
        return full;
    }

    private async Task<BackupManifest?> ReadManifestAsync(string archivePath, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), "GameNetBackupManifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temp, true);
            var path = Path.Combine(temp, "manifest.json");
            if (!File.Exists(path))
                return null;

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    private async Task PruneAsync(string directory, int keep, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var files = Directory.EnumerateFiles(directory, "*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(item => File.GetCreationTimeUtc(item))
            .Skip(Math.Max(keep, 1))
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Delete(file); } catch { }
        }
    }

    private async Task WriteSettingsAsync(BackupSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppDataDirectory);
        var temp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(settings, JsonOptions), Encoding.UTF8, cancellationToken);
        File.Move(temp, SettingsPath, true);
    }

    private async Task SetLastAutoBackupDateAsync(DateTime date, BackupSettings settings, CancellationToken cancellationToken)
    {
        await WriteSettingsAsync(settings with { LastAutoBackupDate = date.ToString("yyyy-MM-dd") }, cancellationToken);
    }

    private static async Task<string> Sha256FileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, target, StringComparison.OrdinalIgnoreCase));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target, StringComparison.OrdinalIgnoreCase), true);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch { }
    }

    private sealed record PendingRestoreMarker(
        string SourceFile,
        string CandidateDirectory,
        string DatabaseFileName,
        DateTimeOffset PreparedAt,
        Guid RequestedByUserId,
        BackupFileDto VerifiedArchive);
}
