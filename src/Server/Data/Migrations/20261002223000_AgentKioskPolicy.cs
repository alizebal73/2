using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AgentKioskPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "KioskEnabled",
            table: "AgentDevices",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "LockOnDisconnect",
            table: "AgentDevices",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "KioskEnabled", table: "AgentDevices");
        migrationBuilder.DropColumn(name: "LockOnDisconnect", table: "AgentDevices");
    }
}
