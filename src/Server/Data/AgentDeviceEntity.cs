using GameNetManager.Shared.Contracts;
using System.ComponentModel.DataAnnotations;

namespace GameNetManager.Server.Data;

public sealed class AgentDevice : BaseEntity
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string AgentTokenHash { get; set; } = string.Empty;

    public Guid? StationId { get; set; }
    public Station? Station { get; set; }

    public string? AgentVersion { get; set; }
    public string? OsVersion { get; set; }
    public double? CpuUsagePercent { get; set; }
    public long? MemoryAvailableBytes { get; set; }
    public long? UptimeSeconds { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public string? LastIpAddress { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsOnline { get; set; }
    public bool IsLocked { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public bool KioskEnabled { get; set; }
    public bool LockOnDisconnect { get; set; } = true;
    public string LifecycleState { get; set; } = ClientLifecycleStates.Starting;
    public string? PendingUpdateVersion { get; set; }
    public string? LastUpdateError { get; set; }
    public DateTimeOffset? LastHealthyAt { get; set; }
    public DateTimeOffset? LifecycleStateChangedAt { get; set; }
    public bool IsActive { get; set; } = true;
}


public sealed class AgentProcessTelemetry : BaseEntity
{
    public Guid AgentDeviceId { get; set; }
    public AgentDevice AgentDevice { get; set; } = default!;
    public Guid? GameId { get; set; }
    public Game? Game { get; set; }
    public required string ProcessName { get; set; }
    public int ProcessId { get; set; }
    public DateTimeOffset ObservedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
}
