using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class OneOpenShift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Shifts_CloseAt",
            table: "Shifts",
            column: "CloseAt",
            unique: true,
            filter: "\"CloseAt\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Shifts_CloseAt",
            table: "Shifts");
    }
}
