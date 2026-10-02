namespace GameNetManager.Shared.Contracts;

public sealed record GameLibraryDto(Guid Id, string Name, string? Version, string Category, string? Launcher, string? InstallPath, string? ExecutablePath, string? LaunchArguments, string? ConnectionType, string? TargetSystem, string? TargetZone, string? TargetScope, string? TargetStations, string? ProcessNames, string? CoverPath, string? TrailerPath, bool Active, string Status, int ActiveUsers);

public sealed record GameLibraryWriteRequest(string Name, string? Version, string? Category, string? Launcher, string? InstallPath, string? ExecutablePath, string? LaunchArguments, string? ConnectionType, string? TargetSystem, string? TargetZone, string? TargetScope, string? TargetStations, string? ProcessNames, string? CoverPath, string? TrailerPath, bool Active = true);

public sealed record GameLibraryOptionDto(Guid Id, string Name);

public sealed record GameAccountPoolDto(Guid Id, string Title, string Platform, string? Launcher, string? Login, string Status, string Owner, DateTimeOffset? ExpiresAt, string[] AllowedGameIds, GameLibraryOptionDto[] AllowedGames, Guid? AssignedClientId, string? AssignedClientName, Guid? ActiveGameId, string? ActiveGameName, string GuardStatus, bool Active);

public sealed record GameAccountPoolWriteRequest(string Title, string Platform, string? Launcher, string? Login, string? Password, string? Owner, DateTimeOffset? ExpiresAt, string? GuardStatus, Guid[]? AllowedGameIds, bool Active = true);

public sealed record GameAccountLeaseRequest(Guid GameId, Guid AgentDeviceId, Guid? CustomerId = null);

public sealed record GameAccountPoolAuditDto(Guid Id, DateTimeOffset CreatedAt, string Action, string? Details, string? Operator);
