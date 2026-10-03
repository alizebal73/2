using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class Stage12SessionLeaseCredential : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecretCiphertext",
            table: "AccountPoolEntries",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CredentialAccessExpiresAt",
            table: "AccountLeases",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AgentDeviceId",
            table: "Sessions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "GameId",
            table: "Sessions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_AgentDeviceId",
            table: "Sessions",
            column: "AgentDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_GameId",
            table: "Sessions",
            column: "GameId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountLeases_CredentialAccessExpiresAt",
            table: "AccountLeases",
            column: "CredentialAccessExpiresAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Sessions_AgentDeviceId", table: "Sessions");
        migrationBuilder.DropIndex(name: "IX_Sessions_GameId", table: "Sessions");
        migrationBuilder.DropIndex(name: "IX_AccountLeases_CredentialAccessExpiresAt", table: "AccountLeases");

        migrationBuilder.DropColumn(name: "SecretCiphertext", table: "AccountPoolEntries");
        migrationBuilder.DropColumn(name: "CredentialAccessExpiresAt", table: "AccountLeases");
        migrationBuilder.DropColumn(name: "AgentDeviceId", table: "Sessions");
        migrationBuilder.DropColumn(name: "GameId", table: "Sessions");
    }
}
