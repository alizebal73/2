namespace GameNetManager.Server.Data;

public enum AccountPoolStatus
{
    Free,
    InUse,
    Locked
}

public enum AccountLeaseState
{
    Active,
    Released,
    Cancelled
}

public sealed class AccountPoolEntry : BaseEntity
{
    public required string Title { get; set; }
    public required string Platform { get; set; }
    public string? Login { get; set; }
    public string? SecretHash { get; set; }
    public string? SecretCiphertext { get; set; }
    public string Owner { get; set; } = "مجموعه";
    public DateTime? ExpiresAt { get; set; }
    public string AllowedGameIdsCsv { get; set; } = string.Empty;
    public AccountPoolStatus Status { get; set; } = AccountPoolStatus.Free;
    public bool IsActive { get; set; } = true;
    public Guid? AssignedAgentDeviceId { get; set; }
    public AgentDevice? AssignedAgentDevice { get; set; }
    public ICollection<AccountLease> Leases { get; set; } = new List<AccountLease>();
}

public sealed class AccountLease : BaseEntity
{
    public Guid AccountPoolEntryId { get; set; }
    public AccountPoolEntry AccountPoolEntry { get; set; } = default!;
    public Guid GameId { get; set; }
    public Game Game { get; set; } = default!;
    public Guid? AgentDeviceId { get; set; }
    public AgentDevice? AgentDevice { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SessionId { get; set; }
    public Session? Session { get; set; }
    public required string LeaseToken { get; set; }
    public DateTimeOffset LeasedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? CredentialAccessExpiresAt { get; set; }
    public string? ReleaseReason { get; set; }
    public AccountLeaseState State { get; set; } = AccountLeaseState.Active;
}
