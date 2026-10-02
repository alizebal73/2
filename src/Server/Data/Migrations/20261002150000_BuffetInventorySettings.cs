using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class BuffetInventorySettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "MinimumStock",
            table: "Products",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Unit",
            table: "Products",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "عدد");

        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "InventoryTransactions",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "Adjustment");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MinimumStock", table: "Products");
        migrationBuilder.DropColumn(name: "Unit", table: "Products");
        migrationBuilder.DropColumn(name: "Kind", table: "InventoryTransactions");
    }
}
