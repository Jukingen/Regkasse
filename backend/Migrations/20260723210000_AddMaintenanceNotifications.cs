using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723210000_AddMaintenanceNotifications")]
public partial class AddMaintenanceNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "maintenance_notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                message = table.Column<string>(type: "text", nullable: false),
                scheduled_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                scheduled_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_force_display = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                force_display_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                affected_systems = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_by = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_maintenance_notifications", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "maintenance_notification_acknowledgments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                is_dismissed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_maintenance_notification_acknowledgments", x => x.id);
                table.ForeignKey(
                    name: "FK_maintenance_notification_acknowledgments_notification_id",
                    column: x => x.notification_id,
                    principalTable: "maintenance_notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_maintenance_notifications_status_start",
            table: "maintenance_notifications",
            columns: new[] { "status", "scheduled_start_at" });

        migrationBuilder.CreateIndex(
            name: "idx_maintenance_notifications_end",
            table: "maintenance_notifications",
            column: "scheduled_end_at");

        migrationBuilder.CreateIndex(
            name: "idx_maintenance_acks_notification_user",
            table: "maintenance_notification_acknowledgments",
            columns: new[] { "notification_id", "user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_maintenance_acks_user_id",
            table: "maintenance_notification_acknowledgments",
            column: "user_id");

        migrationBuilder.Sql(
            """
            ALTER TABLE maintenance_notifications
              ADD CONSTRAINT ck_maintenance_notifications_priority
              CHECK (priority >= 1 AND priority <= 5);

            ALTER TABLE maintenance_notifications
              ADD CONSTRAINT ck_maintenance_notifications_status
              CHECK (status IN ('Draft', 'Published', 'InProgress', 'Completed', 'Cancelled'));

            ALTER TABLE maintenance_notifications
              ADD CONSTRAINT ck_maintenance_notifications_schedule
              CHECK (scheduled_end_at > scheduled_start_at);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "maintenance_notification_acknowledgments");
        migrationBuilder.DropTable(name: "maintenance_notifications");
    }
}
