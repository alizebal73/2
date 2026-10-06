using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class InvoicePaymentSessionAllocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SessionId",
            table: "InvoicePayments",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_InvoicePayments_SessionId",
            table: "InvoicePayments",
            column: "SessionId");

        migrationBuilder.AddForeignKey(
            name: "FK_InvoicePayments_Sessions_SessionId",
            table: "InvoicePayments",
            column: "SessionId",
            principalTable: "Sessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_InvoicePayments_Sessions_SessionId",
            table: "InvoicePayments");

        migrationBuilder.DropIndex(
            name: "IX_InvoicePayments_SessionId",
            table: "InvoicePayments");

        migrationBuilder.DropColumn(
            name: "SessionId",
            table: "InvoicePayments");
    }
}
