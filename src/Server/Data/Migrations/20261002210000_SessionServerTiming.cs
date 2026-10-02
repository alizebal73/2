using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class SessionServerTiming : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PausedAt",
            table: "Sessions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PausedMinutes",
            table: "Sessions",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "TimeAdjustmentMinutes",
            table: "Sessions",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "PrepaidAmount",
            table: "Sessions",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PausedAt", table: "Sessions");
        migrationBuilder.DropColumn(name: "PausedMinutes", table: "Sessions");
        migrationBuilder.DropColumn(name: "TimeAdjustmentMinutes", table: "Sessions");
        migrationBuilder.DropColumn(name: "PrepaidAmount", table: "Sessions");
    }
}
