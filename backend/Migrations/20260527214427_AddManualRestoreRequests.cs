using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddManualRestoreRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manual_restore_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    backup_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_database_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    validation_only = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approval_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    approval_token_expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    requested_by_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    approved_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    result = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    restore_verification_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_restore_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_manual_restore_requests_backup_runs_backup_run_id",
                        column: x => x.backup_run_id,
                        principalTable: "backup_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_restore_requests_restore_verification_runs_restore_v~",
                        column: x => x.restore_verification_run_id,
                        principalTable: "restore_verification_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    theme_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    density_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_page = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    date_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    time_format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    reduced_animations = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_preferences_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_backup_run_id",
                table: "manual_restore_requests",
                column: "backup_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_requested_at",
                table: "manual_restore_requests",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_restore_verification_run_id",
                table: "manual_restore_requests",
                column: "restore_verification_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_manual_restore_requests_status",
                table: "manual_restore_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_user_preferences_user_id",
                table: "user_preferences",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_restore_requests");

            migrationBuilder.DropTable(
                name: "user_preferences");
        }
    }
}
