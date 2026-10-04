namespace GameNetManager.Shared.Contracts;

public sealed record TariffDto(
    Guid Id,
    string Name,
    string? Description,
    decimal HourlyRate,
    decimal DailyRate,
    bool IsActive);

public sealed record SaveTariffRequest(
    string Name,
    string? Description,
    decimal HourlyRate,
    decimal DailyRate,
    bool IsActive = true);
