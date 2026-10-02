using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ClientLifecycleState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LifecycleState",
            table: "AgentDevices",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "Starting");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastHealthyAt",
            table: "AgentDevices",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastUpdateError",
            table: "AgentDevices",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LifecycleStateChangedAt",
            table: "AgentDevices",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PendingUpdateVersion",
            table: "AgentDevices",
            type: "TEXT",
            maxLength: 60,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LifecycleState", table: "AgentDevices");
        migrationBuilder.DropColumn(name: "LastHealthyAt", table: "AgentDevices");
        migrationBuilder.DropColumn(name: "LastUpdateError", table: "AgentDevices");
        migrationBuilder.DropColumn(name: "LifecycleStateChangedAt", table: "AgentDevices");
        migrationBuilder.DropColumn(name: "PendingUpdateVersion", table: "AgentDevices");
    }
}
