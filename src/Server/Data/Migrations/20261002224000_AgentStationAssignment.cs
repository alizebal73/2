using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AgentStationAssignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AgentDevices_StationId",
            table: "AgentDevices");

        migrationBuilder.CreateIndex(
            name: "IX_AgentDevices_StationId",
            table: "AgentDevices",
            column: "StationId",
            unique: true,
            filter: "StationId IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AgentDevices_StationId",
            table: "AgentDevices");

        migrationBuilder.CreateIndex(
            name: "IX_AgentDevices_StationId",
            table: "AgentDevices",
            column: "StationId");
    }
}
