using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AccountPoolExpiryUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "ExpiresAt",
            table: "AccountPoolEntries",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "TEXT",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "ExpiresAt",
            table: "AccountPoolEntries",
            type: "TEXT",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "TEXT",
            oldNullable: true);
    }
}
