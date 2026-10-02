using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class CustomerIdentityVipTier : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Alias",
            table: "Customers",
            type: "TEXT",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NationalId",
            table: "Customers",
            type: "TEXT",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VipTier",
            table: "Customers",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "none");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_NationalId",
            table: "Customers",
            column: "NationalId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_NationalId",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "Alias",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "NationalId",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "VipTier",
            table: "Customers");
    }
}
