using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AccountPoolExpiryUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite stores this value as TEXT. Keep the physical column stable and
        // normalize legacy DateTimeOffset text into UTC DateTime-compatible text.
        migrationBuilder.Sql(
            "UPDATE AccountPoolEntries " +
            "SET ExpiresAt = strftime('%Y-%m-%d %H:%M:%f', ExpiresAt) " +
            "WHERE ExpiresAt IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Physical SQLite storage remains TEXT; no schema reversal is required.
    }
}
