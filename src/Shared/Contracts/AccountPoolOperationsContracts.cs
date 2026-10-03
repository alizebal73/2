namespace GameNetManager.Shared.Contracts;

public sealed record AccountPoolHealthDto(
    int Total,
    int Free,
    int InUse,
    int Locked,
    int Expired,
    int MissingCredential,
    int ActiveLeases,
    int ExpiringLeases);

public sealed record AccountLeaseHistoryDto(
    Guid LeaseId,
    Guid AccountId,
    Guid GameId,
    string AccountTitle,
    string GameName,
    string Platform,
    string? AssignedClient,
    Guid? AgentDeviceId,
    Guid? CustomerId,
    Guid? SessionId,
    DateTimeOffset LeasedAt,
    DateTimeOffset? ReleasedAt,
    string State,
    string? ReleaseReason);
