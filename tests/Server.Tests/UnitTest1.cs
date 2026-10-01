using System.Text.Json;
using GameNetManager.Shared.Contracts;

namespace GameNetManager.Server.Tests;

public sealed class DashboardContractTests
{
    [Fact]
    public void DashboardSnapshotSerializesWithWebContractNames()
    {
        var snapshot = new DashboardSnapshotDto(
            1,
            [new StationDto(Guid.Empty, "PC 01", "pc", "Gaming PC", 80000, "free")],
            DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"totalStations\":1", json);
        Assert.Contains("\"ratePerHour\":80000", json);
        Assert.Contains("\"generatedAt\":", json);
    }
}
