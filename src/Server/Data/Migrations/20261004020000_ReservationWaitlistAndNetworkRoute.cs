using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class ReservationWaitlistAndNetworkRoute : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "Reservations",
            type: "TEXT",
            maxLength: 20,
            nullable: false,
            defaultValue: "Reservation");

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Reservations",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByUserId",
            table: "Reservations",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NetworkRoute",
            table: "Stations",
            type: "TEXT",
            maxLength: 30,
            nullable: false,
            defaultValue: "internet1");

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_CreatedByUserId",
            table: "Reservations",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_Kind_Status_Priority_CreatedAt",
            table: "Reservations",
            columns: new[] { "Kind", "Status", "Priority", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations",
            columns: new[] { "StationId", "StartAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_Reservations_AppUsers_CreatedByUserId",
            table: "Reservations",
            column: "CreatedByUserId",
            principalTable: "AppUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Reservations_AppUsers_CreatedByUserId",
            table: "Reservations");

        migrationBuilder.DropIndex(
            name: "IX_Reservations_CreatedByUserId",
            table: "Reservations");

        migrationBuilder.DropIndex(
            name: "IX_Reservations_Kind_Status_Priority_CreatedAt",
            table: "Reservations");

        migrationBuilder.DropIndex(
            name: "IX_Reservations_StationId_StartAt",
            table: "Reservations");

        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "Reservations");
        migrationBuilder.DropColumn(name: "Kind", table: "Reservations");
        migrationBuilder.DropColumn(name: "Priority", table: "Reservations");
        migrationBuilder.DropColumn(name: "NetworkRoute", table: "Stations");
    }
}
