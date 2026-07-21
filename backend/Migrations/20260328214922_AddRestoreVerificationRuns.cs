using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoreVerificationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restore_verification_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    trigger_source = table.Column<int>(type: "integer", nullable: false),
                    source_backup_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dump_relative_descriptor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    pg_restore_list_passed = table.Column<bool>(type: "boolean", nullable: true),
                    pg_restore_list_exit_code = table.Column<int>(type: "integer", nullable: true),
                    pg_restore_list_line_count = table.Column<int>(type: "integer", nullable: true),
                    fiscal_sql_skipped = table.Column<bool>(type: "boolean", nullable: false),
                    fiscal_sql_skip_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fiscal_sql_passed = table.Column<bool>(type: "boolean", nullable: true),
                    fiscal_sql_fail_count = table.Column<int>(type: "integer", nullable: true),
                    fiscal_sql_warn_count = table.Column<int>(type: "integer", nullable: true),
                    integrity_scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    integrity_checks_passed = table.Column<bool>(type: "boolean", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restore_verification_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_restore_verification_runs_backup_runs_source_backup_run_id",
                        column: x => x.source_backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_requested_at",
                table: "restore_verification_runs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_source_backup_run_id",
                table: "restore_verification_runs",
                column: "source_backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_status",
                table: "restore_verification_runs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restore_verification_runs");
        }
    }
}
