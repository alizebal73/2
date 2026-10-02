using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AgentCommandFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentCommands",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AgentDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                RequestedByAppUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                CommandType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Succeeded = table.Column<bool>(type: "INTEGER", nullable: true),
                ResultMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                AgentConnectionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentCommands", x => x.Id);
                table.ForeignKey(
                    name: "FK_AgentCommands_AgentDevices_AgentDeviceId",
                    column: x => x.AgentDeviceId,
                    principalTable: "AgentDevices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentCommands_AgentDeviceId",
            table: "AgentCommands",
            column: "AgentDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_AgentCommands_Status",
            table: "AgentCommands",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "AgentCommands");
}
