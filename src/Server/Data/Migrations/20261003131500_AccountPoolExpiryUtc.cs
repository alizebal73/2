using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class AccountPoolExpiryUtc : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite persists DateTime/DateTimeOffset as TEXT. Changing the CLR model
        // does not require a physical column-type change. Normalize legacy values
        // to UTC text instead of issuing an unsupported ALTER COLUMN operation.
        migrationBuilder.Sql(
            """
            UPDATE "AccountPoolEntries"
            SET "ExpiresAt" = CASE
                WHEN "ExpiresAt" IS NULL THEN NULL
                ELSE strftime('%Y-%m-%d %H:%M:%f', "ExpiresAt")
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the legacy UTC-offset textual representation when rolling back.
        migrationBuilder.Sql(
            """
            UPDATE "AccountPoolEntries"
            SET "ExpiresAt" = CASE
                WHEN "ExpiresAt" IS NULL THEN NULL
                ELSE strftime('%Y-%m-%d %H:%M:%f+00:00', "ExpiresAt")
            END;
            """);
    }
}
