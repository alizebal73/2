using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class InventoryTransactionInvoiceReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ReferenceInvoiceId",
            table: "InventoryTransactions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_InventoryTransactions_ReferenceInvoiceId",
            table: "InventoryTransactions",
            column: "ReferenceInvoiceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_InventoryTransactions_ReferenceInvoiceId",
            table: "InventoryTransactions");

        migrationBuilder.DropColumn(
            name: "ReferenceInvoiceId",
            table: "InventoryTransactions");
    }
}