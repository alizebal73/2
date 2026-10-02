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
    int HeartbeatIntervalSeconds,
    bool IsLocked);

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
    bool IsLocked,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ConnectedAt,
    string? AgentVersion,
    string? OsVersion,
    double? CpuUsagePercent,
    long? MemoryAvailableBytes,
    long? UptimeSeconds);


public static class AgentCommandTypes
{
    public const string Ping = "ping";
    public const string Lock = "lock";
    public const string Unlock = "unlock";

    public static bool IsSupported(string? commandType)
        => commandType?.Trim().ToLowerInvariant() is Ping or Lock or Unlock;
}

public sealed record AgentCommandRequest(
    string CommandType,
    string? PayloadJson);

public sealed record AgentCommandEnvelope(
    Guid CommandId,
    string CommandType,
    string? PayloadJson,
    DateTimeOffset RequestedAt);

public sealed record AgentCommandAcknowledgement(
    Guid CommandId,
    bool Success,
    string? Message,
    DateTimeOffset CompletedAt);

public sealed record AgentCommandStatusDto(
    Guid CommandId,
    Guid AgentDeviceId,
    string CommandType,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? CompletedAt,
    bool? Succeeded,
    string? ResultMessage);
