using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class UserAuthorizationAndApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppUserSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AppUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppUserSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppUserSessions_AppUsers_AppUserId",
                    column: x => x.AppUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApprovalRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                EntityName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                EntityId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                DecidedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                DecisionNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                DecidedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_AppUsers_DecidedByUserId",
                    column: x => x.DecidedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ApprovalRequests_AppUsers_RequestedByUserId",
                    column: x => x.RequestedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppUserSessions_AppUserId",
            table: "AppUserSessions",
            column: "AppUserId");

        migrationBuilder.CreateIndex(
            name: "IX_AppUserSessions_TokenHash",
            table: "AppUserSessions",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_DecidedByUserId",
            table: "ApprovalRequests",
            column: "DecidedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_RequestedByUserId",
            table: "ApprovalRequests",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalRequests_Status_CreatedAt",
            table: "ApprovalRequests",
            columns: new[] { "Status", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApprovalRequests");

        migrationBuilder.DropTable(
            name: "AppUserSessions");
    }
}
