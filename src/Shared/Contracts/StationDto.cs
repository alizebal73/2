namespace GameNetManager.Shared.Contracts;

public sealed record StationDto(
    Guid Id,
    string Name,
    string Zone,
    string Type,
    long RatePerHour,
    string State);