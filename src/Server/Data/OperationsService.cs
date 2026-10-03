using GameNetManager.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed class OperationsService(GameNetDbContext database)
{
    public async Task<IReadOnlyList<StationManagementDto>> ListStationsAsync(CancellationToken cancellationToken)
    {
        return await database.Stations
            .AsNoTracking()
            .OrderBy(item => item.Zone)
            .ThenBy(item => item.Name)
            .Select(item => new StationManagementDto(
                item.Id,
                item.Name,
                item.Zone,
                item.Type,
                item.RatePerHour,
                item.State.ToString(),
                item.StationTypeId,
                item.TariffId,
                item.NetworkRoute,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<StationManagementDto> SaveStationAsync(
        Guid? id,
        StationWriteRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var zone = request.Zone.Trim();
        var type = request.Type.Trim();
        var network = request.NetworkRoute.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(zone) || string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("نام، زون و نوع ایستگاه الزامی هستند.");
        if (request.RatePerHour < 0)
            throw new ArgumentException("تعرفه ایستگاه نمی‌تواند منفی باشد.");
        if (network is not ("internet1" or "internet2" or "lan"))
            throw new ArgumentException("مسیر شبکه معتبر نیست.");

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var stationTypeExists = await database.StationTypes.AnyAsync(item => item.Id == request.StationTypeId, cancellationToken);
        if (!stationTypeExists)
            throw new KeyNotFoundException("نوع ایستگاه پیدا نشد.");

        if (request.TariffId.HasValue
            && !await database.Tariffs.AnyAsync(item => item.Id == request.TariffId.Value && item.IsActive, cancellationToken))
            throw new KeyNotFoundException("تعرفه معتبر پیدا نشد.");

        Station station;
        var action = "StationCreate";
        if (id.HasValue)
        {
            station = await database.Stations
                .FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("ایستگاه پیدا نشد.");
            action = "StationUpdate";

            if (await database.Sessions.AnyAsync(
                    item => item.StationId == station.Id
                        && item.State == SessionState.Active
                        && !request.IsActive,
                    cancellationToken))
                throw new InvalidOperationException("ایستگاه دارای Session فعال است و نمی‌توان آن را غیرفعال کرد.");
        }
        else
        {
            station = new Station();
            database.Stations.Add(station);
        }

        var duplicate = await database.Stations.AnyAsync(
            item => item.Id != station.Id && item.Name == name,
            cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("نام ایستگاه تکراری است.");

        station.Name = name;
        station.Zone = zone;
        station.Type = type;
        station.StationTypeId = request.StationTypeId;
        station.TariffId = request.TariffId;
        station.RatePerHour = request.RatePerHour;
        station.NetworkRoute = network;
        station.IsActive = request.IsActive;
        if (!request.IsActive && station.State == StationState.Available)
            station.State = StationState.Offline;
        else if (request.IsActive && station.State == StationState.Offline)
            station.State = StationState.Available;

        database.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = "Station",
            EntityId = station.Id.ToString(),
            AppUserId = appUserId,
            Details = $"{station.Name} · {station.Zone} · {station.NetworkRoute}"
        });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StationManagementDto(
            station.Id,
            station.Name,
            station.Zone,
            station.Type,
            station.RatePerHour,
            station.State.ToString(),
            station.StationTypeId,
            station.TariffId,
            station.NetworkRoute,
            station.IsActive);
    }

    public async Task<OperationsHealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        var stations = await database.Stations.AsNoTracking().ToListAsync(cancellationToken);
        var agents = await database.AgentDevices.AsNoTracking().ToListAsync(cancellationToken);
        var activeLeases = await database.AccountLeases
            .AsNoTracking()
            .CountAsync(item => item.State == AccountLeaseState.Active, cancellationToken);
        var activeSessions = await database.Sessions
            .AsNoTracking()
            .CountAsync(item => item.State == SessionState.Active, cancellationToken);

        return new OperationsHealthDto(
            DateTimeOffset.UtcNow,
            stations.Count,
            stations.Count(item => item.State == StationState.Available && item.IsActive),
            stations.Count(item => item.State == StationState.Occupied),
            stations.Count(item => item.State == StationState.Maintenance),
            stations.Count(item => item.State == StationState.Offline || !item.IsActive),
            agents.Count,
            agents.Count(item => item.IsActive && item.IsOnline),
            activeSessions,
            activeLeases,
            stations
                .GroupBy(item => item.NetworkRoute)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));
    }
}
