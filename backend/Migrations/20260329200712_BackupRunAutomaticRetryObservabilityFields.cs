using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class BackupRunAutomaticRetryObservabilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "automatic_retry_last_scheduled_at_utc",
                table: "backup_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "automatic_retry_pending_classified_reason",
                table: "backup_runs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "automatic_retry_last_scheduled_at_utc",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "automatic_retry_pending_classified_reason",
                table: "backup_runs");
        }
    }
}
