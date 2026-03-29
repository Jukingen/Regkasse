using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoreVerificationIsolatedRestoreAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "restore_attempt_executed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "restore_attempt_exit_code",
                table: "restore_verification_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "restore_attempt_passed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restore_attempt_skip_reason",
                table: "restore_verification_runs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restore_target_db_redacted",
                table: "restore_verification_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "restore_attempt_executed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restore_attempt_exit_code",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restore_attempt_passed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restore_attempt_skip_reason",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restore_target_db_redacted",
                table: "restore_verification_runs");
        }
    }
}
