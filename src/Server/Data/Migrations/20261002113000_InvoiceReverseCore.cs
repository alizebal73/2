using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class InvoiceReverseCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ReferenceInvoiceId",
            table: "WalletTransactions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReferenceInvoiceId",
            table: "BenefitTransactions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SessionId",
            table: "Invoices",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "InvoiceReversals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                AppUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                ExternalRefundRequired = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceReversals", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceReversals_AppUsers_AppUserId",
                    column: x => x.AppUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_InvoiceReversals_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WalletTransactions_ReferenceInvoiceId",
            table: "WalletTransactions",
            column: "ReferenceInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_BenefitTransactions_ReferenceInvoiceId",
            table: "BenefitTransactions",
            column: "ReferenceInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices",
            column: "SessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceReversals_AppUserId",
            table: "InvoiceReversals",
            column: "AppUserId");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceReversals_InvoiceId",
            table: "InvoiceReversals",
            column: "InvoiceId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InvoiceReversals");

        migrationBuilder.DropIndex(
            name: "IX_WalletTransactions_ReferenceInvoiceId",
            table: "WalletTransactions");

        migrationBuilder.DropIndex(
            name: "IX_BenefitTransactions_ReferenceInvoiceId",
            table: "BenefitTransactions");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "ReferenceInvoiceId",
            table: "WalletTransactions");

        migrationBuilder.DropColumn(
            name: "ReferenceInvoiceId",
            table: "BenefitTransactions");

        migrationBuilder.DropColumn(
            name: "SessionId",
            table: "Invoices");
    }
}
