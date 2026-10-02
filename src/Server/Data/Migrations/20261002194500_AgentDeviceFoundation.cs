using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AgentDeviceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentDevices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                DeviceId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                AgentTokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                StationId = table.Column<Guid>(type: "TEXT", nullable: true),
                AgentVersion = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                OsVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                CpuUsagePercent = table.Column<double>(type: "REAL", nullable: true),
                MemoryAvailableBytes = table.Column<long>(type: "INTEGER", nullable: true),
                UptimeSeconds = table.Column<long>(type: "INTEGER", nullable: true),
                LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                ConnectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastIpAddress = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                ConnectionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentDevices", x => x.Id);
                table.ForeignKey(
                    name: "FK_AgentDevices_Stations_StationId",
                    column: x => x.StationId,
                    principalTable: "Stations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentDevices_DeviceId",
            table: "AgentDevices",
            column: "DeviceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AgentDevices_StationId",
            table: "AgentDevices",
            column: "StationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "AgentDevices");
}
