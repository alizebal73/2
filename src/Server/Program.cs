using GameNetManager.Server.Data;
using GameNetManager.Server.Hubs;
using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

var databaseFile = builder.Configuration["Database:FileName"] ?? "App_Data/gamenet.db";
var databasePath = Path.IsPathRooted(databaseFile)
    ? databaseFile
    : Path.Combine(builder.Environment.ContentRootPath, databaseFile);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddDbContext<GameNetDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    await InitializeDatabaseAsync(app.Services, databasePath, logger);
    logger.LogInformation("Database ready at {DatabasePath}", databasePath);
}
catch (Exception exception)
{
    logger.LogCritical(exception, "Database initialization failed");
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth");

app.MapGet("/api/server-info", (IWebHostEnvironment environment) =>
    Results.Ok(new ServerInfoDto("GameNet Manager", environment.EnvironmentName, DateTimeOffset.UtcNow)))
    .WithName("GetServerInfo");

app.MapGet("/api/dashboard", async (GameNetDbContext database, CancellationToken cancellationToken) =>
{
    var stations = await database.Stations
        .AsNoTracking()
        .OrderBy(station => station.Zone)
        .ThenBy(station => station.Name)
        .Select(station => new StationDto(
            station.Id,
            station.Name,
            station.Zone,
            station.Type,
            (long)station.RatePerHour,
            station.State.ToString()))
        .ToListAsync(cancellationToken);

    return Results.Ok(new DashboardSnapshotDto(stations.Count, stations, DateTimeOffset.UtcNow));
})
.WithName("GetDashboardSnapshot");

app.MapHub<DashboardHub>("/hubs/dashboard");

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

static async Task InitializeDatabaseAsync(IServiceProvider services, string databasePath, ILogger logger)
{
    try
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
        await database.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(database);
        return;
    }
    catch (Exception exception) when (IsMigrationRecoveryCandidate(exception))
    {
        logger.LogWarning(exception, "Detected a stale SQLite database state; recreating database at {DatabasePath}", databasePath);

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();
        await database.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(database);
    }
}

static bool IsMigrationRecoveryCandidate(Exception exception)
{
    return exception is SqliteException or AggregateException { InnerException: SqliteException }
        || exception.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("SQLite Error 19", StringComparison.OrdinalIgnoreCase);
}

public partial class Program { }
