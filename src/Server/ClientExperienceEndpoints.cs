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
                game.version,
                game.genre,
                game.status,
                game.cover,
                game.trailer,
                game.connectionType,
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
                    icon = item.Category.Contains("نوش", StringComparison.OrdinalIgnoreCase)
                        ? "🥤"
                        : item.Category.Contains("قهو", StringComparison.OrdinalIgnoreCase)
                            ? "☕"
                            : item.Category.Contains("ساند", StringComparison.OrdinalIgnoreCase)
                                ? "🥪"
                                : "🛒"
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
    }

    private static async Task<AgentDevice?> ResolveDeviceAsync(
        HttpContext context,
        GameNetDbContext database,
        CancellationToken cancellationToken)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        var local = remoteIp is not null && System.Net.IPAddress.IsLoopback(remoteIp);

        if (remoteIp is not null && !local)
        {
            var ipText = remoteIp.ToString();
            return await database.AgentDevices
                .AsNoTracking()
                .Include(item => item.Station)
                .Where(item => item.IsActive
                    && item.IsOnline
                    && item.LastIpAddress == ipText)
                .OrderByDescending(item => item.LastSeenAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await database.AgentDevices
            .AsNoTracking()
            .Include(item => item.Station)
            .Where(item => item.IsActive && item.IsOnline && item.LastSeenAt.HasValue)
            .OrderByDescending(item => item.LastSeenAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
