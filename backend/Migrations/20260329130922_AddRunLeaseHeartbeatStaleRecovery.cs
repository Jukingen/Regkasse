using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRunLeaseHeartbeatStaleRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_heartbeat_at_utc",
                table: "restore_verification_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lease_expires_at_utc",
                table: "restore_verification_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "stale_recovered_at_utc",
                table: "restore_verification_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stale_recovery_reason",
                table: "restore_verification_runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_heartbeat_at_utc",
                table: "backup_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lease_expires_at_utc",
                table: "backup_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "stale_recovered_at_utc",
                table: "backup_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stale_recovery_reason",
                table: "backup_runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_restore_verification_runs_lease_expires_stale_reaper",
                table: "restore_verification_runs",
                column: "lease_expires_at_utc",
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_backup_runs_lease_expires_stale_reaper",
                table: "backup_runs",
                column: "lease_expires_at_utc",
                filter: "status IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_restore_verification_runs_lease_expires_stale_reaper",
                table: "restore_verification_runs");

            migrationBuilder.DropIndex(
                name: "ix_backup_runs_lease_expires_stale_reaper",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "last_heartbeat_at_utc",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "lease_expires_at_utc",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "stale_recovered_at_utc",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "stale_recovery_reason",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "last_heartbeat_at_utc",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "lease_expires_at_utc",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "stale_recovered_at_utc",
                table: "backup_runs");

            migrationBuilder.DropColumn(
                name: "stale_recovery_reason",
                table: "backup_runs");
        }
    }
}
