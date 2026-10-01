namespace GameNetManager.Shared.Contracts;

public sealed record ServerInfoDto(
    string Name,
    string Environment,
    DateTimeOffset UtcNow);