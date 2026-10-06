using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class BuffetWarehouseShowcaseFlow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ShowcaseStockQuantity",
            table: "Products",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql(
            "UPDATE Products SET ShowcaseStockQuantity = StockQuantity, StockQuantity = 0;");

        migrationBuilder.AddColumn<string>(
            name: "StockArea",
            table: "InventoryTransactions",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "Warehouse");

        migrationBuilder.AlterColumn<string>(
            name: "Kind",
            table: "InventoryTransactions",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 20);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Kind",
            table: "InventoryTransactions",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 30);

        migrationBuilder.DropColumn(
            name: "StockArea",
            table: "InventoryTransactions");

        migrationBuilder.DropColumn(
            name: "ShowcaseStockQuantity",
            table: "Products");
    }
}
