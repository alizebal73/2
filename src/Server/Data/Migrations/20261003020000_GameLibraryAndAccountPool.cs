using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class GameLibraryAndAccountPool : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Version", table: "Games", type: "TEXT", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Launcher", table: "Games", type: "TEXT", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>(name: "InstallPath", table: "Games", type: "TEXT", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ExecutablePath", table: "Games", type: "TEXT", maxLength: 260, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LaunchArguments", table: "Games", type: "TEXT", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ConnectionType", table: "Games", type: "TEXT", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetSystem", table: "Games", type: "TEXT", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetZone", table: "Games", type: "TEXT", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetScope", table: "Games", type: "TEXT", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TargetStations", table: "Games", type: "TEXT", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProcessNames", table: "Games", type: "TEXT", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CoverPath", table: "Games", type: "TEXT", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "TrailerPath", table: "Games", type: "TEXT", maxLength: 500, nullable: true);

        migrationBuilder.CreateTable(
            name: "GameAccountPoolEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AccountName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Platform = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Launcher = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                Login = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                PasswordHash = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Owner = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                GuardStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GameAccountPoolEntries", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GameAccountAllowedGames",
            columns: table => new
            {
                GameAccountPoolEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                GameId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameAccountAllowedGames", x => new { x.GameAccountPoolEntryId, x.GameId });
                table.ForeignKey("FK_GameAccountAllowedGames_GameAccountPoolEntries_GameAccountPoolEntryId", x => x.GameAccountPoolEntryId, "GameAccountPoolEntries", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_GameAccountAllowedGames_Games_GameId", x => x.GameId, "Games", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GameAccountLeases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                GameAccountPoolEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgentDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                LeasedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReleasedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                ReleaseReason = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameAccountLeases", x => x.Id);
                table.ForeignKey("FK_GameAccountLeases_AgentDevices_AgentDeviceId", x => x.AgentDeviceId, "AgentDevices", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_GameAccountLeases_Customers_CustomerId", x => x.CustomerId, "Customers", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_GameAccountLeases_GameAccountPoolEntries_GameAccountPoolEntryId", x => x.GameAccountPoolEntryId, "GameAccountPoolEntries", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_GameAccountLeases_Games_GameId", x => x.GameId, "Games", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_GameAccountAllowedGames_GameId", table: "GameAccountAllowedGames", column: "GameId");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_AgentDeviceId", table: "GameAccountLeases", column: "AgentDeviceId");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_CustomerId", table: "GameAccountLeases", column: "CustomerId");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_GameAccountPoolEntryId", table: "GameAccountLeases", column: "GameAccountPoolEntryId");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_GameAccountPoolEntryId_Active", table: "GameAccountLeases", column: "GameAccountPoolEntryId", unique: true, filter: "ReleasedAt IS NULL");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_GameId", table: "GameAccountLeases", column: "GameId");
        migrationBuilder.CreateIndex(name: "IX_GameAccountLeases_Status", table: "GameAccountLeases", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_GameAccountPoolEntries_AccountName", table: "GameAccountPoolEntries", column: "AccountName", unique: true);
        migrationBuilder.CreateIndex(name: "IX_GameAccountPoolEntries_ExpiresAt", table: "GameAccountPoolEntries", column: "ExpiresAt");
        migrationBuilder.CreateIndex(name: "IX_GameAccountPoolEntries_Status_IsActive", table: "GameAccountPoolEntries", columns: new[] { "Status", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GameAccountLeases");
        migrationBuilder.DropTable(name: "GameAccountAllowedGames");
        migrationBuilder.DropTable(name: "GameAccountPoolEntries");
        migrationBuilder.DropColumn(name: "Version", table: "Games");
        migrationBuilder.DropColumn(name: "Launcher", table: "Games");
        migrationBuilder.DropColumn(name: "InstallPath", table: "Games");
        migrationBuilder.DropColumn(name: "ExecutablePath", table: "Games");
        migrationBuilder.DropColumn(name: "LaunchArguments", table: "Games");
        migrationBuilder.DropColumn(name: "ConnectionType", table: "Games");
        migrationBuilder.DropColumn(name: "TargetSystem", table: "Games");
        migrationBuilder.DropColumn(name: "TargetZone", table: "Games");
        migrationBuilder.DropColumn(name: "TargetScope", table: "Games");
        migrationBuilder.DropColumn(name: "TargetStations", table: "Games");
        migrationBuilder.DropColumn(name: "ProcessNames", table: "Games");
        migrationBuilder.DropColumn(name: "CoverPath", table: "Games");
        migrationBuilder.DropColumn(name: "TrailerPath", table: "Games");
    }
}
