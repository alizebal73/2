using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class CustomerAccountDebtState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AccountState",
            table: "Invoices",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "PendingPayment");

        migrationBuilder.Sql("UPDATE Invoices SET AccountState = 'Debt' WHERE Status = 'Draft' AND IsCustomerAccount = 1 AND SessionId IS NULL AND TotalAmount > 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AccountState",
            table: "Invoices");
    }
}