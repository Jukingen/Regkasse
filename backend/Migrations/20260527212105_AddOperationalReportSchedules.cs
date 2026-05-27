using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "keep_cart_after_timeout",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "session_timeout_minutes",
                table: "system_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "session_warning_before_timeout_minutes",
                table: "system_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "auth_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "auth_sessions",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at_utc",
                table: "auth_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "auth_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    dedup_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operational_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    report_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipients_json = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_report_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_notification_configs",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_notification_configs", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "activity_event_reads",
                columns: table => new
                {
                    activity_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_event_reads", x => new { x.activity_event_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_activity_event_reads_activity_events_activity_event_id",
                        column: x => x.activity_event_id,
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_created_at_utc",
                table: "activity_events",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_dedup_key",
                table: "activity_events",
                columns: new[] { "tenant_id", "dedup_key" },
                unique: true,
                filter: "\"dedup_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_tenant_id_severity",
                table: "activity_events",
                columns: new[] { "tenant_id", "severity" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_report_schedules_tenant_id_is_active_next_run_u~",
                table: "operational_report_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_event_reads");

            migrationBuilder.DropTable(
                name: "operational_report_schedules");

            migrationBuilder.DropTable(
                name: "tenant_notification_configs");

            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropColumn(
                name: "keep_cart_after_timeout",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "session_timeout_minutes",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "session_warning_before_timeout_minutes",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "auth_sessions");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "auth_sessions");

            migrationBuilder.DropColumn(
                name: "last_activity_at_utc",
                table: "auth_sessions");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "auth_sessions");
        }
    }
}
