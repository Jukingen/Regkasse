using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupRunAutomaticRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "automatic_retry_count",
                table: "backup_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_recorded_terminal_failure_code",
                table: "backup_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_retry_at_utc",
                table: "backup_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_next_retry_at",
                table: "backup_runs",
                column: "next_retry_at_utc",
                filter: "next_retry_at_utc IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_backup_runs_next_retry_at",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "automatic_retry_count",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "last_recorded_terminal_failure_code",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "next_retry_at_utc",
                table: "backup_runs");
        }
    }
}
