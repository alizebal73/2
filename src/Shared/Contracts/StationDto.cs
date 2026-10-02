namespace GameNetManager.Shared.Contracts;

public sealed record StationDto(
    Guid Id,
    string Name,
    string Zone,
    string Type,
    long RatePerHour,
    string State,
    string? CustomerUsername = null,
    string? CustomerFullName = null,
    decimal CustomerDebt = 0,
    string? CustomerNote = null,
    int? RemainingMinutes = null,
    int? Network = null);