namespace GameNetManager.Shared.Contracts;

public sealed record GameLibraryDto(
    Guid Id,
    string Name,
    string? Version,
    string Category,
    string? Launcher,
    string? InstallPath,
    string? ExecutablePath,
    string? LaunchArguments,
    string? ConnectionType,
    string? TargetSystem,
    string? TargetZone,
    string? ProcessNames,
    string? CoverPath,
    bool Active,
    string Status,
    int ActiveUsers);

public sealed record GameLibraryWriteRequest(
    string Name,
    string? Version,
    string? Category,
    string? Launcher,
    string? InstallPath,
    string? ExecutablePath,
    string? LaunchArguments,
    string? ConnectionType,
    string? TargetSystem,
    string? TargetZone,
    string? ProcessNames,
    string? CoverPath,
    bool Active = true);
