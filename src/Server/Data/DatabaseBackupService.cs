using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record BackupFileDto(
    string FileName,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    string Sha256,
    bool Verified,
    string? VerificationMessage);

public sealed record BackupRestoreRequestResultDto(
    bool Pending,
    string FileName,
    string Message,
    DateTimeOffset RequestedAt);

public sealed class DatabaseBackupService
{
    private const string ArchiveExtension = ".gnbackup";
    private const string DatabaseEntry = "database/gamenet.db";
    private const string KeysPrefix = "keys/";
    private const string ManifestEntry = "manifest.json";
    private readonly GameNetDbContext _database;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        GameNetDbContext database,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<DatabaseBackupService> logger)
    {
        _database = database;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<BackupFileDto> CreateBackupAsync(CancellationToken cancellationToken)
    {
        var target = await GetBackupTargetAsync(cancellationToken);
        Directory.CreateDirectory(target);
        var archivePath = BuildArchivePath(target, DateTimeOffset.UtcNow);
        await CreateArchiveFromLiveDatabaseAsync(
            ResolveDatabasePath(),
            ResolveDataProtectionKeysPath(),
            archivePath,
            _configuration["App:ProductVersion"] ?? "0.0.0",
            cancellationToken);

        await ApplyRetentionAsync(target, await GetBackupKeepAsync(cancellationToken));
        return await DescribeBackupAsync(archivePath, verify: true, cancellationToken);
    }

    public async Task<IReadOnlyList<BackupFileDto>> ListBackupsAsync(CancellationToken cancellationToken)
    {
        var target = await GetBackupTargetAsync(cancellationToken);
        Directory.CreateDirectory(target);
        var files = Directory
            .EnumerateFiles(target, "*" + ArchiveExtension, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        var result = new List<BackupFileDto>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await DescribeBackupAsync(file, verify: false, cancellationToken));
        }

        return result;
    }

    public async Task<(bool Valid, string Message)> VerifyBackupAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            var archive = await ResolveArchivePathAsync(fileName, cancellationToken);
            await ValidateArchiveAsync(archive, cancellationToken);
            return (true, "نسخهٔ پشتیبان سالم و قابل‌بازیابی است.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Backup verification failed for {BackupFile}", fileName);
            return (false, $"اعتبارسنجی پشتیبان ناموفق بود: {exception.Message}");
        }
    }

    public async Task<BackupRestoreRequestResultDto> PrepareRestoreAsync(
        string fileName,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var archive = await ResolveArchivePathAsync(fileName, cancellationToken);
        await ValidateArchiveAsync(archive, cancellationToken);

        var recoveryRoot = Path.Combine(_environment.ContentRootPath, "App_Data", "BackupRecovery");
        var pendingRoot = Path.Combine(recoveryRoot, "Pending");
        Directory.CreateDirectory(pendingRoot);

        var restoreId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(pendingRoot, restoreId);
        Directory.CreateDirectory(staging);
        await ExtractArchiveAsync(archive, staging, cancellationToken);

        var requestedAt = DateTimeOffset.UtcNow;
        var marker = new PendingRestoreMarker(
            fileName,
            restoreId,
            requestedAt,
            requestedByUserId);

        var markerPath = Path.Combine(recoveryRoot, "pending.json");
        var markerTempPath = markerPath + ".tmp";
        await File.WriteAllTextAsync(
            markerTempPath,
            JsonSerializer.Serialize(marker, JsonOptions),
            cancellationToken);
        File.Move(markerTempPath, markerPath, overwrite: true);

        return new BackupRestoreRequestResultDto(
            true,
            fileName,
            "بازیابی آماده شد و در راه‌اندازی بعدی Server اعمال می‌شود. قبل از راه‌اندازی مجدد، عملیات مالی جدید متوقف شود.",
            requestedAt);
    }

    public static async Task<bool> ApplyPendingRestoreAsync(
        string databasePath,
        string dataProtectionKeysPath,
        string contentRootPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var recoveryRoot = Path.Combine(contentRootPath, "App_Data", "BackupRecovery");
        var markerPath = Path.Combine(recoveryRoot, "pending.json");
        if (!File.Exists(markerPath))
            return false;

        var marker = JsonSerializer.Deserialize<PendingRestoreMarker>(
            await File.ReadAllTextAsync(markerPath, cancellationToken),
            JsonOptions);

        if (marker is null || string.IsNullOrWhiteSpace(marker.StagingDirectory))
            throw new InvalidOperationException("فایل درخواست بازیابی معتبر نیست.");

        var staging = Path.Combine(recoveryRoot, "Pending", marker.StagingDirectory);
        var restoredDatabasePath = Path.Combine(staging, "database", "gamenet.db");
        var restoredKeysPath = Path.Combine(staging, "keys");

        if (!File.Exists(restoredDatabasePath) || !Directory.Exists(restoredKeysPath))
            throw new InvalidOperationException("فایل‌های لازم برای بازیابی پیدا نشدند.");

        await VerifyDatabaseFileAsync(restoredDatabasePath, cancellationToken);

        var preRestoreRoot = Path.Combine(recoveryRoot, "PreRestore");
        Directory.CreateDirectory(preRestoreRoot);
        if (File.Exists(databasePath))
        {
            var safetyArchive = Path.Combine(
                preRestoreRoot,
                $"pre-restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.gnbackup");

            await CreateClosedDatabaseArchiveAsync(
                databasePath,
                dataProtectionKeysPath,
                safetyArchive,
                cancellationToken);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection.ClearAllPools();

        var oldDatabasePath = databasePath + ".restore-old";
        var oldKeysPath = dataProtectionKeysPath + ".restore-old";

        try
        {
            DeleteIfExists(oldDatabasePath);
            DeleteDirectoryIfExists(oldKeysPath);

            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");

            if (File.Exists(databasePath))
                File.Move(databasePath, oldDatabasePath);

            if (Directory.Exists(dataProtectionKeysPath))
                Directory.Move(dataProtectionKeysPath, oldKeysPath);

            File.Move(restoredDatabasePath, databasePath);

            Directory.Move(restoredKeysPath, dataProtectionKeysPath);

            DeleteIfExists(oldDatabasePath);
            DeleteDirectoryIfExists(oldKeysPath);
            DeleteDirectoryIfExists(staging);
            DeleteIfExists(markerPath);
        }
        catch
        {
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");

            if (!File.Exists(databasePath) && File.Exists(oldDatabasePath))
                File.Move(oldDatabasePath, databasePath);

            if (!Directory.Exists(dataProtectionKeysPath) && Directory.Exists(oldKeysPath))
                Directory.Move(oldKeysPath, dataProtectionKeysPath);

            throw;
        }

        logger.LogWarning(
            "Backup restore applied from {BackupFile} by user {UserId}. Server will continue with restored database.",
            marker.FileName,
            marker.RequestedByUserId);

        return true;
    }

    private async Task<string> GetBackupTargetAsync(CancellationToken cancellationToken)
    {
        var stored = await _database.AppSettings
            .AsNoTracking()
            .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
            .ToListAsync(cancellationToken);

        var values = ServerSettingsCatalog.BuildValues(stored);
        var configured = values.TryGetValue("backupTarget", out var element)
            ? element.GetString()
            : null;

        var target = string.IsNullOrWhiteSpace(configured)
            ? "App_Data/Backups"
            : configured!.Trim();

        return Path.IsPathRooted(target)
            ? Path.GetFullPath(target)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, target));
    }

    private async Task<int> GetBackupKeepAsync(CancellationToken cancellationToken)
    {
        var stored = await _database.AppSettings
            .AsNoTracking()
            .Where(item => item.ScopeKey == ServerSettingsCatalog.GlobalScope)
            .ToListAsync(cancellationToken);

        var values = ServerSettingsCatalog.BuildValues(stored);
        if (!values.TryGetValue("backupKeep", out var element) || !element.TryGetInt32(out var keep))
            return 30;

        return Math.Clamp(keep, 1, 3650);
    }

    private string ResolveDatabasePath()
    {
        var configured = _configuration["Database:FileName"] ?? "App_Data/gamenet.db";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private string ResolveDataProtectionKeysPath()
        => Path.Combine(_environment.ContentRootPath, "App_Data", "DataProtection-Keys");

    private static string BuildArchivePath(string target, DateTimeOffset now)
        => Path.Combine(
            target,
            $"gamenet-{now:yyyyMMdd-HHmmss-fff}.gnbackup");

    private static async Task CreateArchiveFromLiveDatabaseAsync(
        string databasePath,
        string keysPath,
        string archivePath,
        string productVersion,
        CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "GameNetManagerBackup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var stagedDatabasePath = Path.Combine(stagingRoot, "database", "gamenet.db");
            var stagedKeysPath = Path.Combine(stagingRoot, "keys");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedDatabasePath)!);
            Directory.CreateDirectory(stagedKeysPath);

            await BackupDatabaseFileAsync(databasePath, stagedDatabasePath, cancellationToken);
            await VerifyDatabaseFileAsync(stagedDatabasePath, cancellationToken);
            CopyDirectory(keysPath, stagedKeysPath);

            await CreateArchiveAsync(
                stagedDatabasePath,
                stagedKeysPath,
                archivePath,
                productVersion,
                cancellationToken);
        }
        finally
        {
            DeleteDirectoryIfExists(stagingRoot);
        }
    }

    private static async Task CreateClosedDatabaseArchiveAsync(
        string databasePath,
        string keysPath,
        string archivePath,
        CancellationToken cancellationToken)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "GameNetManagerPreRestore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);

        try
        {
            var stagedDatabasePath = Path.Combine(stagingRoot, "database", "gamenet.db");
            var stagedKeysPath = Path.Combine(stagingRoot, "keys");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedDatabasePath)!);
            Directory.CreateDirectory(stagedKeysPath);

            await BackupDatabaseFileAsync(databasePath, stagedDatabasePath, cancellationToken);
            await VerifyDatabaseFileAsync(stagedDatabasePath, cancellationToken);
            CopyDirectory(keysPath, stagedKeysPath);

            await CreateArchiveAsync(
                stagedDatabasePath,
                stagedKeysPath,
                archivePath,
                "pre-restore",
                cancellationToken);
        }
        finally
        {
            DeleteDirectoryIfExists(stagingRoot);
        }
    }

    private static async Task BackupDatabaseFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("فایل دیتابیس پیدا نشد.", sourcePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var source = new SqliteConnection($"Data Source={sourcePath}");
        await using var destination = new SqliteConnection($"Data Source={destinationPath}");

        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task VerifyDatabaseFileAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (!string.Equals(Convert.ToString(result), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SQLite integrity_check ناموفق بود: {result}");
    }

    private static async Task CreateArchiveAsync(
        string databasePath,
        string keysPath,
        string archivePath,
        string productVersion,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        var tempArchive = archivePath + ".tmp";
        DeleteIfExists(tempArchive);

        var databaseSha = await ComputeSha256Async(databasePath, cancellationToken);
        var manifest = new BackupManifest(
            1,
            DateTimeOffset.UtcNow,
            productVersion,
            new FileInfo(databasePath).Length,
            databaseSha);

        await using (var archiveStream = new FileStream(
            tempArchive,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        await using (var archive = new ZipArchive(
            archiveStream,
            ZipArchiveMode.Create,
            leaveOpen: false))
        {
            AddFileToArchive(archive, databasePath, DatabaseEntry);
            foreach (var keyFile in Directory.EnumerateFiles(keysPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(keysPath, keyFile).Replace(Path.DirectorySeparatorChar, '/');
                AddFileToArchive(archive, keyFile, KeysPrefix + relative);
            }

            var manifestEntry = archive.CreateEntry(ManifestEntry, CompressionLevel.Fastest);
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
        }

        File.Move(tempArchive, archivePath, overwrite: true);
    }

    private static async Task ValidateArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("نسخهٔ پشتیبان پیدا نشد.", archivePath);

        await using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var manifestEntry = archive.GetEntry(ManifestEntry)
            ?? throw new InvalidDataException("manifest.json در نسخهٔ پشتیبان وجود ندارد.");
        var databaseEntry = archive.GetEntry(DatabaseEntry)
            ?? throw new InvalidDataException("فایل دیتابیس در نسخهٔ پشتیبان وجود ندارد.");

        var keyEntry = archive.Entries.FirstOrDefault(item =>
            item.FullName.StartsWith(KeysPrefix, StringComparison.OrdinalIgnoreCase)
            && !item.FullName.EndsWith("/", StringComparison.Ordinal));

        if (keyEntry is null)
            throw new InvalidDataException("کلیدهای DataProtection در نسخهٔ پشتیبان وجود ندارند.");

        BackupManifest? manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                manifestStream,
                JsonOptions,
                cancellationToken);
        }

        if (manifest is null || manifest.Version != 1)
            throw new InvalidDataException("نسخهٔ manifest پشتیبان پشتیبانی نمی‌شود.");

        var temp = Path.Combine(Path.GetTempPath(), "GameNetManagerVerify", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
        try
        {
            await using var dbStream = databaseEntry.Open();
            await using var fileStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await dbStream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            var hash = await ComputeSha256Async(temp, cancellationToken);
            if (!string.Equals(hash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("هش دیتابیس با manifest پشتیبان برابر نیست.");

            if (new FileInfo(temp).Length != manifest.DatabaseSizeBytes)
                throw new InvalidDataException("اندازهٔ دیتابیس با manifest پشتیبان برابر نیست.");

            await VerifyDatabaseFileAsync(temp, cancellationToken);
        }
        finally
        {
            DeleteIfExists(temp);
        }
    }

    private static async Task ExtractArchiveAsync(
        string archivePath,
        string staging,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(staging, relative));
            var stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("مسیر فایل داخل پشتیبان نامعتبر است.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open();
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    private async Task<string> ResolveArchivePathAsync(string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || Path.GetFileName(fileName) != fileName
            || !fileName.EndsWith(ArchiveExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("نام فایل پشتیبان نامعتبر است.", nameof(fileName));

        var target = await GetBackupTargetAsync(cancellationToken);
        var fullPath = Path.GetFullPath(Path.Combine(target, fileName));
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("مسیر فایل پشتیبان مجاز نیست.");

        return fullPath;
    }

    private static async Task<BackupFileDto> DescribeBackupAsync(
        string archivePath,
        bool verify,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(archivePath);
        if (!info.Exists)
            throw new FileNotFoundException("نسخهٔ پشتیبان پیدا نشد.", archivePath);

        var hash = await ComputeSha256Async(archivePath, cancellationToken);
        if (!verify)
            return new BackupFileDto(
                info.Name,
                info.LastWriteTimeUtc,
                info.Length,
                hash,
                false,
                "اعتبارسنجی کامل هنگام Restore یا Verify انجام می‌شود.");

        try
        {
            await ValidateArchiveAsync(archivePath, cancellationToken);
            return new BackupFileDto(
                info.Name,
                info.LastWriteTimeUtc,
                info.Length,
                hash,
                true,
                "نسخهٔ پشتیبان معتبر است.");
        }
        catch (Exception exception)
        {
            return new BackupFileDto(
                info.Name,
                info.LastWriteTimeUtc,
                info.Length,
                hash,
                false,
                exception.Message);
        }
    }

    private static async Task ApplyRetentionAsync(string target, int keep)
    {
        var files = Directory
            .EnumerateFiles(target, "*" + ArchiveExtension, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(keep)
            .ToArray();

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Retention is best-effort; a failed cleanup must not invalidate a valid backup.
            }
        }

        await Task.CompletedTask;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void AddFileToArchive(ZipArchive archive, string filePath, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var source = File.OpenRead(filePath);
        using var target = entry.Open();
        source.CopyTo(target);
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed record BackupManifest(
        int Version,
        DateTimeOffset CreatedAtUtc,
        string ProductVersion,
        long DatabaseSizeBytes,
        string DatabaseSha256);

    internal sealed record PendingRestoreMarker(
        string FileName,
        string StagingDirectory,
        DateTimeOffset RequestedAt,
        Guid RequestedByUserId);
}
