using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoreDrillEvidenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "duration_ms",
                table: "restore_verification_runs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_json",
                table: "restore_verification_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "failure_category",
                table: "restore_verification_runs",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "post_restore_continuity_checks_executed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "post_restore_continuity_checks_passed",
                table: "restore_verification_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "restore_drill_reached_stage",
                table: "restore_verification_runs",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_backup_artifact_id",
                table: "restore_verification_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restore_verification_runs_source_backup_artifact_id",
                table: "restore_verification_runs",
                column: "source_backup_artifact_id");

            migrationBuilder.AddForeignKey(
                name: "FK_restore_verification_runs_backup_artifacts_source_backup_ar~",
                table: "restore_verification_runs",
                column: "source_backup_artifact_id",
                principalTable: "backup_artifacts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restore_verification_runs_backup_artifacts_source_backup_ar~",
                table: "restore_verification_runs");

            migrationBuilder.DropIndex(
                name: "IX_restore_verification_runs_source_backup_artifact_id",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "evidence_json",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "failure_category",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "post_restore_continuity_checks_executed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "post_restore_continuity_checks_passed",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "restore_drill_reached_stage",
                table: "restore_verification_runs");

            migrationBuilder.DropColumn(
                name: "source_backup_artifact_id",
                table: "restore_verification_runs");
        }
    }
}
