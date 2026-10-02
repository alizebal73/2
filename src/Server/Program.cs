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
builder.Services.AddScoped<SessionSettlementService>();

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


app.MapPost("/api/customers/{customerId:guid}/wallet-refunds", async (
    Guid customerId,
    WalletRefundRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    if (request.Amount <= 0)
    {
        return Results.BadRequest(new { code = "invalid_amount", message = "مبلغ بازگشت باید بیشتر از صفر باشد." });
    }

    var reason = request.Reason?.Trim();
    if (string.IsNullOrWhiteSpace(reason))
    {
        return Results.BadRequest(new { code = "missing_reason", message = "دلیل بازگشت وجه را وارد کنید." });
    }

    var customer = await database.Customers
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

    if (customer is null)
    {
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });
    }

    if (customer.Balance < request.Amount)
    {
        return Results.BadRequest(new { code = "insufficient_balance", message = "موجودی کیف پول برای بازگشت این مبلغ کافی نیست." });
    }

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

    try
    {
        customer.Balance -= request.Amount;

        var ledger = new WalletTransaction
        {
            CustomerId = customer.Id,
            Amount = request.Amount,
            Type = WalletTransactionType.Debit,
            Description = "بازگشت وجه · " + reason
        };

        database.WalletTransactions.Add(ledger);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "WalletRefund",
            EntityName = "CustomerWallet",
            EntityId = customer.Id.ToString(),
            Details = request.Amount.ToString("0.##") + " تومان · " + reason,
            AppUserId = request.AppUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new WalletLedgerEntryDto(
            ledger.Id,
            ledger.CustomerId,
            ledger.Amount,
            "Refund",
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
.WithName("PostWalletRefund");



app.MapGet("/api/shifts/{shiftId:guid}/expenses", async (
    Guid shiftId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var exists = await database.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId, cancellationToken);
    if (!exists)
    {
        return Results.NotFound(new { code = "shift_not_found", message = "شیفت پیدا نشد." });
    }

    var rows = await database.Expenses
        .AsNoTracking()
        .Where(item => item.ShiftId == shiftId)
        .OrderByDescending(item => item.CreatedAt)
        .Select(item => new FinanceExpenseDto(
            item.Id,
            item.ShiftId,
            item.Category,
            item.Amount,
            item.Description,
            item.CreatedAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(rows);
})
.WithName("GetShiftExpenses");

app.MapPost("/api/shifts/{shiftId:guid}/expenses", async (
    Guid shiftId,
    FinanceExpenseRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    if (request.Amount <= 0)
    {
        return Results.BadRequest(new { code = "invalid_amount", message = "مبلغ هزینه باید بیشتر از صفر باشد." });
    }

    var category = request.Category?.Trim();
    if (string.IsNullOrWhiteSpace(category))
    {
        return Results.BadRequest(new { code = "missing_category", message = "دسته هزینه را وارد کنید." });
    }

    var shift = await database.Shifts.FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
    if (shift is null)
    {
        return Results.NotFound(new { code = "shift_not_found", message = "شیفت پیدا نشد." });
    }

    var expense = new Expense
    {
        ShiftId = shiftId,
        Category = category,
        Amount = request.Amount,
        Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
    };

    database.Expenses.Add(expense);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "ExpenseCreate",
        EntityName = "Expense",
        EntityId = expense.Id.ToString(),
        Details = request.Amount.ToString("0.##") + " تومان · " + category + " · " + (expense.Description ?? "بدون شرح"),
        AppUserId = request.AppUserId
    });

    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(new FinanceExpenseDto(
        expense.Id,
        expense.ShiftId,
        expense.Category,
        expense.Amount,
        expense.Description,
        expense.CreatedAt));
})
.WithName("CreateShiftExpense");

app.MapGet("/api/finance/summary", async (
    DateTimeOffset? from,
    DateTimeOffset? to,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var start = from ?? DateTimeOffset.UtcNow.Date;
    var end = to ?? DateTimeOffset.UtcNow;

    var revenue = await database.Invoices
        .AsNoTracking()
        .Where(item => item.Status == InvoiceStatus.Paid && item.IssuedAt >= start && item.IssuedAt <= end)
        .SumAsync(item => (decimal?)item.TotalAmount, cancellationToken) ?? 0m;

    var expense = await database.Expenses
        .AsNoTracking()
        .Where(item => item.CreatedAt >= start && item.CreatedAt <= end)
        .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    return Results.Ok(new FinanceSummaryDto(
        start,
        end,
        revenue,
        expense,
        revenue - expense));
})
.WithName("GetFinanceSummary");



app.MapPost("/api/sessions", async (
    StartSessionRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

    if (request.CustomerId == Guid.Empty || request.StationId == Guid.Empty)
    {
        return Results.BadRequest(new { code = "invalid_session_reference", message = "مشتری و ایستگاه معتبر نیستند." });
    }

    var customerExists = await database.Customers.AnyAsync(item => item.Id == request.CustomerId, cancellationToken);
    var station = await database.Stations.FirstOrDefaultAsync(item => item.Id == request.StationId, cancellationToken);

    if (!customerExists)
    {
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });
    }

    if (station is null)
    {
        return Results.NotFound(new { code = "station_not_found", message = "ایستگاه پیدا نشد." });
    }

    if (station.State != StationState.Available)
    {
        return Results.Conflict(new { code = "station_not_available", message = "این ایستگاه دیگر آزاد نیست." });
    }

    if (request.TariffId is not null)
    {
        var tariffExists = await database.Tariffs.AnyAsync(item => item.Id == request.TariffId.Value, cancellationToken);
        if (!tariffExists)
        {
            return Results.BadRequest(new { code = "tariff_not_found", message = "تعرفه انتخاب‌شده پیدا نشد." });
        }
    }

    var session = new Session
    {
        CustomerId = request.CustomerId,
        StationId = request.StationId,
        TariffId = request.TariffId,
        AppUserId = request.AppUserId,
        StartAt = DateTimeOffset.UtcNow,
        State = SessionState.Active,
        TotalAmount = 0m
    };

    database.Sessions.Add(session);
    station.State = StationState.Occupied;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionStart",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "شروع جلسه · ایستگاه " + station.Name,
        AppUserId = request.AppUserId
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new StartSessionResultDto(
        session.Id,
        station.Id,
        session.CustomerId,
        session.StartAt));
})
.WithName("StartSession");

app.MapPost("/api/sessions/{sessionId:guid}/settle", async (
    Guid sessionId,
    SessionSettlementRequest request,
    SessionSettlementService settlement,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await settlement.SettleAsync(sessionId, request, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { code = "settlement_conflict", message = exception.Message });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { code = "invalid_settlement", message = exception.Message });
    }
})
.WithName("SettleSession");

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


public sealed record StartSessionRequest(Guid CustomerId, Guid StationId, Guid? TariffId, Guid? AppUserId);
public sealed record StartSessionResultDto(Guid SessionId, Guid StationId, Guid CustomerId, DateTimeOffset StartAt);

public sealed record FinanceExpenseRequestDto(decimal Amount, string Category, string? Description, Guid? AppUserId);
public sealed record FinanceExpenseDto(Guid Id, Guid ShiftId, string Category, decimal Amount, string? Description, DateTimeOffset CreatedAt);
public sealed record FinanceSummaryDto(DateTimeOffset From, DateTimeOffset To, decimal Revenue, decimal Expense, decimal OperatingProfit);
