using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class StationInternetNetwork : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Network",
            table: "Stations",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateIndex(
            name: "IX_Stations_Network",
            table: "Stations",
            column: "Network");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Stations_Network",
            table: "Stations");

        migrationBuilder.DropColumn(
            name: "Network",
            table: "Stations");
    }
}