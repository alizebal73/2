using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ActiveSessionOwnershipGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite cannot add a foreign key to an existing table directly.
        // Rebuild Sessions once so the new CustomerLogin ownership guard and
        // the previously missing AgentDevice/Game foreign keys become real DB constraints.
        migrationBuilder.Sql(
            """
            PRAGMA foreign_keys = OFF;
            BEGIN;

            CREATE TABLE "Sessions__ActiveSessionOwnershipGuards" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Sessions" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "StationId" TEXT NOT NULL,
                "TariffId" TEXT NULL,
                "AppUserId" TEXT NULL,
                "AgentDeviceId" TEXT NULL,
                "GameId" TEXT NULL,
                "CustomerLoginId" TEXT NULL,
                "StartAt" TEXT NOT NULL,
                "EndAt" TEXT NULL,
                "TotalAmount" decimal(18,2) NOT NULL,
                "State" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "HourlyRateOverride" decimal(18,2) NULL,
                "Persons" INTEGER NOT NULL,
                "PausedAt" TEXT NULL,
                "PausedMinutes" INTEGER NOT NULL,
                "TimeAdjustmentMinutes" INTEGER NOT NULL,
                "PrepaidAmount" decimal(18,2) NOT NULL,
                CONSTRAINT "FK_Sessions_Customers_CustomerId"
                    FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Sessions_Stations_StationId"
                    FOREIGN KEY ("StationId") REFERENCES "Stations" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Sessions_Tariffs_TariffId"
                    FOREIGN KEY ("TariffId") REFERENCES "Tariffs" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Sessions_AppUsers_AppUserId"
                    FOREIGN KEY ("AppUserId") REFERENCES "AppUsers" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Sessions_AgentDevices_AgentDeviceId"
                    FOREIGN KEY ("AgentDeviceId") REFERENCES "AgentDevices" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Sessions_CustomerLogins_CustomerLoginId"
                    FOREIGN KEY ("CustomerLoginId") REFERENCES "CustomerLogins" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Sessions_Games_GameId"
                    FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
            );

            INSERT INTO "Sessions__ActiveSessionOwnershipGuards" (
                "Id",
                "CustomerId",
                "StationId",
                "TariffId",
                "AppUserId",
                "AgentDeviceId",
                "GameId",
                "CustomerLoginId",
                "StartAt",
                "EndAt",
                "TotalAmount",
                "State",
                "Notes",
                "CreatedAt",
                "UpdatedAt",
                "HourlyRateOverride",
                "Persons",
                "PausedAt",
                "PausedMinutes",
                "TimeAdjustmentMinutes",
                "PrepaidAmount")
            SELECT
                "Id",
                "CustomerId",
                "StationId",
                "TariffId",
                "AppUserId",
                "AgentDeviceId",
                "GameId",
                NULL,
                "StartAt",
                "EndAt",
                "TotalAmount",
                "State",
                "Notes",
                "CreatedAt",
                "UpdatedAt",
                "HourlyRateOverride",
                "Persons",
                "PausedAt",
                "PausedMinutes",
                "TimeAdjustmentMinutes",
                "PrepaidAmount"
            FROM "Sessions";

            DROP TABLE "Sessions";

            ALTER TABLE "Sessions__ActiveSessionOwnershipGuards"
                RENAME TO "Sessions";

            CREATE INDEX "IX_Sessions_AppUserId"
                ON "Sessions" ("AppUserId");

            CREATE INDEX "IX_Sessions_CustomerId"
                ON "Sessions" ("CustomerId");

            CREATE INDEX "IX_Sessions_AgentDeviceId"
                ON "Sessions" ("AgentDeviceId");

            CREATE INDEX "IX_Sessions_GameId"
                ON "Sessions" ("GameId");

            CREATE INDEX "IX_Sessions_StationId"
                ON "Sessions" ("StationId");

            CREATE INDEX "IX_Sessions_TariffId"
                ON "Sessions" ("TariffId");

            CREATE UNIQUE INDEX "IX_Sessions_CustomerLoginId_State"
                ON "Sessions" ("CustomerLoginId", "State")
                WHERE "CustomerLoginId" IS NOT NULL AND "State" = 'Active';

            COMMIT;
            PRAGMA foreign_keys = ON;
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            PRAGMA foreign_keys = OFF;
            BEGIN;

            CREATE TABLE "Sessions__BeforeActiveSessionOwnershipGuards" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Sessions" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "StationId" TEXT NOT NULL,
                "TariffId" TEXT NULL,
                "AppUserId" TEXT NULL,
                "AgentDeviceId" TEXT NULL,
                "GameId" TEXT NULL,
                "StartAt" TEXT NOT NULL,
                "EndAt" TEXT NULL,
                "TotalAmount" decimal(18,2) NOT NULL,
                "State" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                "HourlyRateOverride" decimal(18,2) NULL,
                "Persons" INTEGER NOT NULL,
                "PausedAt" TEXT NULL,
                "PausedMinutes" INTEGER NOT NULL,
                "TimeAdjustmentMinutes" INTEGER NOT NULL,
                "PrepaidAmount" decimal(18,2) NOT NULL,
                CONSTRAINT "FK_Sessions_Customers_CustomerId"
                    FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Sessions_Stations_StationId"
                    FOREIGN KEY ("StationId") REFERENCES "Stations" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Sessions_Tariffs_TariffId"
                    FOREIGN KEY ("TariffId") REFERENCES "Tariffs" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Sessions_AppUsers_AppUserId"
                    FOREIGN KEY ("AppUserId") REFERENCES "AppUsers" ("Id") ON DELETE SET NULL
            );

            INSERT INTO "Sessions__BeforeActiveSessionOwnershipGuards" (
                "Id",
                "CustomerId",
                "StationId",
                "TariffId",
                "AppUserId",
                "AgentDeviceId",
                "GameId",
                "StartAt",
                "EndAt",
                "TotalAmount",
                "State",
                "Notes",
                "CreatedAt",
                "UpdatedAt",
                "HourlyRateOverride",
                "Persons",
                "PausedAt",
                "PausedMinutes",
                "TimeAdjustmentMinutes",
                "PrepaidAmount")
            SELECT
                "Id",
                "CustomerId",
                "StationId",
                "TariffId",
                "AppUserId",
                "AgentDeviceId",
                "GameId",
                "StartAt",
                "EndAt",
                "TotalAmount",
                "State",
                "Notes",
                "CreatedAt",
                "UpdatedAt",
                "HourlyRateOverride",
                "Persons",
                "PausedAt",
                "PausedMinutes",
                "TimeAdjustmentMinutes",
                "PrepaidAmount"
            FROM "Sessions";

            DROP TABLE "Sessions";

            ALTER TABLE "Sessions__BeforeActiveSessionOwnershipGuards"
                RENAME TO "Sessions";

            CREATE INDEX "IX_Sessions_AppUserId"
                ON "Sessions" ("AppUserId");

            CREATE INDEX "IX_Sessions_CustomerId"
                ON "Sessions" ("CustomerId");

            CREATE INDEX "IX_Sessions_AgentDeviceId"
                ON "Sessions" ("AgentDeviceId");

            CREATE INDEX "IX_Sessions_GameId"
                ON "Sessions" ("GameId");

            CREATE INDEX "IX_Sessions_StationId"
                ON "Sessions" ("StationId");

            CREATE INDEX "IX_Sessions_TariffId"
                ON "Sessions" ("TariffId");

            COMMIT;
            PRAGMA foreign_keys = ON;
            """,
            suppressTransaction: true);
    }
}
