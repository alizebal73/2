namespace GameNetManager.Shared.Contracts;

public sealed record AgentRegistrationRequest(
    string DeviceId,
    string Name,
    Guid? StationId,
    string AgentVersion,
    string? OsVersion);

public sealed record AgentRegistrationResponse(
    Guid AgentId,
    string DeviceId,
    string AgentToken,
    string? StationName,
    DateTimeOffset ServerUtcNow,
    int HeartbeatIntervalSeconds,
    int OfflineAfterSeconds);

public sealed record AgentReadyDto(
    Guid AgentId,
    string DeviceId,
    DateTimeOffset ServerUtcNow,
    int HeartbeatIntervalSeconds);

public sealed record AgentHeartbeatRequest(
    string AgentVersion,
    string? OsVersion,
    double? CpuUsagePercent,
    long? MemoryAvailableBytes,
    long? UptimeSeconds);

public sealed record AgentHeartbeatResponse(
    Guid AgentId,
    DateTimeOffset ServerUtcNow,
    int HeartbeatIntervalSeconds,
    bool Accepted);

public sealed record AgentStatusDto(
    Guid AgentId,
    string DeviceId,
    string Name,
    Guid? StationId,
    string? StationName,
    bool IsOnline,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ConnectedAt,
    string? AgentVersion,
    string? OsVersion,
    double? CpuUsagePercent,
    long? MemoryAvailableBytes,
    long? UptimeSeconds);
