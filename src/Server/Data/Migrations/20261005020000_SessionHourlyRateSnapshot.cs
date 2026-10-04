using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class SessionHourlyRateSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "HourlyRateSnapshot",
            table: "Sessions",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.Sql(@"
UPDATE Sessions
SET HourlyRateSnapshot = COALESCE(
    HourlyRateOverride,
    (
        SELECT T.HourlyRate
        FROM Stations S
        LEFT JOIN Tariffs T ON T.Id = S.TariffId
        WHERE S.Id = Sessions.StationId
    ),
    (
        SELECT S.RatePerHour
        FROM Stations S
        WHERE S.Id = Sessions.StationId
    )
)
WHERE HourlyRateSnapshot IS NULL;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HourlyRateSnapshot",
            table: "Sessions");
    }
}
