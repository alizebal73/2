using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class GameLibraryDomain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Version",
            table: "Games",
            type: "TEXT",
            maxLength: 60,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Launcher",
            table: "Games",
            type: "TEXT",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InstallPath",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutablePath",
            table: "Games",
            type: "TEXT",
            maxLength: 260,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LaunchArguments",
            table: "Games",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ConnectionType",
            table: "Games",
            type: "TEXT",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetSystem",
            table: "Games",
            type: "TEXT",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetZone",
            table: "Games",
            type: "TEXT",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProcessNames",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CoverPath",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Version", table: "Games");
        migrationBuilder.DropColumn(name: "Launcher", table: "Games");
        migrationBuilder.DropColumn(name: "InstallPath", table: "Games");
        migrationBuilder.DropColumn(name: "ExecutablePath", table: "Games");
        migrationBuilder.DropColumn(name: "LaunchArguments", table: "Games");
        migrationBuilder.DropColumn(name: "ConnectionType", table: "Games");
        migrationBuilder.DropColumn(name: "TargetSystem", table: "Games");
        migrationBuilder.DropColumn(name: "TargetZone", table: "Games");
        migrationBuilder.DropColumn(name: "ProcessNames", table: "Games");
        migrationBuilder.DropColumn(name: "CoverPath", table: "Games");
    }
}
