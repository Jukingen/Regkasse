using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDepExportHistoryAndSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dep_export_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exported_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    signature_count = table.Column<int>(type: "integer", nullable: false),
                    group_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    include_special_receipts = table.Column<bool>(type: "boolean", nullable: false),
                    include_daily_closings = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dep_export_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    day_of_month = table.Column<int>(type: "integer", nullable: false),
                    time_of_day = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    recipient_emails = table.Column<string>(type: "text", nullable: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dep_export_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_schedule_id",
                table: "dep_export_history",
                column: "schedule_id",
                filter: "\"schedule_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_history_tenant_id_cash_register_id_exported_at",
                table: "dep_export_history",
                columns: new[] { "tenant_id", "cash_register_id", "exported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_schedules_cash_register_id",
                table: "dep_export_schedules",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_dep_export_schedules_tenant_id_is_active_next_run_at",
                table: "dep_export_schedules",
                columns: new[] { "tenant_id", "is_active", "next_run_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dep_export_history");

            migrationBuilder.DropTable(
                name: "dep_export_schedules");
        }
    }
}
