namespace GameNetManager.Shared.Contracts;

public sealed record DashboardSnapshotDto(
    int TotalStations,
    IReadOnlyList<StationDto> Stations,
    DateTimeOffset GeneratedAt);