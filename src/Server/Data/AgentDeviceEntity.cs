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
    public bool IsActive { get; set; } = true;
}
