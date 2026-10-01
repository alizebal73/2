namespace GameNetManager.Server.Data;

public sealed class StationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Zone { get; set; }
    public required string Type { get; set; }
    public long RatePerHour { get; set; }
    public required string State { get; set; }
}