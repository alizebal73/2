using Microsoft.AspNetCore.DataProtection;
using GameNetManager.Server.Data;
using GameNetManager.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddScoped<SessionSettlementService>();
builder.Services.AddScoped<InvoiceReverseService>();
builder.Services.AddScoped<WalletRefundService>();
builder.Services.AddScoped<AccountPoolService>();
builder.Services.AddScoped<CustomerLoginService>();
builder.Services.AddScoped<CustomerVipReportService>();
builder.Services.AddScoped<UsersShiftReportService>();
builder.Services.AddScoped<SessionPricingService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<SessionReportService>();
builder.Services.AddSingleton<GameCredentialProtectionService>();
builder.Services.AddHostedService<AgentPresenceMonitor>();

var databaseFile = builder.Configuration["Database:FileName"] ?? "App_Data/gamenet.db";
var databasePath = Path.IsPathRooted(databaseFile)
    ? databaseFile
    : Path.Combine(builder.Environment.ContentRootPath, databaseFile);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("GameNetManager");
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

app.MapGet("/api/tariffs", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(
        context,