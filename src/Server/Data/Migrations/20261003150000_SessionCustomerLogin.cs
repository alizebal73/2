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
