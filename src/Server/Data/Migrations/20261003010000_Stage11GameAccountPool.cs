using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class Stage11GameAccountPool : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Version",
            table: "Games",
            type: "TEXT",
            maxLength: 60,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "Games",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "offline");

        migrationBuilder.AddColumn<string>(
            name: "Path",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Executable",
            table: "Games",
            type: "TEXT",
            maxLength: 260,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Cover",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Trailer",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "LaunchArgs",
            table: "Games",
            type: "TEXT",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ConnectionType",
            table: "Games",
            type: "TEXT",
            maxLength: 40,
            nullable: false,
            defaultValue: "آنلاین");

        migrationBuilder.AddColumn<string>(
            name: "TargetSystem",
            table: "Games",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "all");

        migrationBuilder.AddColumn<string>(
            name: "Target",
            table: "Games",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "all");

        migrationBuilder.AddColumn<string>(
            name: "TargetZone",
            table: "Games",
            type: "TEXT",
            maxLength: 60,
            nullable: false,
            defaultValue: "pc");

        migrationBuilder.AddColumn<string>(
            name: "TargetStations",
            table: "Games",
            type: "TEXT",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "AccountPoolEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Platform = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Login = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                SecretHash = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                Owner = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AllowedGameIdsCsv = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                AssignedAgentDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountPoolEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccountPoolEntries_AgentDevices_AssignedAgentDeviceId",
                    column: x => x.AssignedAgentDeviceId,
                    principalTable: "AgentDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "AccountLeases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AccountPoolEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgentDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                LeaseToken = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                LeasedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReleasedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountLeases", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccountLeases_AccountPoolEntries_AccountPoolEntryId",
                    column: x => x.AccountPoolEntryId,
                    principalTable: "AccountPoolEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AccountLeases_AgentDevices_AgentDeviceId",
                    column: x => x.AgentDeviceId,
                    principalTable: "AgentDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_AccountLeases_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_AccountLeases_Games_GameId",
                    column: x => x.GameId,
                    principalTable: "Games",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AccountLeases_Sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "Sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccountPoolEntries_Title",
            table: "AccountPoolEntries",
            column: "Title",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccountPoolEntries_Status_IsActive",
            table: "AccountPoolEntries",
            columns: new[] { "Status", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountPoolEntries_AssignedAgentDeviceId",
            table: "AccountPoolEntries",
            column: "AssignedAgentDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_LeaseToken",
            table: "AccountLeases",
            column: "LeaseToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_AccountPoolEntryId_State",
            table: "AccountLeases",
            columns: new[] { "AccountPoolEntryId", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_GameId",
            table: "AccountLeases",
            column: "GameId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_AgentDeviceId",
            table: "AccountLeases",
            column: "AgentDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_CustomerId",
            table: "AccountLeases",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_SessionId",
            table: "AccountLeases",
            column: "SessionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountLeases");
        migrationBuilder.DropTable(name: "AccountPoolEntries");

        migrationBuilder.DropColumn(name: "Version", table: "Games");
        migrationBuilder.DropColumn(name: "Status", table: "Games");
        migrationBuilder.DropColumn(name: "Path", table: "Games");
        migrationBuilder.DropColumn(name: "Executable", table: "Games");
        migrationBuilder.DropColumn(name: "Cover", table: "Games");
        migrationBuilder.DropColumn(name: "Trailer", table: "Games");
        migrationBuilder.DropColumn(name: "LaunchArgs", table: "Games");
        migrationBuilder.DropColumn(name: "ConnectionType", table: "Games");
        migrationBuilder.DropColumn(name: "TargetSystem", table: "Games");
        migrationBuilder.DropColumn(name: "Target", table: "Games");
        migrationBuilder.DropColumn(name: "TargetZone", table: "Games");
        migrationBuilder.DropColumn(name: "TargetStations", table: "Games");
    }
}
