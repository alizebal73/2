namespace GameNetManager.Server.Data;

public sealed class Station : BaseEntity
{
    public required string Name { get; set; }
    public required string Zone { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal RatePerHour { get; set; }
    public StationState State { get; set; } = StationState.Available;
    public bool IsActive { get; set; } = true;

    public Guid StationTypeId { get; set; }
    public StationType? StationType { get; set; }

    public Guid? TariffId { get; set; }
    public Tariff? Tariff { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}