using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class CustomerOpenAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCustomerAccount",
            table: "Invoices",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "SessionId",
            table: "InvoiceItems",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Invoices_SessionId;");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CustomerId",
            table: "Invoices",
            column: "CustomerId",
            unique: true,
            filter: "Status = 'Draft' AND IsCustomerAccount = 1");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceItems_SessionId",
            table: "InvoiceItems",
            column: "SessionId");

        migrationBuilder.AddForeignKey(
            name: "FK_InvoiceItems_Sessions_SessionId",
            table: "InvoiceItems",
            column: "SessionId",
            principalTable: "Sessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_InvoiceItems_Sessions_SessionId",
            table: "InvoiceItems");

        migrationBuilder.DropIndex(name: "IX_InvoiceItems_SessionId", table: "InvoiceItems");
        migrationBuilder.DropIndex(name: "IX_Invoices_CustomerId", table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_SessionId", table: "Invoices");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices",
            column: "SessionId",
            unique: true);

        migrationBuilder.DropColumn(name: "SessionId", table: "InvoiceItems");
        migrationBuilder.DropColumn(name: "IsCustomerAccount", table: "Invoices");
    }
}
