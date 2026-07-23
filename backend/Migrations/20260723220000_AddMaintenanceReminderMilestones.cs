using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723220000_AddMaintenanceReminderMilestones")]
public partial class AddMaintenanceReminderMilestones : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "reminder_7d_sent_at",
            table: "maintenance_notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "reminder_3d_sent_at",
            table: "maintenance_notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "reminder_24h_sent_at",
            table: "maintenance_notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "reminder_1h_sent_at",
            table: "maintenance_notifications",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "reminder_7d_sent_at", table: "maintenance_notifications");
        migrationBuilder.DropColumn(name: "reminder_3d_sent_at", table: "maintenance_notifications");
        migrationBuilder.DropColumn(name: "reminder_24h_sent_at", table: "maintenance_notifications");
        migrationBuilder.DropColumn(name: "reminder_1h_sent_at", table: "maintenance_notifications");
    }
}
