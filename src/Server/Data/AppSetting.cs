namespace GameNetManager.Server.Data;

public sealed class AppSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string ScopeKey { get; set; } = "global";
    public required string ValueJson { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public AppUser? UpdatedByUser { get; set; }
}
