using GameNetManager.Server.Data;
using GameNetManager.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using GameNetManager.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddHostedService<AgentPresenceMonitor>();

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

app.MapPost("/api/agent/register", async (
    AgentRegistrationRequest request,
    HttpContext context,
    GameNetDbContext database,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var configuredToken = configuration["Agent:RegistrationToken"];
    var suppliedToken = context.Request.Headers["X-GameNet-Registration-Token"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(configuredToken)
        || string.IsNullOrWhiteSpace(suppliedToken)
        || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredToken),
            Encoding.UTF8.GetBytes(suppliedToken)))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var deviceId = request.DeviceId?.Trim();
    var name = request.Name?.Trim();
    var agentVersion = request.AgentVersion?.Trim();
    var osVersion = request.OsVersion?.Trim();

    if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 120)
        return Results.BadRequest(new { code = "invalid_device_id", message = "شناسه دستگاه معتبر نیست." });

    if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        return Results.BadRequest(new { code = "invalid_device_name", message = "نام دستگاه معتبر نیست." });

    if (string.IsNullOrWhiteSpace(agentVersion) || agentVersion.Length > 60)
        return Results.BadRequest(new { code = "invalid_agent_version", message = "نسخه Agent معتبر نیست." });

    if (request.StationId.HasValue
        && !await database.Stations.AnyAsync(item => item.Id == request.StationId.Value && item.IsActive, cancellationToken))
        return Results.BadRequest(new { code = "station_not_found", message = "ایستگاه انتخاب‌شده پیدا نشد." });

    var device = await database.AgentDevices
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);

    if (device is null)
    {
        device = new AgentDevice
        {
            DeviceId = deviceId,
            Name = name,
            IsActive = true
        };
        database.AgentDevices.Add(device);
    }

    var token = AuthorizationService.CreateToken();
    device.Name = name;
    device.StationId = request.StationId;
    device.AgentTokenHash = PasswordSecurity.HashToken(token);
    device.AgentVersion = agentVersion;
    device.OsVersion = osVersion;
    device.IsActive = true;
    device.IsOnline = false;
    device.ConnectionId = null;
    device.ConnectedAt = null;
    device.LastSeenAt = null;
    device.LastIpAddress = context.Connection.RemoteIpAddress?.ToString();

    database.AuditLogs.Add(new AuditLog
    {
        Action = "AgentRegistered",
        EntityName = "AgentDevice",
        EntityId = device.Id.ToString(),
        Details = "ثبت/صدور مجدد دسترسی Agent · " + device.DeviceId
    });

    await database.SaveChangesAsync(cancellationToken);
    await database.Entry(device).Reference(item => item.Station).LoadAsync(cancellationToken);

    var heartbeatInterval = Math.Clamp(configuration.GetValue("Agent:HeartbeatIntervalSeconds", 10), 3, 60);
    var offlineAfter = Math.Clamp(
        configuration.GetValue("Agent:OfflineAfterSeconds", 30),
        heartbeatInterval * 2,
        300);

    return Results.Ok(new AgentRegistrationResponse(
        device.Id,
        device.DeviceId,
        token,
        device.Station?.Name,
        DateTimeOffset.UtcNow,
        heartbeatInterval,
        offlineAfter));
})
.WithName("RegisterAgent");

app.MapPut("/api/agent/devices/{deviceId:guid}/policy", async (
    Guid deviceId,
    AgentPolicyRequest request,
    HttpContext context,
    GameNetDbContext database,
    IHubContext<AgentHub> agentHub,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(
        context,
        database,
        "client.control",
        cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var device = await database.AgentDevices
        .FirstOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);
    if (device is null)
        return Results.NotFound(new { code = "agent_not_found", message = "Agent پیدا نشد." });

    device.KioskEnabled = request.KioskEnabled;
    device.LockOnDisconnect = request.LockOnDisconnect;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "AgentPolicyUpdated",
        EntityName = "AgentDevice",
        EntityId = device.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = $"Kiosk={device.KioskEnabled}; LockOnDisconnect={device.LockOnDisconnect}"
    });

    await database.SaveChangesAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(device.ConnectionId))
    {
        await agentHub.Clients.Client(device.ConnectionId).SendAsync(
            "AgentPolicyChanged",
            new AgentPolicyDto(device.Id, device.KioskEnabled, device.LockOnDisconnect),
            cancellationToken);
    }

    return Results.Ok(new AgentPolicyDto(device.Id, device.KioskEnabled, device.LockOnDisconnect));
})
.WithName("UpdateAgentPolicy");

app.MapGet("/api/agent/devices", async (
    HttpContext context,
    GameNetDbContext database,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(
        context,
        database,
        "client.control",
        cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var now = DateTimeOffset.UtcNow;
    var heartbeatInterval = Math.Clamp(
        configuration.GetValue("Agent:HeartbeatIntervalSeconds", 10),
        3,
        60);
    var offlineAfter = Math.Clamp(
        configuration.GetValue("Agent:OfflineAfterSeconds", 30),
        heartbeatInterval * 2,
        300);

    var devices = await database.AgentDevices
        .AsNoTracking()
        .Include(item => item.Station)
        .OrderBy(item => item.Name)
        .ToListAsync(cancellationToken);

    return Results.Ok(devices.Select(device => new AgentStatusDto(
        device.Id,
        device.DeviceId,
        device.Name,
        device.StationId,
        device.Station?.Name,
        device.IsActive
            && device.LastSeenAt.HasValue
            && now - device.LastSeenAt.Value <= TimeSpan.FromSeconds(offlineAfter),
        device.IsLocked,
        device.KioskEnabled,
        device.LockOnDisconnect,
        device.LastSeenAt,
        device.ConnectedAt,
        device.AgentVersion,
        device.OsVersion,
        device.CpuUsagePercent,
        device.MemoryAvailableBytes,
        device.UptimeSeconds)).ToList());
})
.WithName("GetAgentDevices");



app.MapPost("/api/agent/devices/{deviceId:guid}/commands", async (
    Guid deviceId,
    AgentCommandRequest request,
    HttpContext context,
    GameNetDbContext database,
    IHubContext<AgentHub> agentHub,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(
        context,
        database,
        "client.control",
        cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var commandType = request.CommandType?.Trim().ToLowerInvariant();
    if (!AgentCommandTypes.IsSupported(commandType))
        return Results.BadRequest(new { code = "unsupported_agent_command", message = "فرمان Agent پشتیبانی نمی‌شود." });

    var device = await database.AgentDevices
        .FirstOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);

    if (device is null)
        return Results.NotFound(new { code = "agent_not_found", message = "Agent پیدا نشد." });

    if (!device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
        return Results.Conflict(new { code = "agent_offline", message = "Agent آفلاین است و فرمان ارسال نشد." });

    if (!string.IsNullOrWhiteSpace(request.PayloadJson) && request.PayloadJson.Length > 4000)
        return Results.BadRequest(new { code = "command_payload_too_large", message = "دادهٔ فرمان بیش از حد مجاز است." });

    var now = DateTimeOffset.UtcNow;
    var command = new AgentCommand
    {
        AgentDeviceId = device.Id,
        RequestedByAppUserId = auth.User!.Id,
        CommandType = commandType!,
        PayloadJson = request.PayloadJson,
        Status = "Sent",
        RequestedAt = now,
        SentAt = now,
        AgentConnectionId = device.ConnectionId
    };

    database.AgentCommands.Add(command);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "AgentCommandRequested",
        EntityName = "AgentCommand",
        EntityId = command.Id.ToString(),
        AppUserId = auth.User.Id,
        Details = $"فرمان {command.CommandType} برای Agent {device.DeviceId}"
    });
    await database.SaveChangesAsync(cancellationToken);

    try
    {
        await agentHub.Clients.Client(device.ConnectionId).SendAsync(
            "AgentCommand",
            new AgentCommandEnvelope(command.Id, command.CommandType, command.PayloadJson, command.RequestedAt),
            cancellationToken);
    }
    catch (Exception exception)
    {
        command.Status = "Failed";
        command.Succeeded = false;
        command.CompletedAt = DateTimeOffset.UtcNow;
        command.ResultMessage = "ارسال فرمان به Agent انجام نشد.";
        await database.SaveChangesAsync(CancellationToken.None);
        return Results.Problem(
            detail: command.ResultMessage,
            statusCode: StatusCodes.Status502BadGateway,
            title: "ارسال فرمان Agent ناموفق بود.");
    }

    return Results.Ok(new AgentCommandStatusDto(
        command.Id,
        command.AgentDeviceId,
        command.CommandType,
        command.Status,
        command.RequestedAt,
        command.SentAt,
        command.CompletedAt,
        command.Succeeded,
        command.ResultMessage));
})
.WithName("SendAgentCommand");

app.MapGet("/api/agent/commands/{commandId:guid}", async (
    Guid commandId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(
        context,
        database,
        "client.control",
        cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var command = await database.AgentCommands
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);

    if (command is null)
        return Results.NotFound(new { code = "agent_command_not_found", message = "فرمان Agent پیدا نشد." });

    return Results.Ok(new AgentCommandStatusDto(
        command.Id,
        command.AgentDeviceId,
        command.CommandType,
        command.Status,
        command.RequestedAt,
        command.SentAt,
        command.CompletedAt,
        command.Succeeded,
        command.ResultMessage));
})
.WithName("GetAgentCommand");

app.MapGet("/api/release/manifest", (GameNetDbContext database, IConfiguration configuration) =>
{
    var schemaVersion = database.Database.GetAppliedMigrations().LastOrDefault() ?? "unknown";
    return Results.Ok(new
    {
        productVersion = configuration["App:ProductVersion"] ?? "0.6.0",
        schemaVersion,
        apiContractVersion = configuration["App:ApiContractVersion"] ?? "1",
        minimumClientVersion = configuration["App:MinimumClientVersion"] ?? "0.1.0",
        recommendedClientVersion = configuration["App:RecommendedClientVersion"] ?? "0.1.0",
        updateChannel = configuration["App:UpdateChannel"] ?? "stable"
    });
})
.WithName("GetReleaseManifest");

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    GameNetDbContext database,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var username = request.UserName?.Trim();
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { code = "missing_credentials", message = "نام کاربری و رمز عبور را وارد کنید." });

    var user = await database.AppUsers
        .Include(item => item.Permissions)
        .ThenInclude(item => item.Permission)
        .FirstOrDefaultAsync(item => item.UserName == username && item.IsActive, cancellationToken);

    if (user is null || !PasswordSecurity.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = AuthorizationService.CreateToken();
    var session = new AppUserSession
    {
        AppUserId = user.Id,
        TokenHash = PasswordSecurity.HashToken(token),
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(AuthorizationService.SessionHours),
        LastSeenAt = DateTimeOffset.UtcNow
    };

    user.LastLoginAt = DateTimeOffset.UtcNow;
    database.AppUserSessions.Add(session);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "AppUserLogin",
        EntityName = "AppUser",
        EntityId = user.Id.ToString(),
        AppUserId = user.Id,
        Details = "ورود اپراتور · " + user.UserName
    });
    await database.SaveChangesAsync(cancellationToken);

    context.Response.Cookies.Append(AuthorizationService.SessionCookieName, token, new CookieOptions
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = session.ExpiresAt
    });

    return Results.Ok(ToAppUserDto(user));
})
.WithName("AppUserLogin");

app.MapPost("/api/auth/logout", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var user = await AuthorizationService.ResolveUserAsync(context, database, cancellationToken);
    var token = context.Request.Cookies[AuthorizationService.SessionCookieName];
    if (!string.IsNullOrWhiteSpace(token))
    {
        var hash = PasswordSecurity.HashToken(token);
        var session = await database.AppUserSessions.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (session is not null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    context.Response.Cookies.Delete(AuthorizationService.SessionCookieName);
    if (user is not null)
    {
        database.AuditLogs.Add(new AuditLog
        {
            Action = "AppUserLogout",
            EntityName = "AppUser",
            EntityId = user.Id.ToString(),
            AppUserId = user.Id,
            Details = "خروج اپراتور · " + user.UserName
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new { success = true });
})
.WithName("AppUserLogout");

app.MapGet("/api/auth/me", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var user = await AuthorizationService.ResolveUserAsync(context, database, cancellationToken);
    return user is null ? Results.Unauthorized() : Results.Ok(ToAppUserDto(user));
})
.WithName("GetCurrentAppUser");

app.MapGet("/api/users", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "user.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var users = await database.AppUsers
        .AsNoTracking()
        .Include(item => item.Permissions)
        .ThenInclude(item => item.Permission)
        .OrderBy(item => item.FullName)
        .ToListAsync(cancellationToken);

    return Results.Ok(users.Select(ToAppUserDto).ToList());
})
.WithName("GetAppUsers");

app.MapPost("/api/users", async (
    AppUserWriteRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "user.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var username = request.UserName?.Trim();
    var fullName = request.FullName?.Trim();
    var email = request.Email?.Trim();
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        return Results.BadRequest(new { code = "missing_user_fields", message = "نام، نام کاربری و ایمیل را کامل کنید." });
    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        return Results.BadRequest(new { code = "weak_password", message = "رمز عبور باید حداقل ۸ کاراکتر باشد." });
    if (!AuthorizationService.PermissionCatalog.ContainsKey(request.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "user.manage" : "session.start"))
        return Results.BadRequest(new { code = "invalid_role", message = "نقش کاربر معتبر نیست." });

    if (await database.AppUsers.AnyAsync(item => item.UserName == username || item.Email == email, cancellationToken))
        return Results.Conflict(new { code = "duplicate_user", message = "نام کاربری یا ایمیل قبلاً استفاده شده است." });

    var user = new AppUser
    {
        FullName = fullName,
        UserName = username,
        Email = email,
        PasswordHash = PasswordSecurity.Hash(request.Password),
        Role = request.Role.Trim(),
        IsActive = true
    };
    database.AppUsers.Add(user);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "AppUserCreate",
        EntityName = "AppUser",
        EntityId = user.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = "ایجاد کاربر · " + user.UserName
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToAppUserDto(user));
})
.WithName("CreateAppUser");

app.MapPut("/api/users/{userId:guid}", async (
    Guid userId,
    AppUserWriteRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "user.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var user = await database.AppUsers.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    if (user is null) return Results.NotFound(new { code = "user_not_found", message = "کاربر پیدا نشد." });

    var username = request.UserName?.Trim();
    var fullName = request.FullName?.Trim();
    var email = request.Email?.Trim();
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        return Results.BadRequest(new { code = "missing_user_fields", message = "نام، نام کاربری و ایمیل را کامل کنید." });

    if (await database.AppUsers.AnyAsync(item => item.Id != userId && (item.UserName == username || item.Email == email), cancellationToken))
        return Results.Conflict(new { code = "duplicate_user", message = "نام کاربری یا ایمیل قبلاً استفاده شده است." });

    user.FullName = fullName;
    user.UserName = username;
    user.Email = email;
    user.Role = request.Role.Trim();
    user.IsActive = request.IsActive;
    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        if (request.Password.Length < 8)
            return Results.BadRequest(new { code = "weak_password", message = "رمز عبور باید حداقل ۸ کاراکتر باشد." });
        user.PasswordHash = PasswordSecurity.Hash(request.Password);
    }

    database.AuditLogs.Add(new AuditLog
    {
        Action = "AppUserUpdate",
        EntityName = "AppUser",
        EntityId = user.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = "ویرایش کاربر · " + user.UserName
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToAppUserDto(user));
})
.WithName("UpdateAppUser");

app.MapGet("/api/permissions", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "user.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var permissions = await database.Permissions
        .AsNoTracking()
        .OrderBy(item => item.Name)
        .Select(item => new { id = item.Id, name = item.Name, description = item.Description })
        .ToListAsync(cancellationToken);

    return Results.Ok(permissions);
})
.WithName("GetPermissions");

app.MapPut("/api/users/{userId:guid}/permissions", async (
    Guid userId,
    PermissionAssignmentRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "user.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var user = await database.AppUsers.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    if (user is null) return Results.NotFound(new { code = "user_not_found", message = "کاربر پیدا نشد." });

    var names = request.PermissionNames
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name.Trim().ToLowerInvariant())
        .Distinct()
        .ToList();

    var invalid = names.Where(name => !AuthorizationService.PermissionCatalog.ContainsKey(name)).ToList();
    if (invalid.Count > 0)
        return Results.BadRequest(new { code = "invalid_permission", message = "یکی از دسترسی‌ها معتبر نیست.", invalid });

    var permissions = await database.Permissions.Where(item => names.Contains(item.Name)).ToListAsync(cancellationToken);
    var current = await database.AppUserPermissions.Where(item => item.AppUserId == userId).ToListAsync(cancellationToken);
    database.AppUserPermissions.RemoveRange(current);

    foreach (var permission in permissions)
        database.AppUserPermissions.Add(new AppUserPermission { AppUserId = userId, PermissionId = permission.Id });

    database.AuditLogs.Add(new AuditLog
    {
        Action = "AppUserPermissionsUpdate",
        EntityName = "AppUser",
        EntityId = userId.ToString(),
        AppUserId = auth.User!.Id,
        Details = "تغییر دسترسی‌ها · " + user.UserName + " · " + string.Join(",", names)
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { userId, permissions = names });
})
.WithName("SetAppUserPermissions");

app.MapGet("/api/payroll/users", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "payroll.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var users = await database.AppUsers.AsNoTracking()
        .OrderBy(item => item.FullName)
        .Select(item => new { item.Id, item.FullName, item.IsActive })
        .ToListAsync(cancellationToken);

    var profiles = await database.EmployeeProfiles.AsNoTracking().ToListAsync(cancellationToken);
    var entries = await database.PayrollLedgerEntries.AsNoTracking()
        .Where(item => item.Status == ApprovalStatus.Approved)
        .Include(item => item.EmployeeProfile)
        .ToListAsync(cancellationToken);

    var now = DateTimeOffset.UtcNow;
    var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    var profileByUser = profiles.ToDictionary(item => item.AppUserId);
    var entryGroups = entries.GroupBy(item => item.EmployeeProfile.AppUserId)
        .ToDictionary(group => group.Key, group => group.ToList());

    return Results.Ok(users.Select(user =>
    {
        profileByUser.TryGetValue(user.Id, out var profile);
        entryGroups.TryGetValue(user.Id, out var rows);
        rows ??= new List<PayrollLedgerEntry>();
        var monthRows = rows.Where(item => item.CreatedAt >= monthStart).ToList();
        return new
        {
            userId = user.Id,
            fullName = user.FullName,
            phone = profile?.Phone ?? "",
            payType = profile?.PayType ?? "hourly",
            hourlyRate = profile?.HourlyRate ?? 0m,
            monthlySalary = profile?.MonthlySalary ?? 0m,
            overtimeRate = profile?.OvertimeRate ?? 0m,
            employmentStartDate = profile?.EmploymentStartDate,
            workSchedule = profile?.WorkSchedule,
            notes = profile?.Notes,
            isActive = profile?.IsActive ?? user.IsActive,
            employeePayable = rows.Sum(item => item.EmployeePayableDelta),
            ownerReceivable = rows.Sum(item => item.OwnerReceivableDelta),
            accruedThisMonth = monthRows.Where(item => item.Kind is "SalaryAccrual" or "Overtime").Sum(item => item.Amount),
            paidThisMonth = monthRows.Where(item => item.Kind == "SalaryPayment").Sum(item => item.Amount),
            bonusTotal = rows.Where(item => item.Kind == "Bonus").Sum(item => item.Amount),
            deductionTotal = rows.Where(item => item.Kind == "Deduction").Sum(item => item.Amount),
            damageTotal = rows.Where(item => item.Kind == "Damage").Sum(item => item.Amount),
            advanceTotal = rows.Where(item => item.Kind == "Advance").Sum(item => item.Amount),
            lastPaymentAt = rows.Where(item => item.Kind == "SalaryPayment").OrderByDescending(item => item.CreatedAt).Select(item => (DateTimeOffset?)item.CreatedAt).FirstOrDefault()
        };
    }));
})
.WithName("GetPayrollUsers");

app.MapGet("/api/payroll/users/{userId:guid}/ledger", async (
    Guid userId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "payroll.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var storedRows = await database.PayrollLedgerEntries.AsNoTracking()
        .Where(item => item.EmployeeProfile.AppUserId == userId)
        .Include(item => item.EmployeeProfile)
        .ThenInclude(item => item.AppUser)
        .ToListAsync(cancellationToken);

    var rows = storedRows
        .OrderByDescending(item => item.CreatedAt)
        .Take(200)
        .Select(item => new
        {
            id = item.Id,
            userId,
            userName = item.EmployeeProfile.AppUser.FullName,
            kind = item.Kind,
            amount = item.Amount,
            employeePayableDelta = item.EmployeePayableDelta,
            ownerReceivableDelta = item.OwnerReceivableDelta,
            reason = item.Reason,
            status = item.Status.ToString(),
            createdByUserId = item.CreatedByUserId,
            createdAt = item.CreatedAt,
            approvedByUserId = item.ApprovedByUserId,
            approvedAt = item.ApprovedAt,
            paymentMethod = item.PaymentMethod,
            receiptNumber = item.ReceiptNumber
        })
        .ToList();

    return Results.Ok(rows);
})
.WithName("GetPayrollLedger");

app.MapPut("/api/payroll/users/{userId:guid}", async (
    Guid userId,
    PayrollProfileRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "payroll.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var user = await database.AppUsers.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    if (user is null) return Results.NotFound(new { code = "user_not_found", message = "کاربر پیدا نشد." });
    if (request.HourlyRate < 0 || request.MonthlySalary < 0 || request.OvertimeRate < 0)
        return Results.BadRequest(new { code = "invalid_payroll_profile", message = "مقادیر حقوقی نمی‌توانند منفی باشند." });

    var payType = request.PayType?.Trim().ToLowerInvariant();
    if (payType is not ("hourly" or "monthly"))
        return Results.BadRequest(new { code = "invalid_pay_type", message = "نوع حقوق معتبر نیست." });

    var profile = await database.EmployeeProfiles.FirstOrDefaultAsync(item => item.AppUserId == userId, cancellationToken);
    if (profile is null)
    {
        profile = new EmployeeProfile { AppUserId = userId };
        database.EmployeeProfiles.Add(profile);
    }

    profile.Phone = request.Phone?.Trim() ?? "";
    profile.PayType = payType;
    profile.HourlyRate = request.HourlyRate;
    profile.MonthlySalary = request.MonthlySalary;
    profile.OvertimeRate = request.OvertimeRate;
    profile.EmploymentStartDate = request.EmploymentStartDate;
    profile.WorkSchedule = string.IsNullOrWhiteSpace(request.WorkSchedule) ? null : request.WorkSchedule.Trim();
    profile.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    profile.IsActive = request.IsActive;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "PayrollProfileUpdate",
        EntityName = "EmployeeProfile",
        EntityId = profile.Id.ToString(),
        AppUserId = auth.User!.Id,
        Details = "ویرایش پروفایل حقوقی · " + user.FullName
    });
    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        userId = user.Id,
        fullName = user.FullName,
        phone = profile.Phone,
        payType = profile.PayType,
        hourlyRate = profile.HourlyRate,
        monthlySalary = profile.MonthlySalary,
        overtimeRate = profile.OvertimeRate,
        employmentStartDate = profile.EmploymentStartDate,
        workSchedule = profile.WorkSchedule,
        notes = profile.Notes,
        isActive = profile.IsActive
    });
})
.WithName("UpdatePayrollProfile");

app.MapPost("/api/payroll/users/{userId:guid}/entries", async (
    Guid userId,
    PayrollEntryRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "payroll.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var target = await database.AppUsers.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
    if (target is null) return Results.NotFound(new { code = "user_not_found", message = "کاربر پیدا نشد." });
    if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        return Results.BadRequest(new { code = "invalid_payroll_entry", message = "مبلغ و دلیل عملیات حقوقی الزامی است." });

    var kind = request.Kind?.Trim();
    if (kind is not ("SalaryAccrual" or "Overtime" or "SalaryPayment" or "Bonus" or "Deduction" or "Advance" or "Damage" or "ReceivablePayment" or "Adjustment"))
        return Results.BadRequest(new { code = "invalid_payroll_kind", message = "نوع عملیات حقوقی معتبر نیست." });

    var paymentMethod = request.PaymentMethod?.Trim().ToLowerInvariant();
    if (kind == "SalaryPayment" && string.IsNullOrWhiteSpace(paymentMethod))
        return Results.BadRequest(new { code = "missing_payment_method", message = "روش پرداخت حقوق را مشخص کنید." });
    if (kind == "SalaryPayment" && paymentMethod is not ("cash" or "card" or "bank"))
        return Results.BadRequest(new { code = "invalid_payment_method", message = "روش پرداخت حقوق معتبر نیست." });

    var profile = await database.EmployeeProfiles.FirstOrDefaultAsync(item => item.AppUserId == userId, cancellationToken);
    if (profile is null)
    {
        profile = new EmployeeProfile { AppUserId = userId };
        database.EmployeeProfiles.Add(profile);
    }

    var employeeDelta = request.EmployeePayableDelta ?? kind switch
    {
        "SalaryAccrual" or "Overtime" or "Bonus" => request.Amount,
        "SalaryPayment" or "Deduction" => -request.Amount,
        _ => 0m
    };
    var ownerDelta = request.OwnerReceivableDelta ?? kind switch
    {
        "Advance" or "Damage" => request.Amount,
        "ReceivablePayment" => -request.Amount,
        _ => 0m
    };

    if (kind == "Adjustment" && request.EmployeePayableDelta is null && request.OwnerReceivableDelta is null)
        return Results.BadRequest(new { code = "missing_adjustment_delta", message = "برای اصلاح دستی حداقل یکی از مانده‌ها را مشخص کنید." });

    var sensitive = kind is "SalaryPayment" or "Bonus" or "Deduction" or "Advance" or "Damage" or "ReceivablePayment" or "Adjustment";
    var entry = new PayrollLedgerEntry
    {
        EmployeeProfile = profile,
        Kind = kind,
        Amount = request.Amount,
        EmployeePayableDelta = employeeDelta,
        OwnerReceivableDelta = ownerDelta,
        Reason = request.Reason.Trim(),
        Status = sensitive ? ApprovalStatus.Pending : ApprovalStatus.Approved,
        CreatedByUserId = auth.User!.Id,
        PaymentMethod = paymentMethod,
        ReceiptNumber = string.IsNullOrWhiteSpace(request.ReceiptNumber) ? null : request.ReceiptNumber.Trim()
    };
    database.PayrollLedgerEntries.Add(entry);

    string? approvalId = null;
    if (sensitive)
    {
        var approval = new ApprovalRequest
        {
            Action = "payroll.entry",
            EntityName = "PayrollLedgerEntry",
            EntityId = entry.Id.ToString(),
            Reason = entry.Reason,
            RequestedByUserId = auth.User.Id,
            Status = ApprovalStatus.Pending
        };
        database.ApprovalRequests.Add(approval);
        approvalId = approval.Id.ToString();
    }

    database.AuditLogs.Add(new AuditLog
    {
        Action = sensitive ? "PayrollEntryRequested" : "PayrollEntryCreate",
        EntityName = "PayrollLedgerEntry",
        EntityId = entry.Id.ToString(),
        AppUserId = auth.User.Id,
        Details = kind + " · " + request.Amount.ToString("0.##") + " تومان · " + entry.Reason
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { entryId = entry.Id, status = entry.Status.ToString(), approvalId });
})
.WithName("CreatePayrollEntry");

app.MapGet("/api/approvals", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "approval.decide", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var storedApprovals = await database.ApprovalRequests
        .AsNoTracking()
        .Include(item => item.RequestedByUser)
        .Include(item => item.DecidedByUser)
        .ToListAsync(cancellationToken);

    var approvals = storedApprovals
        .OrderByDescending(item => item.CreatedAt)
        .Take(100)
        .Select(item => new
        {
            id = item.Id,
            action = item.Action,
            entityName = item.EntityName,
            entityId = item.EntityId,
            reason = item.Reason,
            status = item.Status.ToString(),
            requestedByUserId = item.RequestedByUserId,
            requestedBy = item.RequestedByUser.FullName,
            decidedByUserId = item.DecidedByUserId,
            decidedBy = item.DecidedByUser == null ? null : item.DecidedByUser.FullName,
            decisionNote = item.DecisionNote,
            createdAt = item.CreatedAt,
            decidedAt = item.DecidedAt
        })
        .ToList();

    return Results.Ok(approvals);
})
.WithName("GetApprovals");

app.MapPost("/api/approvals", async (
    ApprovalCreateRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var action = request.Action?.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(request.EntityName) || string.IsNullOrWhiteSpace(request.Reason))
        return Results.BadRequest(new { code = "invalid_approval_request", message = "عملیات، موجودیت و دلیل تأیید را وارد کنید." });

    var requiredPermission = action switch
    {
        "invoice.reverse" => "finance.manage",
        "wallet.refund" => "customer.wallet",
        "payroll.entry" => "payroll.manage",
        "discount" => "session.settle",
        _ => null
    };

    if (requiredPermission is null)
        return Results.BadRequest(new { code = "unsupported_approval_action", message = "این نوع درخواست تأیید از مسیر عمومی پشتیبانی نمی‌شود." });

    var auth = await AuthorizationService.RequirePermissionAsync(context, database, requiredPermission, cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var approval = new ApprovalRequest
    {
        Action = action!,
        EntityName = request.EntityName.Trim(),
        EntityId = request.EntityId,
        Reason = request.Reason.Trim(),
        RequestedByUserId = auth.User!.Id,
        Status = ApprovalStatus.Pending
    };
    database.ApprovalRequests.Add(approval);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "ApprovalRequestCreate",
        EntityName = "ApprovalRequest",
        EntityId = approval.Id.ToString(),
        AppUserId = auth.User.Id,
        Details = "درخواست تأیید · " + approval.Action + " · " + approval.Reason
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = approval.Id, status = approval.Status.ToString() });
})
.WithName("CreateApprovalRequest");

app.MapPost("/api/invoices/{invoiceId:guid}/reverse/request", async (
    Guid invoiceId,
    ApprovalOperationRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var reason = request.Reason?.Trim();
    if (string.IsNullOrWhiteSpace(reason))
        return Results.BadRequest(new { code = "invalid_reverse", message = "دلیل برگشت عملیات را وارد کنید." });

    var invoice = await database.Invoices
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
    if (invoice is null)
        return Results.NotFound(new { code = "invoice_not_found", message = "فاکتور پیدا نشد." });

    if (invoice.Status != InvoiceStatus.Paid)
        return Results.Conflict(new { code = "reverse_conflict", message = "این فاکتور قابل درخواست برگشت نیست." });

    var pending = await database.ApprovalRequests.AnyAsync(item =>
        item.Action == "invoice.reverse"
        && item.EntityName == "Invoice"
        && item.EntityId == invoiceId.ToString()
        && item.Status == ApprovalStatus.Pending, cancellationToken);
    if (pending)
        return Results.Conflict(new { code = "reverse_approval_pending", message = "برای این فاکتور یک درخواست برگشت در انتظار تصمیم است." });

    var approval = new ApprovalRequest
    {
        Action = "invoice.reverse",
        EntityName = "Invoice",
        EntityId = invoiceId.ToString(),
        Reason = reason,
        RequestedByUserId = auth.User!.Id,
        Status = ApprovalStatus.Pending
    };
    database.ApprovalRequests.Add(approval);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "InvoiceReverseApprovalRequest",
        EntityName = "Invoice",
        EntityId = invoiceId.ToString(),
        AppUserId = auth.User.Id,
        Details = "درخواست تأیید برگشت فاکتور · " + reason
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        id = approval.Id,
        status = approval.Status.ToString(),
        action = approval.Action,
        entityId = invoiceId
    });
})
.WithName("RequestInvoiceReverseApproval");

app.MapPost("/api/approvals/{approvalId:guid}/approve", async (
    Guid approvalId,
    ApprovalDecisionRequest request,
    InvoiceReverseService reverseService,
    WalletRefundService walletRefundService,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "approval.decide", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var approval = await database.ApprovalRequests.FirstOrDefaultAsync(item => item.Id == approvalId, cancellationToken);
    if (approval is null) return Results.NotFound(new { code = "approval_not_found", message = "درخواست تأیید پیدا نشد." });
    if (approval.Status != ApprovalStatus.Pending)
        return Results.Conflict(new { code = "approval_not_pending", message = "این درخواست دیگر در وضعیت انتظار نیست." });
    if (approval.RequestedByUserId == auth.User!.Id)
        return Results.Conflict(new { code = "approval_self_decision", message = "ثبت‌کننده درخواست نمی‌تواند همان درخواست را تأیید کند." });

    if (approval.Action.Equals("payroll.entry", StringComparison.OrdinalIgnoreCase))
    {
        if (!Guid.TryParse(approval.EntityId, out var payrollEntryId))
            return Results.BadRequest(new { code = "invalid_approval_target", message = "شناسه عملیات حقوقی معتبر نیست." });

        var entry = await database.PayrollLedgerEntries.FirstOrDefaultAsync(item => item.Id == payrollEntryId, cancellationToken);
        if (entry is null) return Results.NotFound(new { code = "payroll_entry_not_found", message = "رکورد حقوقی پیدا نشد." });
        if (entry.Status != ApprovalStatus.Pending)
            return Results.Conflict(new { code = "payroll_entry_not_pending", message = "این عملیات حقوقی دیگر در انتظار تأیید نیست." });

        var approvedRows = await database.PayrollLedgerEntries
            .Where(item => item.EmployeeProfileId == entry.EmployeeProfileId && item.Status == ApprovalStatus.Approved)
            .ToListAsync(cancellationToken);

        var nextEmployeePayable = approvedRows.Sum(item => item.EmployeePayableDelta) + entry.EmployeePayableDelta;
        var nextOwnerReceivable = approvedRows.Sum(item => item.OwnerReceivableDelta) + entry.OwnerReceivableDelta;
        if (nextEmployeePayable < 0)
            return Results.Conflict(new { code = "payroll_negative_employee_payable", message = "مانده حقوق پس از این عملیات منفی می‌شود." });
        if (nextOwnerReceivable < 0)
            return Results.Conflict(new { code = "payroll_negative_owner_receivable", message = "مانده طلب مالک پس از این عملیات منفی می‌شود." });

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        entry.Status = ApprovalStatus.Approved;
        entry.ApprovedByUserId = auth.User.Id;
        entry.ApprovedAt = DateTimeOffset.UtcNow;
        approval.Status = ApprovalStatus.Approved;
        approval.DecidedByUserId = auth.User.Id;
        approval.DecidedAt = DateTimeOffset.UtcNow;
        approval.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? "تأیید و اجرا شد" : request.Note.Trim();

        database.AuditLogs.Add(new AuditLog
        {
            Action = "PayrollEntryApproved",
            EntityName = "PayrollLedgerEntry",
            EntityId = entry.Id.ToString(),
            AppUserId = auth.User.Id,
            Details = "تأیید عملیات حقوقی · " + entry.Kind + " · " + entry.Amount.ToString("0.##") + " تومان"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { id = approval.Id, status = approval.Status.ToString(), entryId = entry.Id });
    }

    if (approval.Action.Equals("wallet.refund", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryDecodeWalletRefundApprovalTarget(approval.EntityId, out var refundTarget))
            return Results.BadRequest(new { code = "invalid_approval_target", message = "اطلاعات بازگشت وجه معتبر نیست." });

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var refundResult = await walletRefundService.RefundWithinTransactionAsync(
                new WalletRefundRequest(refundTarget.CustomerId, refundTarget.Amount, refundTarget.SourceTransactionId, approval.Reason),
                auth.User.Id,
                cancellationToken);

            approval.Status = ApprovalStatus.Approved;
            approval.DecidedByUserId = auth.User.Id;
            approval.DecidedAt = DateTimeOffset.UtcNow;
            approval.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? "تأیید و اجرا شد" : request.Note.Trim();

            database.AuditLogs.Add(new AuditLog
            {
                Action = "WalletRefundApproved",
                EntityName = "CustomerWallet",
                EntityId = refundTarget.CustomerId.ToString(),
                AppUserId = auth.User.Id,
                Details = refundResult.Amount.ToString("0.##") + " تومان · درخواست " + approval.Id
            });

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                id = approval.Id,
                status = approval.Status.ToString(),
                operation = refundResult
            });
        }
        catch (KeyNotFoundException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new { code = "refund_not_found", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { code = "refund_conflict", message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.BadRequest(new { code = "invalid_refund", message = exception.Message });
        }
    }

    if (approval.Action.Equals("invoice.reverse", StringComparison.OrdinalIgnoreCase))
    {
        if (!Guid.TryParse(approval.EntityId, out var invoiceId))
            return Results.BadRequest(new { code = "invalid_approval_target", message = "شناسه فاکتور درخواست تأیید معتبر نیست." });

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var reverseResult = await reverseService.ReverseWithinTransactionAsync(
                invoiceId,
                new InvoiceReverseRequest(auth.User.Id, approval.Reason),
                cancellationToken);

            approval.Status = ApprovalStatus.Approved;
            approval.DecidedByUserId = auth.User.Id;
            approval.DecidedAt = DateTimeOffset.UtcNow;
            approval.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? "تأیید و اجرا شد" : request.Note.Trim();

            database.AuditLogs.Add(new AuditLog
            {
                Action = "ApprovalApproveAndExecute",
                EntityName = "ApprovalRequest",
                EntityId = approval.Id.ToString(),
                AppUserId = auth.User.Id,
                Details = "تأیید و اجرای برگشت فاکتور · " + invoiceId
            });

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                id = approval.Id,
                status = approval.Status.ToString(),
                operation = reverseResult
            });
        }
        catch (KeyNotFoundException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new { code = "invoice_not_found", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { code = "reverse_conflict", message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.BadRequest(new { code = "invalid_reverse", message = exception.Message });
        }
    }

    approval.Status = ApprovalStatus.Approved;
    approval.DecidedByUserId = auth.User.Id;
    approval.DecidedAt = DateTimeOffset.UtcNow;
    approval.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

    database.AuditLogs.Add(new AuditLog
    {
        Action = "ApprovalApprove",
        EntityName = "ApprovalRequest",
        EntityId = approval.Id.ToString(),
        AppUserId = auth.User.Id,
        Details = "تأیید عملیات · " + approval.Action
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = approval.Id, status = approval.Status.ToString() });
})
.WithName("ApproveApprovalRequest");

app.MapPost("/api/approvals/{approvalId:guid}/reject", async (
    Guid approvalId,
    ApprovalDecisionRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "approval.decide", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var approval = await database.ApprovalRequests.FirstOrDefaultAsync(item => item.Id == approvalId, cancellationToken);
    if (approval is null) return Results.NotFound(new { code = "approval_not_found", message = "درخواست تأیید پیدا نشد." });
    if (approval.Status != ApprovalStatus.Pending)
        return Results.Conflict(new { code = "approval_not_pending", message = "این درخواست دیگر در وضعیت انتظار نیست." });

    if (approval.Action.Equals("payroll.entry", StringComparison.OrdinalIgnoreCase)
        && Guid.TryParse(approval.EntityId, out var rejectedPayrollEntryId))
    {
        var entry = await database.PayrollLedgerEntries.FirstOrDefaultAsync(item => item.Id == rejectedPayrollEntryId, cancellationToken);
        if (entry is not null && entry.Status == ApprovalStatus.Pending)
        {
            entry.Status = ApprovalStatus.Rejected;
            database.AuditLogs.Add(new AuditLog
            {
                Action = "PayrollEntryRejected",
                EntityName = "PayrollLedgerEntry",
                EntityId = entry.Id.ToString(),
                AppUserId = auth.User!.Id,
                Details = "رد عملیات حقوقی · " + entry.Kind
            });
        }
    }

    approval.Status = ApprovalStatus.Rejected;
    approval.DecidedByUserId = auth.User!.Id;
    approval.DecidedAt = DateTimeOffset.UtcNow;
    approval.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? "رد شد" : request.Note.Trim();

    database.AuditLogs.Add(new AuditLog
    {
        Action = "ApprovalReject",
        EntityName = "ApprovalRequest",
        EntityId = approval.Id.ToString(),
        AppUserId = auth.User.Id,
        Details = "رد عملیات · " + approval.Action
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = approval.Id, status = approval.Status.ToString() });
})
.WithName("RejectApprovalRequest");

app.MapGet("/api/dashboard", async (HttpContext context,
    GameNetDbContext database, CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(context, database, cancellationToken, "session.start", "session.manage");
    if (auth.Error is not null) return auth.Error;

    var stations = await database.Stations
        .AsNoTracking()
        .OrderBy(station => station.Zone)
        .ThenBy(station => station.Name)
        .ToListAsync(cancellationToken);

    var activeSessions = await database.Sessions
        .AsNoTracking()
        .Where(session => session.State == SessionState.Active)
        .Select(session => new
        {
            session.Id,
            session.StationId,
            CustomerUsername = session.Customer.Username,
            CustomerFullName = session.Customer.FullName,
            CustomerNote = session.Customer.Notes,
            CustomerDebt = database.Invoices
                .Where(invoice => invoice.CustomerId == session.CustomerId && invoice.Status == InvoiceStatus.Draft)
                .Select(invoice => (decimal?)invoice.TotalAmount)
                .Sum() ?? 0m,
            session.StartAt,
            session.EndAt,
            session.PausedAt,
            session.PausedMinutes,
            session.TimeAdjustmentMinutes,
            session.PrepaidAmount
        })
        .ToListAsync(cancellationToken);

    var activeSessionIds = activeSessions.Select(item => item.Id).ToList();
    var buffetTotals = new Dictionary<Guid, decimal>();
    if (activeSessionIds.Count > 0)
    {
        var buffetRows = await database.InvoiceItems
            .AsNoTracking()
            .Where(item => item.Invoice.SessionId.HasValue
                && activeSessionIds.Contains(item.Invoice.SessionId.Value)
                && item.Invoice.Status == InvoiceStatus.Draft
                && item.ProductId.HasValue)
            .Select(item => new { SessionId = item.Invoice.SessionId!.Value, item.Amount })
            .ToListAsync(cancellationToken);

        buffetTotals = buffetRows
            .GroupBy(item => item.SessionId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
    }

    var activeByStation = activeSessions
        .GroupBy(item => item.StationId)
        .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.EndAt).First());

    var now = DateTimeOffset.UtcNow;
    var dtos = stations.Select(station =>
    {
        activeByStation.TryGetValue(station.Id, out var active);
        int? remaining = active?.EndAt is null
            ? null
            : Math.Max(0, (int)Math.Ceiling((active.EndAt.Value - now).TotalMinutes));

        return new StationDto(
            station.Id,
            station.Name,
            station.Zone,
            station.Type,
            (long)station.RatePerHour,
            active?.PausedAt is not null ? "Paused" : station.State.ToString(),
            active?.CustomerUsername,
            active?.CustomerFullName,
            active?.CustomerDebt ?? 0m,
            active?.CustomerNote,
            remaining,
            null,
            active?.Id,
            active is not null && buffetTotals.TryGetValue(active.Id, out var dashboardBuffetTotal) ? dashboardBuffetTotal : 0m,
            null,
            null,
            active?.StartAt,
            active?.PausedAt,
            active?.PausedMinutes ?? 0,
            active?.TimeAdjustmentMinutes ?? 0,
            active?.PrepaidAmount ?? 0m);
    }).ToList();

    return Results.Ok(new DashboardSnapshotDto(dtos.Count, dtos, now));
})
.WithName("GetDashboardSnapshot");


app.MapGet("/api/customers", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(context, database, cancellationToken, "customer.manage", "customer.wallet", "customer.debt");
    if (auth.Error is not null) return auth.Error;

    var canReadWallet = AuthorizationService.HasPermission(auth.User!, "customer.wallet");
    var canReadDebt = AuthorizationService.HasPermission(auth.User!, "customer.debt");

    var customers = await database.Customers
        .AsNoTracking()
        .OrderBy(item => item.Code)
        .ThenBy(item => item.FullName)
        .Select(item => new
        {
            id = item.Id,
            code = item.Code,
            username = item.Username,
             name = item.FullName,
            alias = item.Alias,
            nationalId = item.NationalId,
            mobile = item.Phone,
            vip = item.VipTier,
            wallet = canReadWallet ? item.Balance : 0m,
            debt = canReadDebt
                ? database.Invoices
                    .Where(invoice => invoice.CustomerId == item.Id && invoice.Status == InvoiceStatus.Draft)
                    .Select(invoice => (decimal?)invoice.TotalAmount)
                    .Sum() ?? 0m
                : 0m,
            giftCredit = canReadWallet ? item.FreeMoney : 0m,
            freeTimeMinutes = canReadWallet ? item.FreeTimeMinutes : 0,
            discountLevel = 0,
            lastSeen = "نامشخص",
            status = "active",
            concurrentLoginLimit = item.ConcurrentLoginLimit,
            vipPackageId = item.VipPackageId,
            vipPackageName = item.VipPackage != null ? item.VipPackage.Name : null,
            vipActivatedAt = item.VipActivatedAt,
            vipExpiresAt = item.VipExpiresAt,
            vipDailyMinutes = item.VipPackage != null ? item.VipPackage.DailyMinutes : 0,
            vipTotalMinutes = item.VipPackage != null ? item.VipPackage.TotalMinutes : 0,
            vipDiscountPercent = item.VipPackage != null ? item.VipPackage.DiscountPercent : 0,
            notes = item.Notes
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(customers);
})
.WithName("GetCustomers");

app.MapGet("/api/vip-packages", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var packages = await database.VipPackages.AsNoTracking()
        .Where(item => item.IsActive)
        .OrderBy(item => item.Price)
        .Select(item => new VipPackageDto(
            item.Id,
            item.Name,
            item.Tier,
            item.Price,
            item.DurationDays,
            item.DailyMinutes,
            item.TotalMinutes,
            item.DiscountPercent,
            item.OverflowRule,
            item.Description,
            item.IsActive))
        .ToListAsync(cancellationToken);

    return Results.Ok(packages);
})
.WithName("GetVipPackages");

app.MapPost("/api/vip-packages", async (HttpContext context,
    CreateVipPackageRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest(new { code = "missing_vip_package_name", message = "نام پکیج VIP را وارد کنید." });

    if (request.Price < 0 || request.DurationDays <= 0 || request.DailyMinutes <= 0 || request.TotalMinutes <= 0)
        return Results.BadRequest(new { code = "invalid_vip_package", message = "مقدارهای پکیج VIP معتبر نیستند." });

    var package = new VipPackage
    {
        Name = name,
        Tier = NormalizeVipTier(request.Tier),
        Price = request.Price,
        DurationDays = request.DurationDays,
        DailyMinutes = request.DailyMinutes,
        TotalMinutes = request.TotalMinutes,
        DiscountPercent = Math.Clamp(request.DiscountPercent, 0, 100),
        OverflowRule = string.IsNullOrWhiteSpace(request.OverflowRule) ? "half-hourly" : request.OverflowRule.Trim(),
        Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        IsActive = true
    };

    database.VipPackages.Add(package);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "VipPackageCreate",
        EntityName = "VipPackage",
        EntityId = package.Id.ToString(),
        Details = "ایجاد پکیج VIP · " + package.Name,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(new VipPackageDto(
        package.Id, package.Name, package.Tier, package.Price, package.DurationDays,
        package.DailyMinutes, package.TotalMinutes, package.DiscountPercent,
        package.OverflowRule, package.Description, package.IsActive));
})
.WithName("CreateVipPackage");

app.MapPost("/api/customers/{customerId:guid}/vip-package", async (HttpContext context,
    Guid customerId,
    AssignVipPackageRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var package = await database.VipPackages.FirstOrDefaultAsync(item => item.Id == request.VipPackageId && item.IsActive, cancellationToken);
    if (package is null)
        return Results.NotFound(new { code = "vip_package_not_found", message = "پکیج VIP پیدا نشد یا غیرفعال است." });

    var activatedAt = DateTimeOffset.UtcNow;
    customer.VipPackageId = package.Id;
    customer.VipPackage = package;
    customer.VipActivatedAt = activatedAt;
    customer.VipExpiresAt = activatedAt.AddDays(package.DurationDays);
    customer.VipTier = package.Tier;
    customer.IsVip = package.Tier != "none";

    database.AuditLogs.Add(new AuditLog
    {
        Action = "VipPackageAssign",
        EntityName = "Customer",
        EntityId = customer.Id.ToString(),
        Details = "تخصیص پکیج VIP · " + package.Name + " · تا " + customer.VipExpiresAt.Value.ToString("O"),
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);

    return Results.Ok(new
    {
        customerId = customer.Id,
        packageId = package.Id,
        packageName = package.Name,
        vipTier = customer.VipTier,
        activatedAt = customer.VipActivatedAt,
        expiresAt = customer.VipExpiresAt,
        dailyMinutes = package.DailyMinutes,
        totalMinutes = package.TotalMinutes,
        discountPercent = package.DiscountPercent
    });
})
.WithName("AssignVipPackage");

app.MapPost("/api/customers", async (HttpContext context,
    CreateCustomerRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var fullName = request.FullName?.Trim();
    if (string.IsNullOrWhiteSpace(fullName))
        return Results.BadRequest(new { code = "missing_customer_name", message = "نام کامل مشتری را وارد کنید." });

    var vipTier = NormalizeVipTier(request.VipTier);
    var code = request.Code?.Trim();
    var username = request.Username?.Trim();
    var phone = request.Phone?.Trim();
    var email = request.Email?.Trim();
    var nationalId = request.NationalId?.Trim();

    if (string.IsNullOrWhiteSpace(code))
    {
        var codes = await database.Customers.AsNoTracking()
            .Where(item => item.Code != null)
            .Select(item => item.Code!)
            .ToListAsync(cancellationToken);
        var next = codes.Select(value => int.TryParse(value, out var number) ? number : 1049)
            .DefaultIfEmpty(1049)
            .Max() + 1;
        code = next.ToString();
    }

    username = string.IsNullOrWhiteSpace(username) ? "user" + code : username;

    if (await database.Customers.AnyAsync(item => item.Code == code, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_code", message = "این کد مشتری قبلاً استفاده شده است." });
    if (await database.Customers.AnyAsync(item => item.Username == username, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_username", message = "این نام کاربری قبلاً استفاده شده است." });
    if (!string.IsNullOrWhiteSpace(phone) && await database.Customers.AnyAsync(item => item.Phone == phone, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_phone", message = "این شماره موبایل قبلاً ثبت شده است." });
    if (!string.IsNullOrWhiteSpace(nationalId) && await database.Customers.AnyAsync(item => item.NationalId == nationalId, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_national_id", message = "این کد ملی قبلاً ثبت شده است." });

    var customer = new Customer
    {
        FullName = fullName,
        Code = code,
        Username = username,
        Alias = string.IsNullOrWhiteSpace(request.Alias) ? null : request.Alias.Trim(),
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId,
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
        Email = string.IsNullOrWhiteSpace(email) ? null : email,
        VipTier = vipTier,
        IsVip = vipTier != "none",
        ConcurrentLoginLimit = Math.Max(1, request.ConcurrentLoginLimit),
        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        PasswordHash = string.IsNullOrWhiteSpace(request.Password) ? null : HashPassword(request.Password)
    };

    database.Customers.Add(customer);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerCreate",
        EntityName = "Customer",
        EntityId = customer.Id.ToString(),
        Details = "ایجاد مشتری · " + customer.Code + " · " + customer.FullName,
        AppUserId = auth.User!.Id
    });

    try
    {
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException)
    {
        return Results.Conflict(new { code = "customer_unique_conflict", message = "اطلاعات مشتری با رکورد دیگری تداخل دارد." });
    }

    return Results.Ok(ToCustomerDto(customer));
})
.WithName("CreateCustomer");

app.MapPut("/api/customers/{customerId:guid}", async (HttpContext context,
    Guid customerId,
    UpdateCustomerRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var fullName = request.FullName?.Trim();
    if (string.IsNullOrWhiteSpace(fullName))
        return Results.BadRequest(new { code = "missing_customer_name", message = "نام کامل مشتری را وارد کنید." });

    var code = request.Code?.Trim();
    var username = request.Username?.Trim();
    var phone = request.Phone?.Trim();
    var nationalId = request.NationalId?.Trim();
    var vipTier = NormalizeVipTier(request.VipTier);

    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest(new { code = "missing_customer_code", message = "کد مشتری را وارد کنید." });

    if (await database.Customers.AnyAsync(item => item.Id != customerId && item.Code == code, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_code", message = "این کد مشتری قبلاً استفاده شده است." });
    if (!string.IsNullOrWhiteSpace(username) && await database.Customers.AnyAsync(item => item.Id != customerId && item.Username == username, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_username", message = "این نام کاربری قبلاً استفاده شده است." });
    if (!string.IsNullOrWhiteSpace(phone) && await database.Customers.AnyAsync(item => item.Id != customerId && item.Phone == phone, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_phone", message = "این شماره موبایل قبلاً ثبت شده است." });
    if (!string.IsNullOrWhiteSpace(nationalId) && await database.Customers.AnyAsync(item => item.Id != customerId && item.NationalId == nationalId, cancellationToken))
        return Results.Conflict(new { code = "duplicate_customer_national_id", message = "این کد ملی قبلاً ثبت شده است." });

    if (vipTier != "none" && customer.VipPackageId is not null)
    {
        var assignedPackage = await database.VipPackages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == customer.VipPackageId, cancellationToken);
        if (assignedPackage is not null && NormalizeVipTier(assignedPackage.Tier) != vipTier)
            return Results.Conflict(new { code = "vip_tier_package_mismatch", message = "سطح VIP مشتری با پکیج فعال سازگار نیست؛ ابتدا پکیج را اصلاح کنید." });
    }

    customer.FullName = fullName;
    customer.Code = code;
    customer.Username = string.IsNullOrWhiteSpace(username) ? "user" + code : username;
    customer.Alias = string.IsNullOrWhiteSpace(request.Alias) ? null : request.Alias.Trim();
    customer.NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId;
    customer.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone;
    customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
    customer.VipTier = vipTier;
    customer.IsVip = vipTier != "none";
    if (vipTier == "none")
    {
        customer.VipPackageId = null;
        customer.VipActivatedAt = null;
        customer.VipExpiresAt = null;
    }
    customer.ConcurrentLoginLimit = Math.Max(1, request.ConcurrentLoginLimit);
    customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerUpdate",
        EntityName = "Customer",
        EntityId = customer.Id.ToString(),
        Details = "ویرایش مشتری · " + customer.Code + " · " + customer.FullName,
        AppUserId = auth.User!.Id
    });

    try
    {
        await database.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException)
    {
        return Results.Conflict(new { code = "customer_unique_conflict", message = "اطلاعات مشتری با رکورد دیگری تداخل دارد." });
    }

    return Results.Ok(ToCustomerDto(customer));
})
.WithName("UpdateCustomer");

app.MapPost("/api/customers/{customerId:guid}/password", async (HttpContext context,
    Guid customerId,
    ChangeCustomerPasswordRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        return Results.BadRequest(new { code = "invalid_password", message = "رمز عبور باید حداقل ۶ نویسه داشته باشد." });

    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    customer.PasswordHash = HashPassword(request.Password);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerPasswordChanged",
        EntityName = "Customer",
        EntityId = customer.Id.ToString(),
        Details = "تغییر رمز ورود مشتری",
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { changed = true });
})
.WithName("ChangeCustomerPassword");

app.MapGet("/api/client/identity", async (
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var remoteIp = context.Connection.RemoteIpAddress;
    var local = remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);

    AgentDevice? device = null;
    if (remoteIp is not null && !local)
    {
        var ipText = remoteIp.ToString();
        var matchingDevices = await database.AgentDevices
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.IsActive && item.IsOnline && item.LastIpAddress == ipText)
            .ToListAsync(cancellationToken);
        device = matchingDevices.OrderByDescending(item => item.LastSeenAt).FirstOrDefault();
    }
    else
    {
        var loopbackDevices = await database.AgentDevices
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.IsActive && item.IsOnline && item.LastSeenAt.HasValue)
            .ToListAsync(cancellationToken);
        device = loopbackDevices.OrderByDescending(item => item.LastSeenAt).FirstOrDefault();
    }

    if (device is null)
        return Results.NotFound(new { code = "client_identity_not_found", message = "Agent این رایانه پیدا نشد." });

    return Results.Ok(new
    {
        deviceId = device.DeviceId,
        stationId = device.StationId,
        stationName = device.Station?.Name,
        isOnline = device.IsOnline
    });
})
.WithName("GetClientIdentity");

app.MapPost("/api/customer-auth/login", async (
    CustomerLoginAuthRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var key = request.UsernameOrCode?.Trim();
    var clientKey = request.ClientKey?.Trim();

    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(clientKey))
        return Results.BadRequest(new { code = "missing_credentials", message = "نام کاربری، رمز و شناسه دستگاه الزامی است." });

    var customer = await database.Customers
        .FirstOrDefaultAsync(item => item.Username == key || item.Code == key, cancellationToken);

    if (customer is null || string.IsNullOrWhiteSpace(customer.PasswordHash) || !VerifyPassword(request.Password, customer.PasswordHash))
        return Results.Unauthorized();

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var active = await database.CustomerLogins
        .Where(item => item.CustomerId == customer.Id && item.IsActive)
        .ToListAsync(cancellationToken);

    var existing = active.FirstOrDefault(item => item.ClientKey == clientKey);
    if (existing is not null)
    {
        return Results.Ok(new
        {
            authenticated = true,
            customerId = customer.Id,
            username = customer.Username,
            fullName = customer.FullName,
            loginId = existing.Id,
            activeCount = active.Count,
            limit = customer.ConcurrentLoginLimit,
            balance = customer.Balance,
            freeMoney = customer.FreeMoney,
            freeTimeMinutes = customer.FreeTimeMinutes,
            vipTier = customer.VipTier
        });
    }

    if (active.Count >= Math.Max(1, customer.ConcurrentLoginLimit))
        return Results.Conflict(new
        {
            code = "concurrent_login_limit",
            message = "تعداد ورود هم‌زمان این مشتری به سقف مجاز رسیده است.",
            activeCount = active.Count,
            limit = customer.ConcurrentLoginLimit
        });

    var login = new CustomerLogin
    {
        CustomerId = customer.Id,
        ClientKey = clientKey,
        LoggedInAt = DateTimeOffset.UtcNow,
        IsActive = true
    };
    database.CustomerLogins.Add(login);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerAuthenticated",
        EntityName = "CustomerLogin",
        EntityId = login.Id.ToString(),
        Details = "ورود با رمز · دستگاه " + clientKey
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new
    {
        authenticated = true,
        customerId = customer.Id,
        username = customer.Username,
        fullName = customer.FullName,
        loginId = login.Id,
        activeCount = active.Count + 1,
        limit = customer.ConcurrentLoginLimit,
        balance = customer.Balance,
        freeMoney = customer.FreeMoney,
        freeTimeMinutes = customer.FreeTimeMinutes,
        vipTier = customer.VipTier
    });
})
.WithName("CustomerAuthenticate");

app.MapGet("/api/customer-auth/state", async (
    Guid customerId,
    Guid loginId,
    string clientKey,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var normalizedClientKey = clientKey?.Trim();
    if (string.IsNullOrWhiteSpace(normalizedClientKey))
        return Results.BadRequest(new { code = "missing_client_key", message = "شناسه دستگاه وارد نشده است." });

    var customer = await database.Customers
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var login = await database.CustomerLogins
        .FirstOrDefaultAsync(
            item => item.Id == loginId
                && item.CustomerId == customerId
                && item.IsActive
                && item.ClientKey == normalizedClientKey,
            cancellationToken);

    if (login is null)
        return Results.Ok(new
        {
            authenticated = false,
            customerId,
            loginId,
            username = customer.Username,
            fullName = customer.FullName,
            balance = customer.Balance,
            freeMoney = customer.FreeMoney,
            freeTimeMinutes = customer.FreeTimeMinutes,
            vipTier = customer.VipTier,
            session = (object?)null
        });

    var device = await database.AgentDevices
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.DeviceId == normalizedClientKey && item.IsActive, cancellationToken);

    var session = device?.StationId is Guid stationId
        ? await database.Sessions
            .Where(item => item.CustomerId == customerId
                && item.StationId == stationId
                && (item.State == SessionState.Active || item.State == SessionState.Ended))
            .OrderByDescending(item => item.StartAt)
            .Select(item => new
            {
                id = item.Id,
                state = item.State.ToString(),
                startAt = item.StartAt,
                endAt = item.EndAt,
                stationName = item.Station.Name
            })
            .FirstOrDefaultAsync(cancellationToken)
        : null;

    return Results.Ok(new
    {
        authenticated = true,
        customerId,
        loginId,
        username = customer.Username,
        fullName = customer.FullName,
        balance = customer.Balance,
        freeMoney = customer.FreeMoney,
        freeTimeMinutes = customer.FreeTimeMinutes,
        vipTier = customer.VipTier,
        session
    });
})
.WithName("CustomerAuthState");

app.MapPost("/api/customers/{customerId:guid}/login-acquire", async (
    Guid customerId,
    CustomerLoginRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var clientKey = request.ClientKey?.Trim();
    if (string.IsNullOrWhiteSpace(clientKey))
        return Results.BadRequest(new { code = "missing_client_key", message = "شناسه دستگاه وارد نشده است." });

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var active = await database.CustomerLogins
        .Where(item => item.CustomerId == customerId && item.IsActive)
        .ToListAsync(cancellationToken);

    var existing = active.FirstOrDefault(item => item.ClientKey == clientKey);
    if (existing is not null)
        return Results.Ok(new ConcurrentLoginResultDto(true, existing.Id, active.Count, customer.ConcurrentLoginLimit));

    if (active.Count >= Math.Max(1, customer.ConcurrentLoginLimit))
        return Results.Conflict(new { code = "concurrent_login_limit", message = "تعداد ورود هم‌زمان این مشتری به سقف مجاز رسیده است.", activeCount = active.Count, limit = customer.ConcurrentLoginLimit });

    var login = new CustomerLogin
    {
        CustomerId = customerId,
        ClientKey = clientKey,
        LoggedInAt = DateTimeOffset.UtcNow,
        IsActive = true
    };

    database.CustomerLogins.Add(login);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerLoginAcquire",
        EntityName = "CustomerLogin",
        EntityId = login.Id.ToString(),
        Details = "ورود مشتری · دستگاه " + clientKey
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new ConcurrentLoginResultDto(true, login.Id, active.Count + 1, customer.ConcurrentLoginLimit));
})
.WithName("AcquireCustomerLogin");

app.MapPost("/api/customers/{customerId:guid}/login-release", async (
    Guid customerId,
    CustomerLoginReleaseRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var login = await database.CustomerLogins.FirstOrDefaultAsync(item => item.CustomerId == customerId && item.IsActive && item.ClientKey == request.ClientKey, cancellationToken);
    if (login is null)
        return Results.NotFound(new { code = "login_not_found", message = "ورود فعال برای این دستگاه پیدا نشد." });

    login.IsActive = false;
    login.LoggedOutAt = DateTimeOffset.UtcNow;
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerLoginRelease",
        EntityName = "CustomerLogin",
        EntityId = login.Id.ToString(),
        Details = "خروج مشتری · دستگاه " + request.ClientKey
    });

    await database.SaveChangesAsync(cancellationToken);
    var activeCount = await database.CustomerLogins.CountAsync(item => item.CustomerId == customerId && item.IsActive, cancellationToken);
    return Results.Ok(new { released = true, activeCount });
})
.WithName("ReleaseCustomerLogin");

app.MapGet("/api/buffet/products", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(context, database, cancellationToken, "buffet.sell", "buffet.inventory");
    if (auth.Error is not null) return auth.Error;

    var products = await database.Products
        .AsNoTracking()
        .Where(item => item.IsActive)
        .OrderBy(item => item.Category)
        .ThenBy(item => item.Name)
        .Select(item => new
        {
            id = item.Id,
            name = item.Name,
            category = item.Category,
            price = item.UnitPrice,
            buyPrice = item.CostPrice,
            stock = item.StockQuantity,
            minimumStock = item.MinimumStock,
            unit = item.Unit,
            lowStock = item.StockQuantity <= item.MinimumStock,
            active = item.IsActive
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(products);
})
.WithName("GetBuffetProducts");

app.MapPost("/api/buffet/products", async (HttpContext context,
    CreateBuffetProductRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "buffet.inventory", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (string.IsNullOrWhiteSpace(request.Name) || request.UnitPrice < 0 || request.CostPrice < 0 || request.InitialStock < 0)
        return Results.BadRequest(new { code = "invalid_product", message = "اطلاعات محصول معتبر نیست." });

    var product = new Product
    {
        Name = request.Name.Trim(),
        Category = string.IsNullOrWhiteSpace(request.Category) ? "سایر" : request.Category.Trim(),
        UnitPrice = request.UnitPrice,
        CostPrice = request.CostPrice,
        StockQuantity = request.InitialStock,
        MinimumStock = Math.Max(0, request.MinimumStock),
        Unit = string.IsNullOrWhiteSpace(request.Unit) ? "عدد" : request.Unit.Trim(),
        IsActive = true
    };
    database.Products.Add(product);
    if (request.InitialStock > 0)
    {
        database.InventoryTransactions.Add(new InventoryTransaction
        {
            Product = product,
            Quantity = request.InitialStock,
            UnitPrice = product.UnitPrice,
            UnitCost = product.CostPrice,
            Direction = TransactionDirection.In,
            Kind = "Initial",
            AppUserId = auth.User!.Id,
            Notes = "موجودی اولیه"
        });
    }
    database.AuditLogs.Add(new AuditLog
    {
        Action = "BuffetProductCreated",
        EntityName = "Product",
        EntityId = product.Id.ToString(),
        Details = product.Name,
        AppUserId = auth.User!.Id
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        id = product.Id,
        name = product.Name,
        category = product.Category,
        price = product.UnitPrice,
        buyPrice = product.CostPrice,
        stock = product.StockQuantity,
        minimumStock = product.MinimumStock,
        unit = product.Unit,
        lowStock = product.StockQuantity <= product.MinimumStock,
        active = product.IsActive
    });
})
.WithName("CreateBuffetProduct");

app.MapPost("/api/buffet/products/{productId:guid}/stock", async (
    Guid productId,
    StockAdjustmentRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "buffet.inventory", cancellationToken);
    if (auth.Error is not null) return auth.Error;
    request = request with { AppUserId = auth.User!.Id };

    if (request.Quantity <= 0)
        return Results.BadRequest(new { code = "invalid_quantity", message = "تعداد باید بیشتر از صفر باشد." });

    if (!Enum.TryParse<TransactionDirection>(request.Direction?.Trim(), true, out var direction))
        return Results.BadRequest(new { code = "invalid_direction", message = "نوع حرکت موجودی معتبر نیست." });

    var kind = string.IsNullOrWhiteSpace(request.Kind) ? "Adjustment" : request.Kind.Trim();
    if (!new[] { "Adjustment", "Purchase", "Sale", "Waste", "Return" }.Contains(kind, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { code = "invalid_inventory_kind", message = "نوع حرکت موجودی معتبر نیست." });

    if (kind.Equals("Waste", StringComparison.OrdinalIgnoreCase) && direction != TransactionDirection.Out)
        return Results.BadRequest(new { code = "invalid_waste_direction", message = "ضایعات باید خروجی باشد." });

    if (kind.Equals("Return", StringComparison.OrdinalIgnoreCase) && direction != TransactionDirection.In)
        return Results.BadRequest(new { code = "invalid_return_direction", message = "مرجوعی باید ورودی باشد." });

    if ((kind.Equals("Waste", StringComparison.OrdinalIgnoreCase) || kind.Equals("Return", StringComparison.OrdinalIgnoreCase))
        && string.IsNullOrWhiteSpace(request.Notes))
        return Results.BadRequest(new { code = "missing_inventory_reason", message = "دلیل ضایعات یا مرجوعی را وارد کنید." });

    var product = await database.Products.FirstOrDefaultAsync(item => item.Id == productId && item.IsActive, cancellationToken);
    if (product is null)
        return Results.NotFound(new { code = "product_not_found", message = "محصول پیدا نشد." });

    if (direction == TransactionDirection.Out && product.StockQuantity < request.Quantity)
        return Results.Conflict(new { code = "insufficient_stock", message = "موجودی برای این خروج کافی نیست." });

    var unitCost = request.UnitCost ?? product.CostPrice;
    if (kind.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
        && (!request.UnitCost.HasValue || request.UnitCost.Value <= 0))
        return Results.BadRequest(new { code = "missing_purchase_cost", message = "بهای خرید هر واحد را وارد کنید." });

    var oldStock = product.StockQuantity;
    var oldCost = product.CostPrice;
    product.StockQuantity += direction == TransactionDirection.In ? request.Quantity : -request.Quantity;

    if (kind.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
    {
        var newStock = product.StockQuantity;
        product.CostPrice = newStock <= 0
            ? unitCost
            : ((oldStock * oldCost) + (request.Quantity * unitCost)) / newStock;
    }

    database.InventoryTransactions.Add(new InventoryTransaction
    {
        ProductId = product.Id,
        Quantity = request.Quantity,
        UnitPrice = product.UnitPrice,
        UnitCost = unitCost,
        Direction = direction,
        Kind = kind,
        AppUserId = auth.User!.Id,
        Notes = request.Notes
    });
    database.AuditLogs.Add(new AuditLog
    {
        Action = direction == TransactionDirection.In ? "InventoryIncrease" : "InventoryDecrease",
        EntityName = "Product",
        EntityId = product.Id.ToString(),
        Details = kind + " · " + request.Quantity.ToString() + " · " + (request.Notes ?? ""),
        AppUserId = auth.User!.Id
    });
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = product.Id, stock = product.StockQuantity, lowStock = product.StockQuantity <= product.MinimumStock, kind });
})
.WithName("AdjustBuffetStock");
 
app.MapPut("/api/buffet/products/{productId:guid}", async (HttpContext context,
    Guid productId,
    UpdateBuffetProductRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "buffet.inventory", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var product = await database.Products.FirstOrDefaultAsync(item => item.Id == productId, cancellationToken);
    if (product is null)
        return Results.NotFound(new { code = "product_not_found", message = "محصول پیدا نشد." });

    if (string.IsNullOrWhiteSpace(request.Name) || request.UnitPrice < 0 || request.CostPrice < 0 || request.MinimumStock < 0)
        return Results.BadRequest(new { code = "invalid_product", message = "اطلاعات محصول معتبر نیست." });

    product.Name = request.Name.Trim();
    product.Category = string.IsNullOrWhiteSpace(request.Category) ? "سایر" : request.Category.Trim();
    product.UnitPrice = request.UnitPrice;
    product.CostPrice = request.CostPrice;
    product.MinimumStock = request.MinimumStock;
    product.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "عدد" : request.Unit.Trim();
    product.IsActive = request.IsActive;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "BuffetProductUpdated",
        EntityName = "Product",
        EntityId = product.Id.ToString(),
        Details = product.Name + " · " + product.UnitPrice.ToString("0.##") + " تومان",
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        id = product.Id,
        name = product.Name,
        category = product.Category,
        price = product.UnitPrice,
        buyPrice = product.CostPrice,
        stock = product.StockQuantity,
        minimumStock = product.MinimumStock,
        unit = product.Unit,
        lowStock = product.StockQuantity <= product.MinimumStock,
        active = product.IsActive
    });
})
.WithName("UpdateBuffetProduct");

app.MapGet("/api/buffet/inventory-transactions", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "buffet.inventory", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var rows = await database.InventoryTransactions
        .AsNoTracking()
        .Include(item => item.Product)
        .Take(300)
        .Select(item => new
        {
            id = item.Id,
            productId = item.ProductId,
            productName = item.Product.Name,
            quantity = item.Quantity,
            unitPrice = item.UnitPrice,
            unitCost = item.UnitCost,
            referenceInvoiceId = item.ReferenceInvoiceId,
            direction = item.Direction.ToString(),
            kind = item.Kind,
            notes = item.Notes,
            createdAt = item.CreatedAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(rows
        .OrderByDescending(item => item.createdAt)
        .Take(200)
        .ToList());
})
.WithName("GetInventoryTransactions");


app.MapGet("/api/buffet/reports/profit", async (HttpContext context,
    DateTimeOffset? from, DateTimeOffset? to, GameNetDbContext database, CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var start = from ?? DateTimeOffset.UtcNow.Date.AddDays(-30);
    var end = to ?? DateTimeOffset.UtcNow;
    var inventory = await database.InventoryTransactions
        .Include(item => item.Product)
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    var rows = inventory
        .Where(item => item.CreatedAt >= start && item.CreatedAt <= end
            && (item.Kind == "Sale" || item.Kind == "Purchase" || item.Kind == "Waste" || item.Kind == "Return"))
        .Select(item => new { item.ProductId, productName = item.Product.Name, item.Quantity, item.UnitPrice, item.UnitCost, item.Direction, item.Kind, item.ReferenceInvoiceId })
        .ToList();
    var products = rows.GroupBy(item => new { item.ProductId, item.productName }).Select(group =>
    {
        var sales = group.Where(item => item.Kind == "Sale");
        var returns = group.Where(item => item.Kind == "Return" && item.ReferenceInvoiceId.HasValue);
        var purchases = group.Where(item => item.Kind == "Purchase" && item.Direction == TransactionDirection.In);
        var waste = group.Where(item => item.Kind == "Waste" && item.Direction == TransactionDirection.Out);
        var salesRevenue = sales.Sum(item => item.Quantity * item.UnitPrice);
        var salesCost = sales.Sum(item => item.Quantity * item.UnitCost);
        var returnRevenue = returns.Sum(item => item.Quantity * item.UnitPrice);
        var returnCost = returns.Sum(item => item.Quantity * item.UnitCost);
        return new {
            productId = group.Key.ProductId, productName = group.Key.productName,
            salesQuantity = sales.Sum(item => item.Quantity), salesRevenue, salesCost,
            returnQuantity = returns.Sum(item => item.Quantity), returnRevenue, returnCost,
            purchaseQuantity = purchases.Sum(item => item.Quantity),
            purchaseCost = purchases.Sum(item => item.Quantity * item.UnitCost),
            wasteQuantity = waste.Sum(item => item.Quantity),
            wasteCost = waste.Sum(item => item.Quantity * item.UnitCost),
            grossProfit = salesRevenue - salesCost - returnRevenue + returnCost
        };
    }).OrderByDescending(item => item.grossProfit).ToList();
    return Results.Ok(new {
        from = start, to = end,
        totals = new {
            salesRevenue = products.Sum(item => item.salesRevenue),
            salesCost = products.Sum(item => item.salesCost),
            returnRevenue = products.Sum(item => item.returnRevenue),
            returnCost = products.Sum(item => item.returnCost),
            purchaseCost = products.Sum(item => item.purchaseCost),
            wasteCost = products.Sum(item => item.wasteCost),
            grossProfit = products.Sum(item => item.grossProfit)
        }, products
    });
})
.WithName("GetBuffetProfitReport");


app.MapPost("/api/buffet/sales", async (
    BuffetSaleRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(context, database, cancellationToken, "buffet.sell", "buffet.inventory");
    if (auth.Error is not null) return auth.Error;
    request = request with { AppUserId = auth.User!.Id };

    if (request.Items is null || request.Items.Count == 0)
        return Results.BadRequest(new { code = "empty_sale", message = "سبد فروش خالی است." });

    var target = request.Target?.Trim().ToLowerInvariant();
    if (target is not ("session" or "standalone"))
        return Results.BadRequest(new { code = "invalid_sale_target", message = "نوع مقصد فروش معتبر نیست." });

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    Session? session = null;
    Invoice? invoice = null;

    if (target == "session")
    {
        if (!request.SessionId.HasValue || request.SessionId.Value == Guid.Empty)
            return Results.BadRequest(new { code = "missing_session", message = "برای فروش جلسه، جلسه فعال را انتخاب کنید." });

        session = await database.Sessions
            .FirstOrDefaultAsync(item => item.Id == request.SessionId.Value && item.State == SessionState.Active, cancellationToken);

        if (session is null)
            return Results.Conflict(new { code = "session_not_active", message = "جلسه انتخاب‌شده فعال نیست." });

        invoice = await database.Invoices
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.SessionId == session.Id && item.Status == InvoiceStatus.Draft, cancellationToken);

        if (invoice is null)
        {
            invoice = new Invoice
            {
                CustomerId = session.CustomerId,
                SessionId = session.Id,
                AppUserId = auth.User!.Id,
                TotalAmount = 0m,
                Status = InvoiceStatus.Draft,
                IssuedAt = DateTimeOffset.UtcNow
            };
            database.Invoices.Add(invoice);
        }
    }

    var ids = request.Items.Select(item => item.ProductId).Distinct().ToList();
    var products = await database.Products.Where(item => ids.Contains(item.Id) && item.IsActive).ToListAsync(cancellationToken);
    var byId = products.ToDictionary(item => item.Id);

    decimal total = 0m;
    foreach (var item in request.Items)
    {
        if (item.Quantity <= 0 || !byId.TryGetValue(item.ProductId, out var product))
            return Results.BadRequest(new { code = "invalid_sale_item", message = "یکی از اقلام فروش معتبر نیست." });
        if (product.StockQuantity < item.Quantity)
            return Results.Conflict(new { code = "insufficient_stock", message = "موجودی «" + product.Name + "» کافی نیست." });
        total += product.UnitPrice * item.Quantity;
    }

    foreach (var item in request.Items)
    {
        var product = byId[item.ProductId];
        product.StockQuantity -= item.Quantity;
        database.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = product.Id,
            Quantity = item.Quantity,
            UnitPrice = product.UnitPrice,
            UnitCost = product.CostPrice,
            ReferenceInvoiceId = invoice?.Id,
            Direction = TransactionDirection.Out,
            Kind = "Sale",
            AppUserId = auth.User!.Id,
            Notes = target == "session" ? "فروش به جلسه" : "فروش مستقل"
        });

        if (invoice is not null)
        {
            database.InvoiceItems.Add(new InvoiceItem
            {
                Invoice = invoice,
                ProductId = product.Id,
                Description = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.UnitPrice,
                Amount = product.UnitPrice * item.Quantity
            });
        }
    }

    if (invoice is not null)
        invoice.TotalAmount += total;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "BuffetSale",
        EntityName = target == "session" ? "Invoice" : "Buffet",
        EntityId = invoice?.Id.ToString() ?? Guid.NewGuid().ToString(),
        Details = target + " · " + total.ToString("0.##") + " تومان",
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    var buffetTotal = invoice?.Items.Where(item => item.ProductId.HasValue).Sum(item => item.Amount) ?? 0m;
    return Results.Ok(new { total, target, sessionId = session?.Id, invoiceId = invoice?.Id, buffetTotal });
})
.WithName("CreateBuffetSale");

app.MapPost("/api/customers/{customerId:guid}/debt", async (HttpContext context,
    Guid customerId,
    CustomerDebtRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.debt", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (request.Amount <= 0)
        return Results.BadRequest(new { code = "invalid_debt_amount", message = "مبلغ بدهی باید بیشتر از صفر باشد." });

    var description = request.Description?.Trim();
    if (string.IsNullOrWhiteSpace(description))
        description = "ثبت بدهی مشتری";

    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var invoice = new Invoice
    {
        CustomerId = customer.Id,
        AppUserId = auth.User!.Id,
        TotalAmount = request.Amount,
        Status = InvoiceStatus.Draft,
        IssuedAt = DateTimeOffset.UtcNow,
        Items =
        {
            new InvoiceItem
            {
                Description = description,
                Quantity = 1,
                UnitPrice = request.Amount,
                Amount = request.Amount
            }
        }
    };

    database.Invoices.Add(invoice);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerDebtCreated",
        EntityName = "Invoice",
        EntityId = invoice.Id.ToString(),
        Details = request.Amount.ToString("0.##") + " تومان · " + description,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { invoiceId = invoice.Id, customerId = customer.Id, amount = request.Amount, description });
})
.WithName("CreateCustomerDebt");

app.MapGet("/api/customers/{customerId:guid}/debts", async (HttpContext context,
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.debt", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var debts = await database.Invoices
        .AsNoTracking()
        .Where(item => item.CustomerId == customerId && item.Status == InvoiceStatus.Draft)
        .Select(item => new
        {
            id = item.Id,
            amount = item.TotalAmount,
            issuedAt = item.IssuedAt,
            description = item.Items.Select(line => line.Description).FirstOrDefault() ?? "بدهی مشتری"
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(debts.OrderBy(item => item.issuedAt).ToList());
})
.WithName("GetCustomerDebts");

app.MapPost("/api/customers/{customerId:guid}/debts/{invoiceId:guid}/settle", async (HttpContext context,
    Guid customerId,
    Guid invoiceId,
    CustomerDebtSettlementRequest request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.debt", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var method = request.Method?.Trim().ToLowerInvariant();
    if (method is not ("cash" or "card" or "wallet"))
        return Results.BadRequest(new { code = "invalid_debt_settlement_method", message = "روش تسویه بدهی معتبر نیست." });

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

    var invoice = await database.Invoices
        .Include(item => item.Customer)
        .FirstOrDefaultAsync(item => item.Id == invoiceId && item.CustomerId == customerId && item.Status == InvoiceStatus.Draft, cancellationToken);

    if (invoice is null)
        return Results.NotFound(new { code = "debt_not_found", message = "بدهی موردنظر پیدا نشد یا قبلاً تسویه شده است." });

    if (method == "wallet")
    {
        if (invoice.Customer.Balance < invoice.TotalAmount)
            return Results.Conflict(new { code = "insufficient_balance", message = "موجودی کیف پول برای تسویه این بدهی کافی نیست." });

        invoice.Customer.Balance -= invoice.TotalAmount;
        database.WalletTransactions.Add(new WalletTransaction
        {
            CustomerId = customerId,
            Amount = invoice.TotalAmount,
            Type = WalletTransactionType.Debit,
            ReferenceInvoiceId = invoice.Id,
            Description = "تسویه بدهی از کیف پول"
        });
    }

    invoice.Status = InvoiceStatus.Paid;
    invoice.PaidAt = DateTimeOffset.UtcNow;
    database.InvoicePayments.Add(new InvoicePayment
    {
        InvoiceId = invoice.Id,
        Method = method,
        Amount = invoice.TotalAmount
    });
    database.AuditLogs.Add(new AuditLog
    {
        Action = "CustomerDebtSettled",
        EntityName = "Invoice",
        EntityId = invoice.Id.ToString(),
        Details = invoice.TotalAmount.ToString("0.##") + " تومان · " + method,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new
    {
        invoiceId = invoice.Id,
        customerId,
        amount = invoice.TotalAmount,
        method,
        debtRemaining = await database.Invoices
            .Where(item => item.CustomerId == customerId && item.Status == InvoiceStatus.Draft)
            .Select(item => (decimal?)item.TotalAmount)
            .SumAsync() ?? 0m,
        walletBalanceAfter = invoice.Customer.Balance
    });
})
.WithName("SettleCustomerDebt");

app.MapGet("/api/customers/{customerId:guid}/vip-usage", async (HttpContext context,
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers.AsNoTracking()
        .Include(item => item.VipPackage)
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    if (customer.VipPackageId is null || customer.VipPackage is null || customer.VipActivatedAt is null)
        return Results.Ok(new { active = false, usedTodayMinutes = 0, remainingTodayMinutes = 0, usedTotalMinutes = 0, remainingTotalMinutes = 0 });

    var now = DateTimeOffset.UtcNow;
    var activatedAt = customer.VipActivatedAt.Value;
    var expiresAt = customer.VipExpiresAt;
    var usageEnd = expiresAt.HasValue && expiresAt.Value < now ? expiresAt.Value : now;
    var packageUsable = usageEnd > activatedAt;

    if (!packageUsable)
    {
        return Results.Ok(new
        {
            active = false,
            usedTodayMinutes = 0,
            remainingTodayMinutes = 0,
            usedTotalMinutes = 0,
            remainingTotalMinutes = 0
        });
    }

    var todayStart = now.Date;
    var sessions = await database.Sessions.AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .Select(item => new { item.StartAt, item.EndAt })
        .ToListAsync(cancellationToken);

    var totalUsed = 0;
    var todayUsed = 0;
    foreach (var session in sessions)
    {
        if (session.StartAt >= usageEnd)
            continue;

        var rawEnd = session.EndAt ?? usageEnd;
        var end = rawEnd > usageEnd ? usageEnd : rawEnd;
        var start = session.StartAt < activatedAt ? activatedAt : session.StartAt;
        if (end <= start)
            continue;

        var minutes = (int)Math.Ceiling(Math.Max(0, (end - start).TotalMinutes));
        totalUsed += minutes;

        var todayStartAt = start < todayStart ? todayStart : start;
        if (end > todayStart && todayStartAt < end)
            todayUsed += (int)Math.Ceiling(Math.Max(0, (end - todayStartAt).TotalMinutes));
    }

    return Results.Ok(new
    {
        active = expiresAt is null || expiresAt.Value > now,
        usedTodayMinutes = todayUsed,
        remainingTodayMinutes = Math.Max(0, customer.VipPackage.DailyMinutes - todayUsed),
        usedTotalMinutes = totalUsed,
        remainingTotalMinutes = Math.Max(0, customer.VipPackage.TotalMinutes - totalUsed)
    });
})
.WithName("GetCustomerVipUsage");

app.MapGet("/api/customers/{customerId:guid}/history", async (HttpContext context,
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var exists = await database.Customers.AsNoTracking().AnyAsync(item => item.Id == customerId, cancellationToken);
    if (!exists)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var wallet = await database.WalletTransactions.AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .Select(item => new CustomerHistoryItemDto(item.Id, "wallet", item.Description, item.Amount, item.CreatedAt, item.ReferenceInvoiceId))
        .ToListAsync(cancellationToken);

    var benefits = await database.BenefitTransactions.AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .Select(item => new CustomerHistoryItemDto(item.Id, "benefit", item.Description, item.MoneyAmount, item.CreatedAt, item.ReferenceInvoiceId))
        .ToListAsync(cancellationToken);

    var invoices = await database.Invoices.AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .Select(item => new CustomerHistoryItemDto(item.Id, "invoice", item.Status.ToString(), item.TotalAmount, item.IssuedAt, item.SessionId))
        .ToListAsync(cancellationToken);

    var sessions = await database.Sessions.AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .Select(item => new CustomerHistoryItemDto(item.Id, "session", "جلسه", item.TotalAmount, item.StartAt, item.Id))
        .ToListAsync(cancellationToken);

    return Results.Ok(wallet.Concat(benefits).Concat(invoices).Concat(sessions)
        .OrderByDescending(item => item.CreatedAt)
        .Take(100)
        .ToList());
})
.WithName("GetCustomerHistory");

app.MapGet("/api/customers/{customerId:guid}/free-benefits", async (HttpContext context,
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var transactions = (await database.BenefitTransactions
        .AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .ToListAsync(cancellationToken))
        .OrderByDescending(item => item.CreatedAt)
        .Take(50)
        .Select(item => new FreeBenefitTransactionDto(
            item.Id,
            item.Type.ToString(),
            item.MoneyAmount,
            item.Minutes,
            item.Description,
            item.CreatedAt))
        .ToList();

    return Results.Ok(new FreeBenefitsSnapshotDto(customer.FreeMoney, customer.FreeTimeMinutes, transactions));
})
.WithName("GetCustomerFreeBenefits");

app.MapPost("/api/customers/{customerId:guid}/free-benefits", async (HttpContext context,
    Guid customerId,
    FreeBenefitRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers.FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
    if (customer is null)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    var moneyAmount = Math.Max(0m, request.MoneyAmount);
    var minutes = Math.Max(0, request.Minutes);
    if (moneyAmount <= 0 && minutes <= 0)
        return Results.BadRequest(new { code = "invalid_benefit", message = "مبلغ یا دقیقه رایگان معتبر وارد کنید." });

    var mode = request.Mode?.Trim().ToLowerInvariant();
    if (mode is not ("credit" or "debit"))
        return Results.BadRequest(new { code = "invalid_benefit_mode", message = "نوع عملیات اعتبار رایگان معتبر نیست." });

    var description = string.IsNullOrWhiteSpace(request.Description) ? "تنظیم اعتبار رایگان" : request.Description.Trim();

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    if (moneyAmount > 0)
    {
        if (mode == "debit" && customer.FreeMoney < moneyAmount)
            return Results.BadRequest(new { code = "insufficient_free_money", message = "اعتبار مالی رایگان کافی نیست." });
        customer.FreeMoney = mode == "credit" ? customer.FreeMoney + moneyAmount : customer.FreeMoney - moneyAmount;
    }

    if (minutes > 0)
    {
        if (mode == "debit" && customer.FreeTimeMinutes < minutes)
            return Results.BadRequest(new { code = "insufficient_free_time", message = "اعتبار زمانی رایگان کافی نیست." });
        customer.FreeTimeMinutes = mode == "credit" ? customer.FreeTimeMinutes + minutes : customer.FreeTimeMinutes - minutes;
    }

    database.BenefitTransactions.Add(new BenefitTransaction
    {
        CustomerId = customer.Id,
        Type = moneyAmount > 0
            ? (mode == "credit" ? BenefitTransactionType.FreeMoneyCredit : BenefitTransactionType.FreeMoneyDebit)
            : (mode == "credit" ? BenefitTransactionType.FreeTimeCredit : BenefitTransactionType.FreeTimeDebit),
        MoneyAmount = moneyAmount,
        Minutes = minutes,
        Description = description
    });

    database.AuditLogs.Add(new AuditLog
    {
        Action = "FreeBenefitChange",
        EntityName = "CustomerBenefit",
        EntityId = customer.Id.ToString(),
        Details = (mode == "credit" ? "اعطای اعتبار رایگان" : "کسر اعتبار رایگان")
            + " · " + (moneyAmount > 0 ? moneyAmount.ToString("0.##") + " تومان" : minutes + " دقیقه")
            + " · " + description,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new FreeBenefitsSnapshotDto(customer.FreeMoney, customer.FreeTimeMinutes, Array.Empty<FreeBenefitTransactionDto>()));
})
.WithName("ChangeCustomerFreeBenefits");

app.MapGet("/api/customers/{customerId:guid}/wallet-ledger", async (HttpContext context,
    Guid customerId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var customer = await database.Customers
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);

    if (customer is null)
    {
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });
    }

    var transactions = (await database.WalletTransactions
        .AsNoTracking()
        .Where(item => item.CustomerId == customerId)
        .ToListAsync(cancellationToken))
        .OrderByDescending(item => item.CreatedAt)
        .ThenByDescending(item => item.Id)
        .ToList();

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
            running,
            transaction.ReferenceTransactionId));

        running = transaction.Type == WalletTransactionType.Credit
            ? running - transaction.Amount
            : running + transaction.Amount;
    }

    result.Reverse();
    return Results.Ok(result);
})
.WithName("GetWalletLedger");

app.MapPost("/api/customers/{customerId:guid}/wallet-transactions", async (HttpContext context,
    Guid customerId,
    WalletTransactionRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

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
            AppUserId = auth.User!.Id
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
            customer.Balance,
            ledger.ReferenceTransactionId));
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
})
.WithName("PostWalletTransaction");


app.MapPost("/api/customers/{customerId:guid}/wallet-refunds/request", async (
    HttpContext context,
    Guid customerId,
    WalletRefundRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var reason = request.Reason?.Trim();
    if (request.Amount <= 0 || string.IsNullOrWhiteSpace(reason))
        return Results.BadRequest(new { code = "invalid_refund_request", message = "مبلغ و دلیل بازگشت وجه الزامی است." });

    var customerExists = await database.Customers.AsNoTracking().AnyAsync(item => item.Id == customerId, cancellationToken);
    if (!customerExists)
        return Results.NotFound(new { code = "customer_not_found", message = "مشتری پیدا نشد." });

    WalletTransaction? source = null;
    if (request.SourceTransactionId is Guid sourceId)
    {
        source = await database.WalletTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sourceId && item.CustomerId == customerId, cancellationToken);
        if (source is null)
            return Results.NotFound(new { code = "refund_source_not_found", message = "تراکنش مبدأ بازگشت وجه پیدا نشد." });
        if (source.Type != WalletTransactionType.Credit)
            return Results.BadRequest(new { code = "invalid_refund_source", message = "تراکنش انتخاب‌شده قابل بازگشت نیست." });
    }

    var target = EncodeWalletRefundApprovalTarget(customerId, request.Amount, request.SourceTransactionId);
    var pending = await database.ApprovalRequests.AnyAsync(item =>
        item.Action == "wallet.refund"
        && item.EntityName == "CustomerWallet"
        && item.EntityId == target
        && item.Status == ApprovalStatus.Pending, cancellationToken);
    if (pending)
        return Results.Conflict(new { code = "wallet_refund_approval_pending", message = "برای این بازگشت وجه یک درخواست در انتظار تصمیم وجود دارد." });

    var approval = new ApprovalRequest
    {
        Action = "wallet.refund",
        EntityName = "CustomerWallet",
        EntityId = target,
        Reason = reason,
        RequestedByUserId = auth.User!.Id,
        Status = ApprovalStatus.Pending
    };
    database.ApprovalRequests.Add(approval);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "WalletRefundApprovalRequest",
        EntityName = "CustomerWallet",
        EntityId = customerId.ToString(),
        AppUserId = auth.User.Id,
        Details = request.Amount.ToString("0.##") + " تومان · " + reason
            + (source is null ? "" : " · مرجع " + source.Id)
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { id = approval.Id, status = approval.Status.ToString(), action = approval.Action, entityId = customerId });
})
.WithName("RequestWalletRefundApproval");

app.MapPost("/api/customers/{customerId:guid}/wallet-refunds", async (
    Guid customerId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "customer.wallet", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    return Results.Conflict(new
    {
        code = "approval_required",
        message = "بازگشت وجه کیف پول باید ابتدا برای تأیید ثبت شود.",
        customerId,
        requiredEndpoint = $"/api/customers/{customerId}/wallet-refunds/request"
    });
})
.WithName("PostWalletRefund");


app.MapGet("/api/shifts/{shiftId:guid}/expenses", async (HttpContext context,
    Guid shiftId,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var exists = await database.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId, cancellationToken);
    if (!exists)
    {
        return Results.NotFound(new { code = "shift_not_found", message = "شیفت پیدا نشد." });
    }

    var rows = (await database.Expenses
        .AsNoTracking()
        .Where(item => item.ShiftId == shiftId)
        .ToListAsync(cancellationToken))
        .OrderByDescending(item => item.CreatedAt)
        .Select(item => new FinanceExpenseDto(
            item.Id,
            item.ShiftId,
            item.Category,
            item.Amount,
            item.Description,
            item.CreatedAt))
        .ToList();

    return Results.Ok(rows);
})
.WithName("GetShiftExpenses");

app.MapPost("/api/shifts/{shiftId:guid}/expenses", async (HttpContext context,
    Guid shiftId,
    FinanceExpenseRequestDto request,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

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
        AppUserId = auth.User!.Id
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

app.MapGet("/api/finance/summary", async (HttpContext context,
    DateTimeOffset? from,
    DateTimeOffset? to,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

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



app.MapGet("/api/finance/transactions", async (HttpContext context,
    DateTimeOffset? from,
    DateTimeOffset? to,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "finance.view", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var start = from ?? DateTimeOffset.UtcNow.Date;
    var end = to ?? DateTimeOffset.UtcNow;

    var invoices = await database.Invoices
        .AsNoTracking()
        .Include(item => item.Items)
        .Where(item => item.IssuedAt >= start && item.IssuedAt <= end)
        .OrderByDescending(item => item.IssuedAt)
        .Take(500)
        .Select(item => new
        {
            item.Id,
            item.IssuedAt,
            item.TotalAmount,
            item.Status,
            Description = item.Items
                .OrderBy(child => child.Id)
                .Select(child => child.Description)
                .FirstOrDefault() ?? "فاکتور"
        })
        .ToListAsync(cancellationToken);

    var invoiceIds = invoices.Select(item => item.Id).ToList();
    var payments = await database.InvoicePayments
        .AsNoTracking()
        .Where(item => invoiceIds.Contains(item.InvoiceId))
        .Select(item => new { item.InvoiceId, item.Method, item.Amount })
        .ToListAsync(cancellationToken);

    var result = invoices.Select(invoice =>
    {
        var parts = payments.Where(item => item.InvoiceId == invoice.Id).ToList();
        var methods = string.Join(" + ", parts.Select(item => item.Method).Distinct(StringComparer.OrdinalIgnoreCase));
        var method = methods switch
        {
            "" => "unknown",
            "cash" => "cash",
            "card" => "card",
            "wallet" => "wallet",
            "gift" => "gift",
            _ => "mixed"
        };

        return new FinanceTransactionDto(
            invoice.Id,
            invoice.IssuedAt,
            invoice.Description,
            invoice.TotalAmount,
            method,
            invoice.Status.ToString());
    }).ToList();

    return Results.Ok(result);
})
.WithName("GetFinanceTransactions");


app.MapGet("/api/shifts/current", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "shift.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var shift = await database.Shifts
        .AsNoTracking()
        .Include(item => item.AppUser)
        .OrderByDescending(item => item.OpenAt)
        .FirstOrDefaultAsync(item => item.CloseAt == null, cancellationToken);

    if (shift is null)
        return Results.Ok<ShiftSnapshotDto?>(null);

    return Results.Ok(await BuildShiftSnapshotAsync(database, shift, null, cancellationToken));
})
.WithName("GetCurrentShift");

app.MapGet("/api/shifts/history", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "shift.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var shifts = await database.Shifts
        .AsNoTracking()
        .Include(item => item.AppUser)
        .OrderByDescending(item => item.OpenAt)
        .Take(30)
        .ToListAsync(cancellationToken);

    var result = new List<ShiftSnapshotDto>(shifts.Count);
    foreach (var shift in shifts)
        result.Add(await BuildShiftSnapshotAsync(database, shift, null, cancellationToken));

    return Results.Ok(result);
})
.WithName("GetShiftHistory");

app.MapPost("/api/shifts/start", async (
    StartShiftRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "shift.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;
    request = request with { AppUserId = auth.User!.Id };

    if (request.CashOpening < 0)
        return Results.BadRequest(new { code = "invalid_cash_opening", message = "مبلغ شروع صندوق نمی‌تواند منفی باشد." });

    var openExists = await database.Shifts.AnyAsync(item => item.CloseAt == null, cancellationToken);
    if (openExists)
        return Results.Conflict(new { code = "shift_already_open", message = "یک شیفت دیگر هنوز باز است." });

    var user = auth.User!;


    var shift = new Shift
    {
        AppUserId = user.Id,
        OpenAt = DateTimeOffset.UtcNow,
        CashOpening = request.CashOpening,
        Notes = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
    };

    database.Shifts.Add(shift);
    database.AuditLogs.Add(new AuditLog
    {
        Action = "ShiftStart",
        EntityName = "Shift",
        EntityId = shift.Id.ToString(),
        Details = "شروع شیفت · " + user.FullName + " · صندوق اولیه " + request.CashOpening.ToString("0.##") + " تومان",
        AppUserId = user.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(await BuildShiftSnapshotAsync(database, shift, null, cancellationToken));
})
.WithName("StartShift");

app.MapPost("/api/shifts/{shiftId:guid}/close", async (
    Guid shiftId,
    CloseShiftRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "shift.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (request.CashClosing < 0 || request.ExternalCash < 0)
        return Results.BadRequest(new { code = "invalid_cash_value", message = "مبالغ صندوق نمی‌توانند منفی باشند." });

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var shift = await database.Shifts
        .Include(item => item.AppUser)
        .FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken);

    if (shift is null)
        return Results.NotFound(new { code = "shift_not_found", message = "شیفت پیدا نشد." });

    if (shift.CloseAt is not null)
        return Results.Conflict(new { code = "shift_closed", message = "این شیفت قبلاً بسته شده است." });

    var now = DateTimeOffset.UtcNow;
    var cashSales = await database.InvoicePayments
        .Where(item => item.Method == "cash"
            && item.Invoice.Status == InvoiceStatus.Paid
            && item.Invoice.PaidAt != null
            && item.Invoice.PaidAt >= shift.OpenAt
            && item.Invoice.PaidAt <= now)
        .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    var expenseTotal = await database.Expenses
        .Where(item => item.ShiftId == shift.Id)
        .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    var expectedCash = shift.CashOpening + cashSales + request.ExternalCash - expenseTotal;
    var difference = request.CashClosing - expectedCash;

    shift.CashClosing = request.CashClosing;
    shift.CloseAt = now;
    var noteParts = new List<string>();
    if (request.ExternalCash > 0)
        noteParts.Add("تطبیق نقدی خارج از سیستم " + request.ExternalCash.ToString("0.##") + " تومان");
    if (!string.IsNullOrWhiteSpace(request.Note))
        noteParts.Add(request.Note.Trim());
    shift.Notes = string.Join(" · ", noteParts);

    database.AuditLogs.Add(new AuditLog
    {
        Action = "ShiftClose",
        EntityName = "Shift",
        EntityId = shift.Id.ToString(),
        Details = "بستن شیفت · فروش نقدی " + cashSales.ToString("0.##") + " · هزینه " + expenseTotal.ToString("0.##") + " · اختلاف " + difference.ToString("0.##"),
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new ShiftSnapshotDto(
        shift.Id,
        shift.AppUserId,
        shift.AppUser?.FullName ?? "کاربر",
        shift.OpenAt,
        shift.CloseAt,
        shift.CashOpening,
        request.CashClosing,
        cashSales,
        expenseTotal,
        request.ExternalCash,
        expectedCash,
        difference,
        shift.Notes));
})
.WithName("CloseShift");

app.MapPost("/api/sessions", async (
    StartSessionRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.start", cancellationToken);
    if (auth.Error is not null) return auth.Error;
    request = request with { AppUserId = auth.User!.Id };

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
        AppUserId = auth.User!.Id,
        StartAt = DateTimeOffset.UtcNow,
        State = SessionState.Active,
        TotalAmount = 0m,
        HourlyRateOverride = request.HourlyRateOverride > 0 ? request.HourlyRateOverride : null,
        Persons = Math.Max(1, request.Persons ?? 1)
    };

    database.Sessions.Add(session);
    station.State = StationState.Occupied;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionStart",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "شروع جلسه · ایستگاه " + station.Name,
        AppUserId = auth.User!.Id
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

app.MapMethods("/api/sessions/{sessionId:guid}/details", new[] { "PATCH" }, async (
    Guid sessionId,
    SessionDetailsRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var session = await database.Sessions
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

    if (session is null)
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });

    if (session.State != SessionState.Active)
        return Results.Conflict(new { code = "session_not_active", message = "این جلسه فعال نیست." });

    if (request.HourlyRate is <= 0)
        return Results.BadRequest(new { code = "invalid_hourly_rate", message = "نرخ جلسه باید بیشتر از صفر باشد." });

    var persons = request.Persons ?? session.Persons;
    var isPc = session.Station.Type.Equals("PC", StringComparison.OrdinalIgnoreCase)
        || session.Station.Type.Contains("رایانه", StringComparison.OrdinalIgnoreCase);
    if (isPc)
        persons = 1;

    if (persons < 1 || persons > 4)
        return Results.BadRequest(new { code = "invalid_persons", message = "تعداد نفرات باید بین ۱ تا ۴ باشد." });

    if (request.HourlyRate is not null)
        session.HourlyRateOverride = request.HourlyRate.Value;

    session.Persons = persons;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionDetailsChange",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "نرخ " + (session.HourlyRateOverride?.ToString("0.##") ?? "تعرفه") + " · نفرات " + persons,
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { sessionId = session.Id, hourlyRate = session.HourlyRateOverride, persons = session.Persons });
})
.WithName("UpdateSessionDetails");

app.MapPost("/api/sessions/{sessionId:guid}/pause", async (
    Guid sessionId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var session = await database.Sessions
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

    if (session is null)
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });

    if (session.State != SessionState.Active)
        return Results.Conflict(new { code = "session_not_active", message = "این جلسه فعال نیست." });

    if (session.PausedAt.HasValue)
        return Results.Conflict(new { code = "session_already_paused", message = "جلسه از قبل متوقف است." });

    session.PausedAt = DateTimeOffset.UtcNow;
    session.Station.State = StationState.Occupied;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionPause",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "توقف جلسه · ایستگاه " + session.Station.Name,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { sessionId = session.Id, pausedAt = session.PausedAt });
})
.WithName("PauseSession");

app.MapPost("/api/sessions/{sessionId:guid}/resume", async (
    Guid sessionId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    var session = await database.Sessions
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

    if (session is null)
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });

    if (session.State != SessionState.Active)
        return Results.Conflict(new { code = "session_not_active", message = "این جلسه فعال نیست." });

    if (!session.PausedAt.HasValue)
        return Results.Conflict(new { code = "session_not_paused", message = "جلسه متوقف نیست." });

    var now = DateTimeOffset.UtcNow;
    session.PausedMinutes += Math.Max(0, (int)Math.Ceiling((now - session.PausedAt.Value).TotalMinutes));
    session.PausedAt = null;
    session.Station.State = StationState.Occupied;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionResume",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "ادامه جلسه · ایستگاه " + session.Station.Name,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { sessionId = session.Id, pausedMinutes = session.PausedMinutes });
})
.WithName("ResumeSession");

app.MapPost("/api/sessions/{sessionId:guid}/time-adjustment", async (
    Guid sessionId,
    SessionTimeAdjustmentRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (request.Minutes == 0 || Math.Abs(request.Minutes) > 1440)
        return Results.BadRequest(new { code = "invalid_time_adjustment", message = "تغییر زمان باید بین ۱ تا ۱۴۴۰ دقیقه باشد." });

    var session = await database.Sessions
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

    if (session is null)
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });

    if (session.State != SessionState.Active)
        return Results.Conflict(new { code = "session_not_active", message = "این جلسه فعال نیست." });

    var currentMinutes = SessionTiming.GetBillableMinutes(session, DateTimeOffset.UtcNow);
    if (request.Minutes < 0 && Math.Abs(request.Minutes) >= currentMinutes)
        return Results.BadRequest(new { code = "invalid_time_reduction", message = "کاهش زمان نمی‌تواند به صفر یا کمتر برسد." });

    session.TimeAdjustmentMinutes += request.Minutes;

    database.AuditLogs.Add(new AuditLog
    {
        Action = request.Minutes > 0 ? "SessionExtend" : "SessionReduce",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = (request.Minutes > 0 ? "تمدید " : "کاهش ") + Math.Abs(request.Minutes) + " دقیقه · " + session.Station.Name,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        sessionId = session.Id,
        timeAdjustmentMinutes = session.TimeAdjustmentMinutes,
        billableMinutes = SessionTiming.GetBillableMinutes(session, DateTimeOffset.UtcNow)
    });
})
.WithName("AdjustSessionTime");

app.MapGet("/api/sessions/active", async (HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequireAnyPermissionAsync(context, database, cancellationToken, "session.start", "session.manage", "session.settle", "buffet.sell");
    if (auth.Error is not null) return auth.Error;

    var sessions = await database.Sessions
        .AsNoTracking()
        .Where(session => session.State == SessionState.Active)
        .Select(session => new
        {
            id = session.Id,
            stationName = session.Station.Name,
            customerId = session.CustomerId,
            customerName = session.Customer.FullName,
            customerCode = session.Customer.Code,
            username = session.Customer.Username,
            startedAt = session.StartAt
        })
        .ToListAsync(cancellationToken);

    sessions = sessions
        .OrderBy(session => session.startedAt)
        .ToList();

    var sessionIds = sessions.Select(item => item.id).ToList();
    var buffetRows = sessionIds.Count == 0
        ? new List<(Guid SessionId, decimal Amount)>()
        : (await database.InvoiceItems
            .AsNoTracking()
            .Where(item => item.Invoice.SessionId.HasValue
                && sessionIds.Contains(item.Invoice.SessionId.Value)
                && item.Invoice.Status == InvoiceStatus.Draft
                && item.ProductId.HasValue)
            .Select(item => new { SessionId = item.Invoice.SessionId!.Value, item.Amount })
            .ToListAsync(cancellationToken))
            .Select(item => (item.SessionId, item.Amount))
            .ToList();

    var buffetTotals = buffetRows
        .GroupBy(item => item.SessionId)
        .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

    return Results.Ok(sessions.Select(session => new
    {
        session.id,
        session.stationName,
        session.customerId,
        session.customerName,
        session.customerCode,
        session.username,
        session.startedAt,
        buffetTotal = buffetTotals.TryGetValue(session.id, out var total) ? total : 0m
    }));
})
.WithName("GetActiveSessions");

app.MapPost("/api/sessions/{sessionId:guid}/transfer", async (
    Guid sessionId,
    SessionTransferRequest request,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.manage", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    if (request.TargetStationId == Guid.Empty)
        return Results.BadRequest(new { code = "invalid_target_station", message = "ایستگاه مقصد معتبر نیست." });

    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var session = await database.Sessions
        .Include(item => item.Station)
        .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

    if (session is null)
        return Results.NotFound(new { code = "session_not_found", message = "جلسه پیدا نشد." });

    if (session.State != SessionState.Active)
        return Results.Conflict(new { code = "session_not_active", message = "جلسه فعال نیست." });

    var target = await database.Stations.FirstOrDefaultAsync(item => item.Id == request.TargetStationId, cancellationToken);
    if (target is null)
        return Results.NotFound(new { code = "station_not_found", message = "ایستگاه مقصد پیدا نشد." });

    if (target.State != StationState.Available)
        return Results.Conflict(new { code = "station_not_available", message = "ایستگاه مقصد آزاد نیست." });

    var source = session.Station;
    source.State = StationState.Available;
    target.State = StationState.Occupied;
    session.StationId = target.Id;

    database.AuditLogs.Add(new AuditLog
    {
        Action = "SessionTransfer",
        EntityName = "Session",
        EntityId = session.Id.ToString(),
        Details = "انتقال از " + source.Name + " به " + target.Name,
        AppUserId = auth.User!.Id
    });

    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Results.Ok(new SessionTransferResultDto(session.Id, target.Id));
})
.WithName("TransferSession");

app.MapPost("/api/sessions/{sessionId:guid}/settle", async (
    Guid sessionId,
    SessionSettlementRequest request,
    SessionSettlementService settlement,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "session.settle", cancellationToken);
    if (auth.Error is not null) return auth.Error;
    request = request with { AppUserId = auth.User!.Id };

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

app.MapPost("/api/invoices/{invoiceId:guid}/reverse", async (
    Guid invoiceId,
    HttpContext context,
    GameNetDbContext database,
    CancellationToken cancellationToken) =>
{
    var auth = await AuthorizationService.RequirePermissionAsync(context, database, "approval.decide", cancellationToken);
    if (auth.Error is not null) return auth.Error;

    return Results.Conflict(new
    {
        code = "approval_required",
        message = "برگشت فاکتور باید ابتدا از مسیر درخواست تأیید ثبت شود و سپس توسط کاربر مجاز اجرا شود.",
        invoiceId,
        requiredEndpoint = $"/api/invoices/{invoiceId}/reverse/request"
    });
})
.WithName("ReverseInvoice");

app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<AgentHub>("/hubs/agent");

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();



static string EncodeWalletRefundApprovalTarget(Guid customerId, decimal amount, Guid? sourceTransactionId)
    => string.Join("|",
        customerId.ToString("D"),
        amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        sourceTransactionId?.ToString("D") ?? "");

static bool TryDecodeWalletRefundApprovalTarget(string? value, out WalletRefundApprovalTarget target)
{
    target = default!;
    var parts = value?.Split('|');
    if (parts is null || parts.Length < 2 || parts.Length > 3)
        return false;

    if (!Guid.TryParse(parts[0], out var customerId)
        || !decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)
        || amount <= 0)
        return false;

    Guid? sourceTransactionId = null;
    if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]))
    {
        if (!Guid.TryParse(parts[2], out var sourceId))
            return false;
        sourceTransactionId = sourceId;
    }

    target = new WalletRefundApprovalTarget(customerId, amount, sourceTransactionId);
    return true;
}

static string HashPassword(string password)
{
    var salt = RandomNumberGenerator.GetBytes(16);
    const int iterations = 120_000;
    var hash = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), salt, iterations, HashAlgorithmName.SHA256, 32);
    return "PBKDF2-SHA256$" + iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
}

static bool VerifyPassword(string password, string stored)
{
    var parts = stored.Split('$');
    if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" || !int.TryParse(parts[1], out var iterations))
        return false;

    try
    {
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
    catch
    {
        return false;
    }
}

static string NormalizeVipTier(string? tier)
{
    var value = tier?.Trim().ToLowerInvariant();
    return value is "silver" or "gold" or "bronze" or "custom" ? value : "none";
}

static CustomerDto ToCustomerDto(Customer customer) => new(
    customer.Id,
    customer.Code,
    customer.Username,
    customer.FullName,
    customer.Alias,
    customer.NationalId,
    customer.Phone,
    customer.Email,
    customer.VipTier,
    customer.Balance,
    customer.FreeMoney,
    customer.FreeTimeMinutes,
    customer.ConcurrentLoginLimit,
    customer.Notes);

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

static async Task<ShiftSnapshotDto> BuildShiftSnapshotAsync(
    GameNetDbContext database,
    Shift shift,
    decimal? countedCashOverride,
    CancellationToken cancellationToken)
{
    var end = shift.CloseAt ?? DateTimeOffset.UtcNow;

    var cashSales = await database.InvoicePayments
        .Where(item => item.Method == "cash"
            && item.Invoice.Status == InvoiceStatus.Paid
            && item.Invoice.PaidAt != null
            && item.Invoice.PaidAt >= shift.OpenAt
            && item.Invoice.PaidAt <= end)
        .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    var expenseTotal = await database.Expenses
        .Where(item => item.ShiftId == shift.Id)
        .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

    var externalCash = 0m;
    var expected = shift.CashOpening + cashSales - expenseTotal;
    var counted = countedCashOverride ?? shift.CashClosing;
    var difference = counted.HasValue ? counted.Value - expected : 0m;

    return new ShiftSnapshotDto(
        shift.Id,
        shift.AppUserId,
        shift.AppUser?.FullName ?? "کاربر",
        shift.OpenAt,
        shift.CloseAt,
        shift.CashOpening,
        counted,
        cashSales,
        expenseTotal,
        externalCash,
        expected,
        difference,
        shift.Notes);
}

static AppUserDto ToAppUserDto(AppUser user)
    => new(
        user.Id,
        user.FullName,
        user.UserName,
        user.Email,
        user.Role,
        user.IsActive,
        user.LastLoginAt,
        user.Permissions.Select(item => item.Permission.Name).OrderBy(name => name).ToArray());

public sealed record LoginRequest(string UserName, string Password);
public sealed record AppUserDto(Guid Id, string FullName, string UserName, string Email, string Role, bool IsActive, DateTimeOffset? LastLoginAt, IReadOnlyList<string> Permissions);
public sealed record AppUserWriteRequest(string FullName, string UserName, string Email, string Password, string Role, bool IsActive = true);
public sealed record PermissionAssignmentRequest(IReadOnlyList<string> PermissionNames);
public sealed record ApprovalCreateRequest(string Action, string EntityName, string? EntityId, string Reason);
public sealed record ApprovalOperationRequest(string Reason);
public sealed record PayrollProfileRequest(
    string? Phone,
    string PayType,
    decimal HourlyRate,
    decimal MonthlySalary,
    decimal OvertimeRate,
    DateTimeOffset? EmploymentStartDate,
    string? WorkSchedule,
    string? Notes,
    bool IsActive = true);

public sealed record PayrollEntryRequest(
    string Kind,
    decimal Amount,
    string Reason,
    string? PaymentMethod = null,
    string? ReceiptNumber = null,
    decimal? EmployeePayableDelta = null,
    decimal? OwnerReceivableDelta = null);

public sealed record ApprovalDecisionRequest(string? Note);
public sealed record WalletRefundRequestDto(decimal Amount, string? Reason, Guid? SourceTransactionId);
public sealed record WalletRefundApprovalTarget(Guid CustomerId, decimal Amount, Guid? SourceTransactionId);

public sealed record StartShiftRequest(
    string? OperatorName,
    Guid? AppUserId,
    decimal CashOpening,
    string? Note);

public sealed record CloseShiftRequest(
    decimal CashClosing,
    decimal ExternalCash,
    string? Note);

public sealed record StartSessionRequest(Guid CustomerId, Guid StationId, Guid? TariffId, Guid? AppUserId, decimal? HourlyRateOverride, int? Persons);
public sealed record SessionDetailsRequest(decimal? HourlyRate, int? Persons);
public sealed record SessionTimeAdjustmentRequest(int Minutes);
public sealed record SessionTransferRequest(Guid TargetStationId);
public sealed record SessionTransferResultDto(Guid SessionId, Guid StationId);
public sealed record StartSessionResultDto(Guid SessionId, Guid StationId, Guid CustomerId, DateTimeOffset StartAt);

public sealed record FinanceExpenseRequestDto(decimal Amount, string Category, string? Description, Guid? AppUserId);
public sealed record FinanceExpenseDto(Guid Id, Guid ShiftId, string Category, decimal Amount, string? Description, DateTimeOffset CreatedAt);
public sealed record FinanceSummaryDto(DateTimeOffset From, DateTimeOffset To, decimal Revenue, decimal Expense, decimal OperatingProfit);
public sealed record FinanceTransactionDto(Guid Id, DateTimeOffset ClosedAt, string Description, decimal Amount, string Method, string Status);

public sealed record CustomerDebtRequest(decimal Amount, string? Description, Guid? AppUserId);
public sealed record CustomerHistoryItemDto(Guid Id, string Type, string Description, decimal Amount, DateTimeOffset CreatedAt, Guid? ReferenceId);
public sealed record CreateBuffetProductRequest(string Name, string Category, decimal UnitPrice, decimal CostPrice, int InitialStock, int MinimumStock = 0, string? Unit = null, Guid? AppUserId = null);
public sealed record UpdateBuffetProductRequest(string Name, string Category, decimal UnitPrice, decimal CostPrice, int MinimumStock = 0, string? Unit = null, bool IsActive = true, Guid? AppUserId = null);
public sealed record StockAdjustmentRequest(int Quantity, string Direction, string? Notes, Guid? AppUserId, string Kind = "Adjustment", decimal? UnitCost = null);
public sealed record BuffetSaleItem(Guid ProductId, int Quantity);
public sealed record BuffetSaleRequest(IReadOnlyList<BuffetSaleItem> Items, string Target, Guid? AppUserId, Guid? SessionId = null);
public sealed record FreeBenefitRequestDto(decimal MoneyAmount, int Minutes, string Mode, string? Description);
public sealed record FreeBenefitTransactionDto(Guid Id, string Type, decimal MoneyAmount, int Minutes, string Description, DateTimeOffset CreatedAt);
public sealed record FreeBenefitsSnapshotDto(decimal FreeMoney, int FreeTimeMinutes, IReadOnlyList<FreeBenefitTransactionDto> Transactions);
public sealed record CustomerLoginRequest(string ClientKey);
public sealed record CustomerLoginReleaseRequest(string ClientKey);
public sealed record ConcurrentLoginResultDto(bool Acquired, Guid LoginId, int ActiveCount, int Limit);

public sealed record ShiftSnapshotDto(
    Guid Id,
    Guid AppUserId,
    string Operator,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal CashOpening,
    decimal? CashClosing,
    decimal CashSales,
    decimal Expenses,
    decimal ExternalCash,
    decimal ExpectedCash,
    decimal Difference,
    string? Note);

public partial class Program { }

public partial class Program { }
