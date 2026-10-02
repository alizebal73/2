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

        migrationBuilder.Sql(
            """
            PRAGMA foreign_keys = OFF;

            CREATE TABLE "Customers__VipPackageDomain" (
                "Id" TEXT NOT NULL,
                "FullName" TEXT NOT NULL,
                "Code" TEXT NULL,
                "Username" TEXT NULL,
                "Alias" TEXT NULL,
                "NationalId" TEXT NULL,
                "Phone" TEXT NULL,
                "Email" TEXT NULL,
                "IsVip" INTEGER NOT NULL,
                "VipTier" TEXT NOT NULL DEFAULT 'none',
                "VipPackageId" TEXT NULL,
                "VipActivatedAt" TEXT NULL,
                "VipExpiresAt" TEXT NULL,
                "Balance" decimal(18,2) NOT NULL,
                "FreeMoney" decimal(18,2) NOT NULL DEFAULT 0,
                "FreeTimeMinutes" INTEGER NOT NULL DEFAULT 0,
                "ConcurrentLoginLimit" INTEGER NOT NULL DEFAULT 1,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Customers_VipPackages_VipPackageId"
                    FOREIGN KEY ("VipPackageId") REFERENCES "VipPackages" ("Id") ON DELETE SET NULL
            );

            INSERT INTO "Customers__VipPackageDomain" (
                "Id",
                "FullName",
                "Code",
                "Username",
                "Alias",
                "NationalId",
                "Phone",
                "Email",
                "IsVip",
                "VipTier",
                "VipPackageId",
                "VipActivatedAt",
                "VipExpiresAt",
                "Balance",
                "FreeMoney",
                "FreeTimeMinutes",
                "ConcurrentLoginLimit",
                "Notes",
                "CreatedAt",
                "UpdatedAt"
            )
            SELECT
                "Id",
                "FullName",
                "Code",
                "Username",
                "Alias",
                "NationalId",
                "Phone",
                "Email",
                "IsVip",
                "VipTier",
                "VipPackageId",
                "VipActivatedAt",
                "VipExpiresAt",
                "Balance",
                "FreeMoney",
                "FreeTimeMinutes",
                "ConcurrentLoginLimit",
                "Notes",
                "CreatedAt",
                "UpdatedAt"
            FROM "Customers";

            DROP TABLE "Customers";
            ALTER TABLE "Customers__VipPackageDomain" RENAME TO "Customers";

            CREATE UNIQUE INDEX "IX_Customers_Code" ON "Customers" ("Code");
            CREATE UNIQUE INDEX "IX_Customers_Email" ON "Customers" ("Email");
            CREATE UNIQUE INDEX "IX_Customers_NationalId" ON "Customers" ("NationalId");
            CREATE UNIQUE INDEX "IX_Customers_Phone" ON "Customers" ("Phone");
            CREATE UNIQUE INDEX "IX_Customers_Username" ON "Customers" ("Username");
            CREATE INDEX "IX_Customers_VipPackageId" ON "Customers" ("VipPackageId");

            PRAGMA foreign_keys = ON;
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            PRAGMA foreign_keys = OFF;

            CREATE TABLE "Customers__VipPackageDomainDown" (
                "Id" TEXT NOT NULL,
                "FullName" TEXT NOT NULL,
                "Code" TEXT NULL,
                "Username" TEXT NULL,
                "Alias" TEXT NULL,
                "NationalId" TEXT NULL,
                "Phone" TEXT NULL,
                "Email" TEXT NULL,
                "IsVip" INTEGER NOT NULL,
                "VipTier" TEXT NOT NULL DEFAULT 'none',
                "Balance" decimal(18,2) NOT NULL,
                "FreeMoney" decimal(18,2) NOT NULL DEFAULT 0,
                "FreeTimeMinutes" INTEGER NOT NULL DEFAULT 0,
                "ConcurrentLoginLimit" INTEGER NOT NULL DEFAULT 1,
                "Notes" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NULL,
                CONSTRAINT "PK_Customers" PRIMARY KEY ("Id")
            );

            INSERT INTO "Customers__VipPackageDomainDown" (
                "Id",
                "FullName",
                "Code",
                "Username",
                "Alias",
                "NationalId",
                "Phone",
                "Email",
                "IsVip",
                "VipTier",
                "Balance",
                "FreeMoney",
                "FreeTimeMinutes",
                "ConcurrentLoginLimit",
                "Notes",
                "CreatedAt",
                "UpdatedAt"
            )
            SELECT
                "Id",
                "FullName",
                "Code",
                "Username",
                "Alias",
                "NationalId",
                "Phone",
                "Email",
                "IsVip",
                "VipTier",
                "Balance",
                "FreeMoney",
                "FreeTimeMinutes",
                "ConcurrentLoginLimit",
                "Notes",
                "CreatedAt",
                "UpdatedAt"
            FROM "Customers";

            DROP TABLE "Customers";
            ALTER TABLE "Customers__VipPackageDomainDown" RENAME TO "Customers";

            CREATE UNIQUE INDEX "IX_Customers_Code" ON "Customers" ("Code");
            CREATE UNIQUE INDEX "IX_Customers_Email" ON "Customers" ("Email");
            CREATE UNIQUE INDEX "IX_Customers_NationalId" ON "Customers" ("NationalId");
            CREATE UNIQUE INDEX "IX_Customers_Phone" ON "Customers" ("Phone");
            CREATE UNIQUE INDEX "IX_Customers_Username" ON "Customers" ("Username");

            PRAGMA foreign_keys = ON;
            """,
            suppressTransaction: true);

        migrationBuilder.DropColumn(
            name: "DailyMinutes",
            table: "VipPackages");

        migrationBuilder.DropColumn(
            name: "DiscountPercent",
            table: "VipPackages");

        migrationBuilder.DropColumn(
            name: "OverflowRule",
            table: "VipPackages");

        migrationBuilder.DropColumn(
            name: "TotalMinutes",
            table: "VipPackages");

        migrationBuilder.DropColumn(
            name: "Tier",
            table: "VipPackages");
    }
}
