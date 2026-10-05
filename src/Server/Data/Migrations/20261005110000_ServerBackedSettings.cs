using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ServerBackedSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                ScopeKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                ValueJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppSettings_AppUsers_UpdatedByUserId",
                    column: x => x.UpdatedByUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppSettings_ScopeKey_Key",
            table: "AppSettings",
            columns: new[] { "ScopeKey", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppSettings_UpdatedByUserId",
            table: "AppSettings",
            column: "UpdatedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppSettings");
    }
}
