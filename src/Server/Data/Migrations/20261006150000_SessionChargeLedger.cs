using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class SessionChargeLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SessionCharges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                AppUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionCharges", x => x.Id);
                table.ForeignKey(
                    name: "FK_SessionCharges_AppUsers_AppUserId",
                    column: x => x.AppUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_SessionCharges_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SessionCharges_Sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "Sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SessionCharges_AppUserId",
            table: "SessionCharges",
            column: "AppUserId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionCharges_InvoiceId",
            table: "SessionCharges",
            column: "InvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_SessionCharges_SessionId_CreatedAt",
            table: "SessionCharges",
            columns: new[] { "SessionId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SessionCharges");
    }
}
