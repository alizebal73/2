namespace GameNetManager.Shared.Contracts;

public sealed record GameRecordDto(
    Guid Id,
    string Name,
    string Version,
    string? Genre,
    string Status,
    int ActiveUsers,
    string Path,
    string Executable,
    string Cover,
    string Trailer,
    string LaunchArgs,
    string ConnectionType,
    bool Active,
    string TargetSystem,
    string Target,
    string TargetZone,
    string TargetStations);

public sealed record SaveGameRequest(
    string Name,
    string? Version,
    string? Genre,
    string? Status,
    string? Path,
    string? Executable,
    string? Cover,
    string? Trailer,
    string? LaunchArgs,
    string? ConnectionType,
    bool Active,
    string? TargetSystem,
    string? Target,
    string? TargetZone,
    string? TargetStations);

public sealed record AccountPoolEntryDto(
    Guid Id,
    string Title,
    string Platform,
    string? Login,
    string Owner,
    DateTimeOffset? ExpiresAt,
    string[] AllowedGames,
    string Status,
    string? AssignedClient,
    Guid? AssignedAgentDeviceId);

public sealed record SaveAccountPoolEntryRequest(
    string Title,
    string Platform,
    string? Login,
    string? Secret,
    string? Owner,
    DateTimeOffset? ExpiresAt,
    Guid[] AllowedGameIds,
    string Status);

public sealed record AllocateAccountRequest(
    Guid GameId,
    Guid? AgentDeviceId,
    Guid? CustomerId,
    Guid? SessionId);

public sealed record AccountLeaseDto(
    Guid LeaseId,
    Guid AccountId,
    Guid GameId,
    string AccountTitle,
    string Platform,
    string? Login,
    string? AssignedClient,
    DateTimeOffset LeasedAt,
    string State);

public sealed record ReleaseAccountLeaseRequest(string? Reason);
