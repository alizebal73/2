using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ReservationIndexParity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Reservations_StationId",
            table: "Reservations");

        migrationBuilder.DropIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations");

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations",
            columns: new[] { "StationId", "StartAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations");

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_StationId",
            table: "Reservations",
            column: "StationId");

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations",
            columns: new[] { "StationId", "StartAt" },
            unique: true,
            filter: "State = 'Active'");
    }
}
