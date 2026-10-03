using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class EventTournamentReady : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                MaxParticipants = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Events", x => x.Id);
                table.ForeignKey(
                    name: "FK_Events_AppUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "EventParticipants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventParticipants", x => x.Id);
                table.ForeignKey(
                    name: "FK_EventParticipants_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EventParticipants_Events_EventId",
                    column: x => x.EventId,
                    principalTable: "Events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Events_CreatedByUserId",
            table: "Events",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Events_StartAt_Status",
            table: "Events",
            columns: new[] { "StartAt", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_EventParticipants_CustomerId",
            table: "EventParticipants",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_EventParticipants_EventId_CustomerId",
            table: "EventParticipants",
            columns: new[] { "EventId", "CustomerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EventParticipants_EventId_Seed",
            table: "EventParticipants",
            columns: new[] { "EventId", "Seed" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EventParticipants");
        migrationBuilder.DropTable(name: "Events");
    }
}
