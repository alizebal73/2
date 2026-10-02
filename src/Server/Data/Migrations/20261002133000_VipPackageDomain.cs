using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class VipPackageDomain : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DailyMinutes",
            table: "VipPackages",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "DiscountPercent",
            table: "VipPackages",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<string>(
            name: "OverflowRule",
            table: "VipPackages",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "half-hourly");

        migrationBuilder.AddColumn<int>(
            name: "TotalMinutes",
            table: "VipPackages",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Tier",
            table: "VipPackages",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "custom");

        migrationBuilder.AddColumn<Guid>(
            name: "VipPackageId",
            table: "Customers",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "VipActivatedAt",
            table: "Customers",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "VipExpiresAt",
            table: "Customers",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_VipPackageId",
            table: "Customers",
            column: "VipPackageId");

        migrationBuilder.AddForeignKey(
            name: "FK_Customers_VipPackages_VipPackageId",
            table: "Customers",
            column: "VipPackageId",
            principalTable: "VipPackages",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Customers_VipPackages_VipPackageId", "Customers");
        migrationBuilder.DropIndex("IX_Customers_VipPackageId", "Customers");

        migrationBuilder.DropColumn("VipPackageId", "Customers");
        migrationBuilder.DropColumn("VipActivatedAt", "Customers");
        migrationBuilder.DropColumn("VipExpiresAt", "Customers");

        migrationBuilder.DropColumn("DailyMinutes", "VipPackages");
        migrationBuilder.DropColumn("DiscountPercent", "VipPackages");
        migrationBuilder.DropColumn("OverflowRule", "VipPackages");
        migrationBuilder.DropColumn("TotalMinutes", "VipPackages");
        migrationBuilder.DropColumn("Tier", "VipPackages");
    }
}
