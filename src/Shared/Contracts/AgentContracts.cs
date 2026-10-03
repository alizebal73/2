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
    bool IsLocked,
    bool KioskEnabled,
    bool LockOnDisconnect);

public sealed record AgentHeartbeatRequest(
    string AgentVersion,
    string? OsVersion,
    double? CpuUsagePercent,
    long? MemoryAvailableBytes,
    long? UptimeSeconds,
    bool IsLocked,
    string? LifecycleState = null,
    string? PendingUpdateVersion = null,
    string? LastUpdateError = null);

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
    bool KioskEnabled,
    bool LockOnDisconnect,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? ConnectedAt,
    string? AgentVersion,
    string? OsVersion,
    double? CpuUsagePercent,
    long? MemoryAvailableBytes,
    long? UptimeSeconds,
    string LifecycleState,
    string? PendingUpdateVersion,
    string? LastUpdateError,
    DateTimeOffset? LastHealthyAt,
    DateTimeOffset? LifecycleStateChangedAt);


public static class AgentCommandTypes
{
    public const string Ping = "ping";
    public const string Lock = "lock";
    public const string Unlock = "unlock";
    public const string LogoutLock = "logout-lock";
    public const string Update = "update";
    public const string Rollback = "rollback";

    public static bool IsSupported(string? commandType)
        => commandType?.Trim().ToLowerInvariant() is Ping or Lock or Unlock or LogoutLock or Update or Rollback;
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
    DateTimeOffset CompletedAt,
    bool Final = true,
    string? FinalStatus = null);

public sealed record AgentPolicyRequest(
    bool KioskEnabled,
    bool LockOnDisconnect);

public sealed record AgentPolicyDto(
    Guid AgentId,
    bool KioskEnabled,
    bool LockOnDisconnect);

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


public sealed record AgentSessionStartRequest(
    Guid CustomerId,
    Guid CustomerLoginId,
    Guid? TariffId,
    decimal? HourlyRateOverride,
    int? Persons,
    Guid? GameId = null);

public sealed record AgentSessionStartResponse(
    Guid SessionId,
    Guid StationId,
    Guid CustomerId,
    DateTimeOffset StartAt);

public sealed record AgentSessionEndRequest(
    Guid SessionId,
    Guid? CustomerLoginId);

public sealed record AgentSessionEndResponse(
    Guid SessionId,
    Guid StationId,
    DateTimeOffset EndAt,
    string State);
