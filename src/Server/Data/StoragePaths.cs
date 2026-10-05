namespace GameNetManager.Server.Data;

public static class StoragePaths
{
    public const string DataRootConfigurationKey = "App:DataRoot";
    public const string DataRootEnvironmentVariable = "GAMENET_DATA_ROOT";

    public static string ResolveDataRoot(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[DataRootConfigurationKey]
            ?? Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured.Trim());

        return environment.IsProduction()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "GameNetManager")
            : Path.Combine(environment.ContentRootPath, "App_Data");
    }

    public static string ResolveDatabasePath(
        IConfiguration configuration,
        IHostEnvironment environment,
        string dataRoot)
    {
        var configured = configuration["Database:FileName"] ?? "App_Data/gamenet.db";
        if (Path.IsPathRooted(configured))
            return Path.GetFullPath(configured);

        var relative = configured
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        const string legacyPrefix = "App_Data" + "/";
        if (relative.StartsWith(legacyPrefix.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            relative = relative[(legacyPrefix.Length)..];

        return Path.GetFullPath(Path.Combine(dataRoot, relative));
    }

    public static string ResolveDataProtectionKeysPath(string dataRoot)
        => Path.Combine(dataRoot, "DataProtection-Keys");

    public static string ResolveBackupRecoveryRoot(string dataRoot)
        => Path.Combine(dataRoot, "BackupRecovery");

    public static string ResolveDefaultBackupTarget(string dataRoot)
        => Path.Combine(dataRoot, "Backups");
}
