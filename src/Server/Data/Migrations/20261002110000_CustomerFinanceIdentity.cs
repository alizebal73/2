using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class CustomerFinanceIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Code",
            table: "Customers",
            type: "TEXT",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Username",
            table: "Customers",
            type: "TEXT",
            maxLength: 60,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Code",
            table: "Customers",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Username",
            table: "Customers",
            column: "Username",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_Code",
            table: "Customers");

        migrationBuilder.DropIndex(
            name: "IX_Customers_Username",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "Code",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "Username",
            table: "Customers");
    }
}
