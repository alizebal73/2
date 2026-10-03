using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ActiveSessionOwnershipGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions");

        migrationBuilder.DropIndex(
            name: "IX_Sessions_StationId",
            table: "Sessions");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions",
            column: "CustomerLoginId",
            unique: true,
            filter: "CustomerLoginId IS NOT NULL AND State = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_StationId",
            table: "Sessions",
            column: "StationId",
            unique: true,
            filter: "State = 'Active'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions");

        migrationBuilder.DropIndex(
            name: "IX_Sessions_StationId",
            table: "Sessions");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions",
            column: "CustomerLoginId");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_StationId",
            table: "Sessions",
            column: "StationId");
    }
}
