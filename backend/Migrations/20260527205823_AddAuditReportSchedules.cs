using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipients_json = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_report_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_report_schedules_tenant_id_is_active_next_run_utc",
                table: "audit_report_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_report_schedules");
        }
    }
}
