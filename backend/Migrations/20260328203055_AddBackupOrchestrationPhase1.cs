using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupOrchestrationPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    trigger_source = table.Column<int>(type: "integer", nullable: false),
                    adapter_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<int>(type: "integer", nullable: false),
                    storage_descriptor = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: true),
                    content_hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_artifacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_backup_artifacts_backup_runs_backup_run_id",
                        column: x => x.backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backup_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verifier_source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    completeness_flag = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    details_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_verifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_backup_verifications_backup_runs_backup_run_id",
                        column: x => x.backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backup_artifacts_backup_run_id",
                table: "backup_artifacts",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_idempotency_key",
                table: "backup_runs",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_requested_at",
                table: "backup_runs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_backup_runs_status",
                table: "backup_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_backup_verifications_backup_run_id",
                table: "backup_verifications",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_backup_verifications_started_at",
                table: "backup_verifications",
                column: "started_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_artifacts");

            migrationBuilder.DropTable(
                name: "backup_verifications");

            migrationBuilder.DropTable(
                name: "backup_runs");
        }
    }
}
