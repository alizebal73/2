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


app.MapGet("/api/customers/{customerId:guid}/wallet-ledger", async (
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var customer = await database.Customers
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

    if (customer is null)
    {
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });
    }

    var transactions = await database.WalletTransactions
        .AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.Id)
        .ToListAsync(cancellationToken);

    var running = customer.Balance;
    var result = new List<WalletLedgerEntryDto>(transactions.Count);

    foreach (var transaction in transactions)
    {
        result.Add(new WalletLedgerEntryDto(
            transaction.Id,
            transaction.CustomerId,
            transaction.Amount,
            transaction.Type.ToString(),
            transaction.Description,
            transaction.CreatedAt,
            running));

        running = transaction.Type == WalletTransactionType.Credit
            ? running - transaction.Amount
            : running + transaction.Amount;
    }

    result.Reverse();
    return Results.Ok(result);
})
.WithName("GetWalletLedger");

app.MapPost("/api/customers/{customerId:guid}/wallet-transactions", async (
    Guid customerId,
    WalletTransactionRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    if (request.Amount <= 0)
    {
        return Results.BadRequest(new { code = "invalid_amount", message = "مبلغ باید بیشتر از صفر باشد." });
    }

    if (!Enum.TryParse<WalletTransactionType>(request.Type, true, out var type))
    {
        return Results.BadRequest(new { code = "invalid_transaction_type", message = "نوع تراکنش معتبر نیست." });
    }

    var description = request.Description?.Trim();
    if (string.IsNullOrWhiteSpace(description))
    {
        return Results.BadRequest(new { code = "missing_description", message = "توضیح تراکنش را وارد کنید." });
    }

    var customer = await database.Customers
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

    if (customer is null)
    {
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });
    }

    var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

    try
    {
        if (type == WalletTransactionType.Credit)
        {
            customer.Balance += request.Amount;
        }
        else
        {
            if (customer.Balance < request.Amount)
            {
                return Results.BadRequest(new { code = "insufficient_balance", message = "موجودی کیف پول کافی نیست." });
            }

            customer.Balance -= request.Amount;
        }

        var ledger = new WalletTransaction
        {
            CustomerId = customer.Id,
            Amount = request.Amount,
            Type = type,
            Description = description
        };

        database.WalletTransactions.Add(ledger);
        database.AuditLogs.Add(new AuditLog
        {
            Action = type == WalletTransactionType.Credit ? "WalletCredit" : "WalletDebit",
            EntityName = "CustomerWallet",
            EntityId = customer.Id.ToString(),
            Details = request.Amount.ToString("0.##") + " تومان · " + description,
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new WalletLedgerEntryDto(
            ledger.Id,
            ledger.CustomerId,
            ledger.Amount,
            ledger.Type.ToString(),
            ledger.Description,
            ledger.CreatedAt,
            customer.Balance));
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
})
.WithName("PostWalletTransaction");

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
    await using var scope = services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<GameNetDbContext>();

    try
    {
        await database.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(database);
    }
    catch (Exception exception) when (IsMigrationRecoveryCandidate(exception))
    {
        logger.LogCritical(
            exception,
            "خطای مهاجرت دیتابیس در {DatabasePath} رخ داد. حذف خودکار دیتابیس غیرفعال است؛ قبل از ادامه، فایل پشتیبان/Recovery بررسی شود.",
            databasePath);

        throw new InvalidOperationException(
            "مهاجرت دیتابیس ناموفق بود. برای جلوگیری از از دست رفتن اطلاعات، دیتابیس حذف یا بازسازی خودکار نشد. ابتدا Recovery/Backup را بررسی کنید.",
            exception);
    }
}
static bool IsMigrationRecoveryCandidate(Exception exception)
{
    return exception is SqliteException or AggregateException { InnerException: SqliteException }
        || exception.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("SQLite Error 19", StringComparison.OrdinalIgnoreCase);
}

public partial class Program { }
