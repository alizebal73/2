using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class SeparateCustomerPendingAndDebtAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Invoices_CustomerId",
            table: "Invoices");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CustomerId_AccountState",
            table: "Invoices",
            columns: new[] { "CustomerId", "AccountState" },
            unique: true,
            filter: "Status = 'Draft' AND IsCustomerAccount = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Invoices_CustomerId_AccountState",
            table: "Invoices");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CustomerId",
            table: "Invoices",
            column: "CustomerId",
            unique: true,
            filter: "Status = 'Draft' AND IsCustomerAccount = 1");
    }
}
