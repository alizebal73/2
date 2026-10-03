using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class SessionCustomerLogin : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CustomerLoginId",
            table: "Sessions",
            type: "TEXT",
            nullable: true);

        // Preserve the CustomerLogin -> Session owner for legacy active sessions.
        // Only sessions with exactly one active login matching the same Customer and Agent
        // are backfilled; ambiguous historical data remains nullable for manual recovery.
        migrationBuilder.Sql(
            """
            UPDATE "Sessions"
            SET "CustomerLoginId" = (
                SELECT cl."Id"
                FROM "CustomerLogins" cl
                INNER JOIN "AgentDevices" ad
                    ON ad."DeviceId" = cl."ClientKey"
                WHERE cl."CustomerId" = "Sessions"."CustomerId"
                  AND ad."Id" = "Sessions"."AgentDeviceId"
                  AND cl."IsActive" = 1
                  AND "Sessions"."State" = 'Active'
                  AND (
                      SELECT COUNT(*)
                      FROM "CustomerLogins" cl2
                      INNER JOIN "AgentDevices" ad2
                          ON ad2."DeviceId" = cl2."ClientKey"
                      WHERE cl2."CustomerId" = "Sessions"."CustomerId"
                        AND ad2."Id" = "Sessions"."AgentDeviceId"
                        AND cl2."IsActive" = 1
                  ) = 1
                LIMIT 1
            )
            WHERE "Sessions"."CustomerLoginId" IS NULL
              AND "Sessions"."State" = 'Active'
              AND "Sessions"."AgentDeviceId" IS NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions",
            column: "CustomerLoginId");

        migrationBuilder.AddForeignKey(
            name: "FK_Sessions_CustomerLogins_CustomerLoginId",
            table: "Sessions",
            column: "CustomerLoginId",
            principalTable: "CustomerLogins",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Sessions_CustomerLogins_CustomerLoginId",
            table: "Sessions");

        migrationBuilder.DropIndex(
            name: "IX_Sessions_CustomerLoginId",
            table: "Sessions");

        migrationBuilder.DropColumn(
            name: "CustomerLoginId",
            table: "Sessions");
    }
}
