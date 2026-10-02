using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class EmployeePayroll : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmployeeProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AppUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                PayType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                OvertimeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                EmploymentStartDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                WorkSchedule = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmployeeProfiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_EmployeeProfiles_AppUsers_AppUserId",
                    column: x => x.AppUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PayrollLedgerEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                EmployeeProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                EmployeePayableDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                OwnerReceivableDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ApprovedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                ReceiptNumber = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PayrollLedgerEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_PayrollLedgerEntries_EmployeeProfiles_EmployeeProfileId",
                    column: x => x.EmployeeProfileId,
                    principalTable: "EmployeeProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmployeeProfiles_AppUserId",
            table: "EmployeeProfiles",
            column: "AppUserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PayrollLedgerEntries_ApprovedByUserId",
            table: "PayrollLedgerEntries",
            column: "ApprovedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollLedgerEntries_CreatedByUserId",
            table: "PayrollLedgerEntries",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PayrollLedgerEntries_EmployeeProfileId_Status_CreatedAt",
            table: "PayrollLedgerEntries",
            columns: new[] { "EmployeeProfileId", "Status", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PayrollLedgerEntries");

        migrationBuilder.DropTable(
            name: "EmployeeProfiles");
    }
}
