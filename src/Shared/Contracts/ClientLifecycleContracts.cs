namespace GameNetManager.Shared.Contracts;

public static class ClientLifecycleStates
{
    public const string Starting = "Starting";
    public const string Running = "Running";
    public const string Degraded = "Degraded";
    public const string Recovering = "Recovering";
    public const string UpdatePending = "UpdatePending";
    public const string Updating = "Updating";
    public const string Failed = "Failed";

    public static bool IsKnown(string? state)
        => state?.Trim() is Starting or Running or Degraded or Recovering or UpdatePending or Updating or Failed;
}

public sealed record ClientReleaseCompatibilityDto(
    string ProductVersion,
    string MinimumClientVersion,
    string RecommendedClientVersion,
    string UpdateChannel,
    bool Compatible,
    bool UpdateRecommended);

public sealed record ClientLifecycleStatusDto(
    Guid AgentId,
    string DeviceId,
    string LifecycleState,
    string AgentVersion,
    string? PendingUpdateVersion,
    string? LastUpdateError,
    DateTimeOffset? LastHealthyAt,
    DateTimeOffset? LifecycleStateChangedAt,
    bool IsOnline,
    ClientReleaseCompatibilityDto Compatibility);
