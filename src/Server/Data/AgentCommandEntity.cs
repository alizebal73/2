namespace GameNetManager.Server.Data;

public sealed class AgentCommand : BaseEntity
{
    public Guid AgentDeviceId { get; set; }
    public AgentDevice? AgentDevice { get; set; }

    public Guid? RequestedByAppUserId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool? Succeeded { get; set; }
    public string? ResultMessage { get; set; }
    public string? AgentConnectionId { get; set; }
}
