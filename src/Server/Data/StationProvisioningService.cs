using Microsoft.EntityFrameworkCore;

namespace GameNetManager.Server.Data;

public sealed record StationListItem(
    Guid Id,
    string Name,
    string Zone,
    string Type,
    decimal RatePerHour,
    int Network,
    string State,
    bool IsActive,
    Guid StationTypeId,
    string StationTypeName,
    Guid? TariffId,
    string? TariffName,
    Guid? AgentDeviceId,
    string? AgentDeviceName);

public sealed record CreateStationRequest(
    string Name,
    string Zone,
    string Type,
    decimal RatePerHour,
    int Network = 1,
    Guid? TariffId = null);

public sealed record UpdateStationRequest(
    string Name,
    string Zone,
    string Type,
    decimal RatePerHour,
    int Network,
    Guid? TariffId = null,
    bool IsActive = true);

public sealed record ProvisionStationRangeRequest(
    string Prefix,
    int StartNumber,
    int Count,
    string Zone,
    string Type,
    decimal RatePerHour,
    int Network = 1,
    Guid? TariffId = null);

public sealed class StationProvisioningService(GameNetDbContext database)
{
    public async Task<IReadOnlyList<StationListItem>> ListAsync(CancellationToken cancellationToken)
    {
        return await database.Stations
            .AsNoTracking()
            .Include(item => item.StationType)
            .Include(item => item.Tariff)
            .OrderBy(item => item.Zone)
            .ThenBy(item => item.Name)
            .Select(item => new StationListItem(
                item.Id,
                item.Name,
                item.Zone,
                item.Type,
                item.RatePerHour,
                item.Network,
                item.State.ToString(),
                item.IsActive,
                item.StationTypeId,
                item.StationType!.Name,
                item.TariffId,
                item.Tariff != null ? item.Tariff.Name : null,
                database.AgentDevices
                    .Where(agent => agent.StationId == item.Id && agent.IsActive)
                    .Select(agent => (Guid?)agent.Id)
                    .FirstOrDefault(),
                database.AgentDevices
                    .Where(agent => agent.StationId == item.Id && agent.IsActive)
                    .Select(agent => agent.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<(StationListItem? Item, string? ErrorCode, string? ErrorMessage)> CreateAsync(
        CreateStationRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(
            request.Name,
            request.Zone,
            request.Type,
            request.RatePerHour,
            request.Network);

        if (normalized.errorCode is not null)
            return (null, normalized.errorCode, normalized.errorMessage);

        if (await database.Stations.AnyAsync(item => item.Name == normalized.name, cancellationToken))
            return (null, "station_exists", "ایستگاهی با این نام از قبل وجود دارد.");

        var stationType = await GetOrCreateStationTypeAsync(normalized.type, cancellationToken);
        var tariff = await ResolveTariffAsync(request.TariffId, cancellationToken);
        if (request.TariffId.HasValue && tariff is null)
            return (null, "tariff_not_found", "تعرفهٔ انتخاب‌شده پیدا نشد یا غیرفعال است.");

        var station = new Station
        {
            Name = normalized.name,
            Zone = normalized.zone,
            Type = normalized.type,
            RatePerHour = normalized.rate,
            Network = normalized.network,
            State = StationState.Available,
            IsActive = true,
            StationTypeId = stationType.Id,
            TariffId = tariff?.Id
        };

        database.Stations.Add(station);
        database.AuditLogs.Add(new AuditLog
        {
            Action = "StationCreated",
            EntityName = "Station",
            EntityId = station.Id.ToString(),
            Details = $"ایجاد ایستگاه · {station.Name} · {station.Type} · {station.Zone} · اینترنت {station.Network}",
            AppUserId = appUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        return (await FindAsync(station.Id, cancellationToken), null, null);
    }

    public async Task<(StationListItem? Item, string? ErrorCode, string? ErrorMessage)> UpdateAsync(
        Guid stationId,
        UpdateStationRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var station = await database.Stations.FirstOrDefaultAsync(item => item.Id == stationId, cancellationToken);
        if (station is null)
            return (null, "station_not_found", "ایستگاه پیدا نشد.");

        var normalized = NormalizeRequest(
            request.Name,
            request.Zone,
            request.Type,
            request.RatePerHour,
            request.Network);

        if (normalized.errorCode is not null)
            return (null, normalized.errorCode, normalized.errorMessage);

        if (await database.Stations.AnyAsync(item => item.Id != stationId && item.Name == normalized.name, cancellationToken))
            return (null, "station_exists", "ایستگاهی با این نام از قبل وجود دارد.");

        if (!request.IsActive)
        {
            var hasActiveSession = await database.Sessions.AnyAsync(
                item => item.StationId == stationId && item.State == SessionState.Active,
                cancellationToken);
            if (hasActiveSession)
                return (null, "station_active_session", "تا وقتی جلسهٔ فعالی روی این ایستگاه وجود دارد، غیرفعال‌سازی مجاز نیست.");

            var hasActiveAgent = await database.AgentDevices.AnyAsync(
                item => item.StationId == stationId && item.IsActive,
                cancellationToken);
            if (hasActiveAgent)
                return (null, "station_has_agent", "ابتدا Agent این ایستگاه را آزاد کنید.");
        }

        var stationType = await GetOrCreateStationTypeAsync(normalized.type, cancellationToken);
        var tariff = await ResolveTariffAsync(request.TariffId, cancellationToken);
        if (request.TariffId.HasValue && tariff is null)
            return (null, "tariff_not_found", "تعرفهٔ انتخاب‌شده پیدا نشد یا غیرفعال است.");

        station.Name = normalized.name;
        station.Zone = normalized.zone;
        station.Type = normalized.type;
        station.RatePerHour = normalized.rate;
        station.Network = normalized.network;
        station.StationTypeId = stationType.Id;
        station.TariffId = tariff?.Id;
        station.IsActive = request.IsActive;

        if (!request.IsActive)
            station.State = StationState.Offline;

        database.AuditLogs.Add(new AuditLog
        {
            Action = request.IsActive ? "StationUpdated" : "StationArchived",
            EntityName = "Station",
            EntityId = station.Id.ToString(),
            Details = request.IsActive
                ? $"ویرایش ایستگاه · {station.Name} · {station.Type} · اینترنت {station.Network}"
                : $"آرشیو ایستگاه · {station.Name}",
            AppUserId = appUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        return (await FindAsync(station.Id, cancellationToken), null, null);
    }

    public async Task<(int Created, IReadOnlyList<string> Names, string? ErrorCode, string? ErrorMessage)> ProvisionRangeAsync(
        ProvisionStationRangeRequest request,
        Guid appUserId,
        CancellationToken cancellationToken)
    {
        var prefix = request.Prefix?.Trim();
        var zone = request.Zone?.Trim();
        var type = request.Type?.Trim();

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 50)
            return (0, Array.Empty<string>(), "invalid_prefix", "پیشوند نام ایستگاه معتبر نیست.");
        if (request.StartNumber < 0 || request.StartNumber > 9999)
            return (0, Array.Empty<string>(), "invalid_start_number", "شمارهٔ شروع معتبر نیست.");
        if (request.Count < 1 || request.Count > 200)
            return (0, Array.Empty<string>(), "invalid_count", "تعداد ایستگاه باید بین ۱ تا ۲۰۰ باشد.");
        if (string.IsNullOrWhiteSpace(zone) || zone.Length > 60)
            return (0, Array.Empty<string>(), "invalid_zone", "زون ایستگاه معتبر نیست.");
        if (string.IsNullOrWhiteSpace(type) || type.Length > 50)
            return (0, Array.Empty<string>(), "invalid_type", "نوع ایستگاه معتبر نیست.");
        if (request.RatePerHour <= 0 || request.RatePerHour > 100_000_000)
            return (0, Array.Empty<string>(), "invalid_rate", "نرخ ساعتی ایستگاه معتبر نیست.");
        if (request.Network is < 1 or > 2)
            return (0, Array.Empty<string>(), "invalid_network", "شمارهٔ اینترنت باید ۱ یا ۲ باشد.");

        var names = Enumerable.Range(request.StartNumber, request.Count)
            .Select(number => $"{prefix}-{number:00}")
            .ToArray();

        var existing = await database.Stations
            .Where(item => names.Contains(item.Name))
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            return (0, existing, "station_exists", "بعضی از نام‌های ایستگاه از قبل وجود دارند.");

        var stationType = await GetOrCreateStationTypeAsync(type, cancellationToken);
        var tariff = await ResolveTariffAsync(request.TariffId, cancellationToken);
        if (request.TariffId.HasValue && tariff is null)
            return (0, Array.Empty<string>(), "tariff_not_found", "تعرفهٔ انتخاب‌شده پیدا نشد یا غیرفعال است.");

        foreach (var name in names)
        {
            database.Stations.Add(new Station
            {
                Name = name,
                Zone = zone,
                Type = type,
                RatePerHour = request.RatePerHour,
                Network = request.Network,
                State = StationState.Available,
                IsActive = true,
                StationTypeId = stationType.Id,
                TariffId = tariff?.Id
            });
        }

        database.AuditLogs.Add(new AuditLog
        {
            Action = "StationsProvisioned",
            EntityName = "Station",
            EntityId = string.Join(",", names),
            Details = $"Provision گروهی {names.Length} ایستگاه · {prefix} · {type} · {zone} · اینترنت {request.Network}",
            AppUserId = appUserId
        });

        await database.SaveChangesAsync(cancellationToken);
        return (names.Length, names, null, null);
    }

    private async Task<StationListItem?> FindAsync(Guid stationId, CancellationToken cancellationToken)
        => await database.Stations
            .AsNoTracking()
            .Include(item => item.StationType)
            .Include(item => item.Tariff)
            .Where(item => item.Id == stationId)
            .Select(item => new StationListItem(
                item.Id,
                item.Name,
                item.Zone,
                item.Type,
                item.RatePerHour,
                item.Network,
                item.State.ToString(),
                item.IsActive,
                item.StationTypeId,
                item.StationType!.Name,
                item.TariffId,
                item.Tariff != null ? item.Tariff.Name : null,
                database.AgentDevices.Where(agent => agent.StationId == item.Id && agent.IsActive).Select(agent => (Guid?)agent.Id).FirstOrDefault(),
                database.AgentDevices.Where(agent => agent.StationId == item.Id && agent.IsActive).Select(agent => agent.Name).FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<StationType> GetOrCreateStationTypeAsync(string type, CancellationToken cancellationToken)
    {
        var stationType = await database.StationTypes.FirstOrDefaultAsync(
            item => item.Name == type,
            cancellationToken);

        if (stationType is not null)
            return stationType;

        stationType = new StationType
        {
            Name = type,
            Description = "نوع ایستگاه ایجادشده هنگام Provisioning"
        };
        database.StationTypes.Add(stationType);
        await database.SaveChangesAsync(cancellationToken);
        return stationType;
    }

    private async Task<Tariff?> ResolveTariffAsync(Guid? tariffId, CancellationToken cancellationToken)
        => tariffId.HasValue
            ? await database.Tariffs.FirstOrDefaultAsync(item => item.Id == tariffId.Value && item.IsActive, cancellationToken)
            : null;

    private static (string name, string zone, string type, decimal rate, int network, string? errorCode, string? errorMessage) NormalizeRequest(
        string? name,
        string? zone,
        string? type,
        decimal rate,
        int network)
    {
        var normalizedName = name?.Trim();
        var normalizedZone = zone?.Trim();
        var normalizedType = type?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80)
            return ("", "", "", 0, 0, "invalid_name", "نام ایستگاه معتبر نیست.");
        if (string.IsNullOrWhiteSpace(normalizedZone) || normalizedZone.Length > 60)
            return ("", "", "", 0, 0, "invalid_zone", "زون ایستگاه معتبر نیست.");
        if (string.IsNullOrWhiteSpace(normalizedType) || normalizedType.Length > 50)
            return ("", "", "", 0, 0, "invalid_type", "نوع ایستگاه معتبر نیست.");
        if (rate <= 0 || rate > 100_000_000)
            return ("", "", "", 0, 0, "invalid_rate", "نرخ ساعتی ایستگاه معتبر نیست.");
        if (network is < 1 or > 2)
            return ("", "", "", 0, 0, "invalid_network", "شمارهٔ اینترنت باید ۱ یا ۲ باشد.");

        return (normalizedName, normalizedZone, normalizedType, rate, network, null, null);
    }
}
