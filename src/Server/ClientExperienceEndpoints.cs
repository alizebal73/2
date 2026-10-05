using System.Text.Json;
using GameNetManager.Server.Data;
using GameNetManager.Server.Hubs;
using GameNetManager.Shared.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server;

public static class ClientExperienceEndpoints
{
    public static void MapClientExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/client/catalog", async (
            HttpContext context,
            GameNetDbContext database,
            CancellationToken cancellationToken) =>
        {
            var device = await ResolveDeviceAsync(context, database, cancellationToken);
            if (device is null)
            {
                return Results.NotFound(new
                {
                    code = "client_identity_not_found",
                    message = "Agent این رایانه پیدا نشد."
                });
            }

            var games = await database.Games
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new
                {
                    id = item.Id,
                    name = item.Name,
                    category = item.Genre,
                    version = item.Version,
                    genre = item.Genre,
                    status = item.Status,
                    cover = item.Cover,
                    trailer = item.Trailer,
                    connectionType = item.ConnectionType,
                    icon = "🎮",
                    description = string.IsNullOrWhiteSpace(item.Genre)
                        ? (item.ConnectionType ?? string.Empty)
                        : string.IsNullOrWhiteSpace(item.ConnectionType)
                            ? item.Genre
                            : item.Genre + " · " + item.ConnectionType
                })
                .ToListAsync(cancellationToken);

            var activePoolRows = await database.AccountPoolEntries
                .AsNoTracking()
                .Where(item => item.IsActive && item.Status == AccountPoolStatus.Free)
                .Select(item => item.AllowedGameIdsCsv)
                .ToListAsync(cancellationToken);

            var poolGameIds = activePoolRows
                .SelectMany(csv => (csv ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => Guid.TryParse(value, out _))
                .Select(Guid.Parse)
                .ToHashSet();

            var clientGames = games.Select(game => new
            {
                game.id,
                game.name,
                category = game.category,
                game.version,
                game.genre,
                game.status,
                game.cover,
                game.trailer,
                game.connectionType,
                icon = game.icon,
                description = game.description,
                hasPoolAccount = poolGameIds.Contains(game.id)
            });

            var buffet = await database.Products
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
                    unit = item.Unit,
                    available = item.StockQuantity > 0,
                    icon = "🛒"
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                deviceId = device.DeviceId,
                stationId = device.StationId,
                stationName = device.Station?.Name,
                games = clientGames,
                buffet
            });
        })
        .WithName("GetClientCatalog");

        app.MapPost("/api/client/request", async (
            ClientRequestRequest request,
            HttpContext context,
            GameNetDbContext database,
            NotificationQueueService notifications,
            CancellationToken cancellationToken) =>
        {
            var resolved = await ResolveCustomerContextAsync(
                context,
                database,
                request.CustomerId,
                request.LoginId,
                cancellationToken);

            if (resolved.Error is not null)
                return resolved.Error;

            var device = resolved.Device!;
            var customer = resolved.Customer!;
            var kind = request.Kind?.Trim().ToLowerInvariant();

            if (kind is "message" or "charge" or "move" or "unlock")
            {
                var detail = kind switch
                {
                    "message" => string.IsNullOrWhiteSpace(request.Message)
                        ? "مشتری برای اپراتور پیام فرستاد."
                        : $"پیام مشتری: {request.Message.Trim()}",
                    "charge" => "مشتری درخواست شارژ حساب داد.",
                    "move" => "مشتری درخواست جابه‌جایی به سیستم دیگر داد.",
                    "unlock" => "مشتری درخواست بازگشایی دستگاه داد.",
                    _ => "درخواست مشتری ثبت شد."
                };

                if (kind == "message" && (request.Message?.Trim().Length ?? 0) > 500)
                    return Results.BadRequest(new
                    {
                        code = "client_message_too_long",
                        message = "پیام مشتری بیش از حد مجاز است."
                    });

                database.AuditLogs.Add(new AuditLog
                {
                    Action = "ClientRequestCreated",
                    EntityName = "Customer",
                    EntityId = customer.Id.ToString(),
                    Details = $"درخواست مشتری · نوع={kind} · Agent={device.DeviceId}"
                });
                await database.SaveChangesAsync(cancellationToken);

                await notifications.PublishToPermissionAsync(
                    "client.control",
                    new NotificationEvent(
                        "client.request",
                        "درخواست مشتری",
                        $"{customer.FullName} · {device.Station?.Name ?? device.DeviceId} · {detail}",
                        NotificationLevel.Info,
                        "Customer",
                        customer.Id.ToString()),
                    cancellationToken);

                return Results.Ok(new
                {
                    created = true,
                    message = "درخواست برای اپراتور ارسال شد."
                });
            }

            if (kind == "buffet")
            {
                if (!request.ProductId.HasValue)
                    return Results.BadRequest(new
                    {
                        code = "product_required",
                        message = "محصول بوفه مشخص نشده است."
                    });

                var quantity = Math.Clamp(request.Quantity ?? 1, 1, 20);
                var product = await database.Products
                    .FirstOrDefaultAsync(item => item.Id == request.ProductId.Value && item.IsActive, cancellationToken);
                if (product is null)
                    return Results.NotFound(new
                    {
                        code = "product_not_found",
                        message = "محصول بوفه پیدا نشد."
                    });

                if (product.StockQuantity < quantity)
                    return Results.Conflict(new
                    {
                        code = "product_out_of_stock",
                        message = "موجودی این محصول برای درخواست شما کافی نیست."
                    });

                database.AuditLogs.Add(new AuditLog
                {
                    Action = "ClientBuffetRequestCreated",
                    EntityName = "Product",
                    EntityId = product.Id.ToString(),
                    Details = $"درخواست بوفه از مشتری {customer.FullName} · مقدار {quantity} · Agent={device.DeviceId}"
                });
                await database.SaveChangesAsync(cancellationToken);

                await notifications.PublishToPermissionAsync(
                    "buffet.sell",
                    new NotificationEvent(
                        "client.buffet-request",
                        "درخواست بوفه",
                        $"{customer.FullName} از {device.Station?.Name ?? device.DeviceId} · {product.Name} × {quantity}",
                        NotificationLevel.Info,
                        "Product",
                        product.Id.ToString()),
                    cancellationToken);

                return Results.Ok(new
                {
                    created = true,
                    message = "درخواست بوفه برای اپراتور ارسال شد."
                });
            }

            return Results.BadRequest(new
            {
                code = "unsupported_client_request",
                message = "نوع درخواست مشتری پشتیبانی نمی‌شود."
            });
        })
        .WithName("CreateClientRequest");

        app.MapPost("/api/client/command/lock", async (
            ClientCustomerCommandRequest request,
            HttpContext context,
            GameNetDbContext database,
            IHubContext<AgentHub> agentHub,
            CancellationToken cancellationToken) =>
            await SendCustomerAgentCommandAsync(
                AgentCommandTypes.Lock,
                request,
                "قفل دستگاه",
                context,
                database,
                agentHub,
                cancellationToken))
        .WithName("ClientLock");

        app.MapPost("/api/client/command/logout-lock", async (
            ClientCustomerCommandRequest request,
            HttpContext context,
            GameNetDbContext database,
            IHubContext<AgentHub> agentHub,
            CancellationToken cancellationToken) =>
            await SendCustomerAgentCommandAsync(
                AgentCommandTypes.LogoutLock,
                request,
                "خروج و قفل دستگاه",
                context,
                database,
                agentHub,
                cancellationToken))
        .WithName("ClientLogoutAndLock");

        app.MapPost("/api/client/game/launch", async (
            ClientGameCommandRequest request,
            HttpContext context,
            GameNetDbContext database,
            IHubContext<AgentHub> agentHub,
            CancellationToken cancellationToken) =>
        {
            var resolved = await ResolveCustomerContextAsync(
                context,
                database,
                request.CustomerId,
                request.LoginId,
                cancellationToken);

            if (resolved.Error is not null)
                return resolved.Error;

            var device = resolved.Device!;
            if (!device.StationId.HasValue || string.IsNullOrWhiteSpace(device.ConnectionId))
                return Results.Conflict(new
                {
                    code = "agent_not_ready",
                    message = "Agent این رایانه آماده دریافت فرمان نیست."
                });

            var session = await database.Sessions
                .FirstOrDefaultAsync(item =>
                    item.Id == request.SessionId
                    && item.State == SessionState.Active
                    && item.CustomerId == request.CustomerId
                    && item.CustomerLoginId == request.LoginId
                    && item.StationId == device.StationId.Value
                    && item.AgentDeviceId == device.Id,
                    cancellationToken);

            if (session is null)
                return Results.Conflict(new
                {
                    code = "active_session_not_found",
                    message = "جلسه فعال این مشتری روی همین رایانه پیدا نشد."
                });

            var game = await database.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.GameId && item.IsActive, cancellationToken);
            if (game is null)
                return Results.NotFound(new
                {
                    code = "game_not_found",
                    message = "بازی فعال پیدا نشد."
                });

            if (session.GameId.HasValue && session.GameId.Value != game.Id)
                return Results.Conflict(new
                {
                    code = "session_game_locked",
                    message = "برای این Session بازی دیگری ثبت شده است."
                });

            if (!session.GameId.HasValue)
            {
                session.GameId = game.Id;
                database.AuditLogs.Add(new AuditLog
                {
                    Action = "ClientGameSelected",
                    EntityName = "Session",
                    EntityId = session.Id.ToString(),
                    Details = $"بازی {game.Name} توسط Customer Client انتخاب شد · Agent={device.DeviceId}"
                });
                await database.SaveChangesAsync(cancellationToken);
            }

            var payload = JsonSerializer.Serialize(new AgentGameLaunchCommandPayload(
                game.Id,
                session.Id,
                game.Path,
                game.Executable,
                game.LaunchArgs));

            return await DispatchCustomerAgentCommandAsync(
                AgentCommandTypes.LaunchGame,
                payload,
                request.CustomerId,
                device,
                "اجرای بازی",
                database,
                agentHub,
                cancellationToken);
        })
        .WithName("ClientLaunchGame");

        app.MapPost("/api/client/game/stop", async (
            ClientGameCommandRequest request,
            HttpContext context,
            GameNetDbContext database,
            IHubContext<AgentHub> agentHub,
            CancellationToken cancellationToken) =>
        {
            var resolved = await ResolveCustomerContextAsync(
                context,
                database,
                request.CustomerId,
                request.LoginId,
                cancellationToken);

            if (resolved.Error is not null)
                return resolved.Error;

            var device = resolved.Device!;
            if (!device.StationId.HasValue || string.IsNullOrWhiteSpace(device.ConnectionId))
                return Results.Conflict(new
                {
                    code = "agent_not_ready",
                    message = "Agent این رایانه آماده دریافت فرمان نیست."
                });

            var session = await database.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.Id == request.SessionId
                    && item.State == SessionState.Active
                    && item.CustomerId == request.CustomerId
                    && item.CustomerLoginId == request.LoginId
                    && item.StationId == device.StationId.Value
                    && item.AgentDeviceId == device.Id
                    && item.GameId == request.GameId,
                    cancellationToken);

            if (session is null)
                return Results.Conflict(new
                {
                    code = "active_session_game_not_found",
                    message = "بازی فعال این Session روی همین رایانه پیدا نشد."
                });

            return await DispatchCustomerAgentCommandAsync(
                AgentCommandTypes.StopGame,
                JsonSerializer.Serialize(new AgentGameLaunchCommandPayload(
                    request.GameId,
                    request.SessionId,
                    string.Empty,
                    string.Empty,
                    string.Empty)),
                request.CustomerId,
                device,
                "توقف بازی",
                database,
                agentHub,
                cancellationToken);
        })
        .WithName("ClientStopGame");
    }

    private static async Task<IResult> SendCustomerAgentCommandAsync(
        string commandType,
        ClientCustomerCommandRequest request,
        string auditTitle,
        HttpContext context,
        GameNetDbContext database,
        IHubContext<AgentHub> agentHub,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveCustomerContextAsync(
            context,
            database,
            request.CustomerId,
            request.LoginId,
            cancellationToken);

        if (resolved.Error is not null)
            return resolved.Error;

        return await DispatchCustomerAgentCommandAsync(
            commandType,
            null,
            request.CustomerId,
            resolved.Device!,
            auditTitle,
            database,
            agentHub,
            cancellationToken);
    }

    private static async Task<IResult> DispatchCustomerAgentCommandAsync(
        string commandType,
        string? payloadJson,
        Guid customerId,
        AgentDevice device,
        string auditTitle,
        GameNetDbContext database,
        IHubContext<AgentHub> agentHub,
        CancellationToken cancellationToken)
    {
        if (!device.IsActive || !device.IsOnline || string.IsNullOrWhiteSpace(device.ConnectionId))
            return Results.Conflict(new
            {
                code = "agent_offline",
                message = "Agent این رایانه آفلاین است."
            });

        var now = DateTimeOffset.UtcNow;
        var command = new AgentCommand
        {
            AgentDeviceId = device.Id,
            RequestedByAppUserId = null,
            CommandType = commandType,
            PayloadJson = payloadJson,
            Status = "Sent",
            RequestedAt = now,
            SentAt = now,
            AgentConnectionId = device.ConnectionId
        };

        database.AgentCommands.Add(command);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "ClientAgentCommandRequested",
            EntityName = "AgentCommand",
            EntityId = command.Id.ToString(),
            Details = $"{auditTitle} · Customer={customerId} · Agent={device.DeviceId}"
        });
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            await agentHub.Clients.Group(AgentHub.DeviceGroup(device.Id)).SendAsync(
                "AgentCommand",
                new AgentCommandEnvelope(
                    command.Id,
                    command.CommandType,
                    command.PayloadJson,
                    command.RequestedAt),
                cancellationToken);
        }
        catch
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

        return Results.Ok(new
        {
            commandId = command.Id,
            status = command.Status,
            message = $"{auditTitle} برای Agent ارسال شد."
        });
    }

    private static async Task<(AgentDevice? Device, Customer? Customer, IResult? Error)> ResolveCustomerContextAsync(
        HttpContext context,
        GameNetDbContext database,
        Guid customerId,
        Guid loginId,
        CancellationToken cancellationToken)
    {
        var device = await ResolveDeviceAsync(context, database, cancellationToken);
        if (device is null)
            return (
                null,
                null,
                Results.NotFound(new
                {
                    code = "client_identity_not_found",
                    message = "Agent این رایانه پیدا نشد."
                }));

        var customer = await database.Customers
            .FirstOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        if (customer is null)
            return (
                device,
                null,
                Results.NotFound(new
                {
                    code = "customer_not_found",
                    message = "مشتری پیدا نشد."
                }));

        var login = await database.CustomerLogins
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Id == loginId
                && item.CustomerId == customerId
                && item.ClientKey == device.DeviceId
                && item.IsActive,
                cancellationToken);

        return login is null
            ? (
                device,
                customer,
                Results.Conflict(new
                {
                    code = "customer_login_not_active",
                    message = "ورود مشتری روی این رایانه دیگر فعال نیست."
                }))
            : (device, customer, null);
    }

    public static async Task<AgentDevice?> ResolveRegisteredDeviceAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken)
    {
        var cookieDevice = await ResolveDeviceFromCookieAsync(context, database, requireOnline: false, cancellationToken);
        if (cookieDevice is not null)
            return cookieDevice;

        var remoteIp = context.Connection.RemoteIpAddress;
        var local = remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);

        if (remoteIp is not null && !local)
        {
            var ipText = remoteIp.ToString();
            var remoteAgents = await database.AgentDevices
                .AsNoTracking()
                .Include(item => item.Station)
                .Where(item => item.IsActive
                    && item.LastIpAddress == ipText)
                .ToListAsync(cancellationToken);

            var resolved = remoteAgents
                .OrderByDescending(item => item.LastSeenAt)
                .FirstOrDefault();

            RememberClientDevice(context, resolved);
            return resolved;
        }

        var localAgents = await database.AgentDevices
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.IsActive && item.LastSeenAt.HasValue)
            .ToListAsync(cancellationToken);

        var localResolved = localAgents
            .OrderByDescending(item => item.LastSeenAt)
            .FirstOrDefault();

        RememberClientDevice(context, localResolved);
        return localResolved;
    }

    public static async Task<AgentDevice?> ResolveDeviceAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken)
    {
        var cookieDevice = await ResolveDeviceFromCookieAsync(context, database, requireOnline: true, cancellationToken);
        if (cookieDevice is not null)
            return cookieDevice;

        var remoteIp = context.Connection.RemoteIpAddress;
        var local = remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);

        if (remoteIp is not null && !local)
        {
            var ipText = remoteIp.ToString();
            var remoteAgents = await database.AgentDevices
                .AsNoTracking()
                .Include(item => item.Station)
                .Where(item => item.IsActive
                    && item.IsOnline
                    && item.LastIpAddress == ipText)
                .ToListAsync(cancellationToken);

            var resolved = remoteAgents
                .OrderByDescending(item => item.LastSeenAt)
                .FirstOrDefault();

            RememberClientDevice(context, resolved);
            return resolved;
        }

        var localAgents = await database.AgentDevices
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.IsActive && item.IsOnline && item.LastSeenAt.HasValue)
            .ToListAsync(cancellationToken);

        var localResolved = localAgents
            .OrderByDescending(item => item.LastSeenAt)
            .FirstOrDefault();

        RememberClientDevice(context, localResolved);
        return localResolved;
    }

    private const string ClientDeviceCookieName = "gamenet_client_device";
    private const string ClientDeviceProtectorPurpose = "GameNetManager.ClientDeviceIdentity";
    private static readonly TimeSpan ClientDeviceCookieLifetime = TimeSpan.FromDays(30);

    private static async Task<AgentDevice?> ResolveDeviceFromCookieAsync(
        HttpContext context,
        GameNetDbContext database,
        bool requireOnline,
        CancellationToken cancellationToken)
    {
        var protectedValue = context.Request.Cookies[ClientDeviceCookieName];
        if (string.IsNullOrWhiteSpace(protectedValue))
            return null;

        var provider = context.RequestServices.GetService<IDataProtectionProvider>();
        if (provider is null)
            return null;

        try
        {
            var protector = provider.CreateProtector(ClientDeviceProtectorPurpose);
            var value = protector.Unprotect(protectedValue);
            var parts = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !Guid.TryParse(parts[0], out var deviceId)
                || !long.TryParse(parts[1], out var issuedTicks))
                return null;

            var issuedAt = new DateTimeOffset(issuedTicks, TimeSpan.Zero);
            if (DateTimeOffset.UtcNow - issuedAt > ClientDeviceCookieLifetime)
                return null;

            var query = database.AgentDevices
                .AsNoTracking()
                .Include(item => item.Station)
                .Where(item => item.Id == deviceId && item.IsActive);

            if (requireOnline)
                query = query.Where(item => item.IsOnline);

            return await query.FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception) when (
            context.RequestServices.GetService<ILoggerFactory>() is not null)
        {
            return null;
        }
    }

    private static void RememberClientDevice(HttpContext context, AgentDevice? device)
    {
        if (device is null)
            return;

        var provider = context.RequestServices.GetService<IDataProtectionProvider>();
        if (provider is null)
            return;

        var protector = provider.CreateProtector(ClientDeviceProtectorPurpose);
        var payload = $"{device.Id:D}|{DateTimeOffset.UtcNow.UtcTicks}";
        var protectedValue = protector.Protect(payload);

        context.Response.Cookies.Append(
            ClientDeviceCookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = ClientDeviceCookieLifetime,
                Expires = DateTimeOffset.UtcNow.Add(ClientDeviceCookieLifetime)
            });
    }

    private sealed record ClientRequestRequest(
        Guid CustomerId,
        Guid LoginId,
        string Kind,
        string? Message,
        Guid? ProductId,
        int? Quantity);

    private sealed record ClientCustomerCommandRequest(
        Guid CustomerId,
        Guid LoginId);

    private sealed record ClientGameCommandRequest(
        Guid CustomerId,
        Guid LoginId,
        Guid SessionId,
        Guid GameId);
}
