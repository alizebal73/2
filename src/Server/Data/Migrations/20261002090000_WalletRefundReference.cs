using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations
{
    public partial class WalletRefundReference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceTransactionId",
                table: "WalletTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ReferenceTransactionId",
                table: "WalletTransactions",
                column: "ReferenceTransactionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_ReferenceTransactionId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ReferenceTransactionId",
                table: "WalletTransactions");
        }
    }
}
