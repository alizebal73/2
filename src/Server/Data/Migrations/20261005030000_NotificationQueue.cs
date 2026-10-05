using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class NotificationQueue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AppUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Detail = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                EntityName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                EntityId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_AppUserId_IsRead_CreatedAt",
            table: "Notifications",
            columns: new[] { "AppUserId", "IsRead", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");
    }
}
