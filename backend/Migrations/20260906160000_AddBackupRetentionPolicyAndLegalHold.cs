using System;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Legal 7-year retention policy singleton, backup legal hold, and cold-archive locators.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260906160000_AddBackupRetentionPolicyAndLegalHold")]
public partial class AddBackupRetentionPolicyAndLegalHold : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "legal_hold",
            table: "backup_runs",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "legal_hold_until_utc",
            table: "backup_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "legal_hold_reason",
            table: "backup_runs",
            type: "character varying(400)",
            maxLength: 400,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "legal_hold_set_by_user_id",
            table: "backup_runs",
            type: "character varying(450)",
            maxLength: 450,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "legal_hold_set_at_utc",
            table: "backup_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_backup_runs_legal_hold",
            table: "backup_runs",
            column: "legal_hold",
            filter: "legal_hold = TRUE");

        migrationBuilder.AddColumn<string>(
            name: "cloud_locator",
            table: "backup_artifacts",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "cloud_provider",
            table: "backup_artifacts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "compressed_byte_size",
            table: "backup_artifacts",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "deduplicated_from_artifact_id",
            table: "backup_artifacts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "moved_to_cold_at_utc",
            table: "backup_artifacts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "immutable_until_utc",
            table: "backup_artifacts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_backup_artifacts_content_hash",
            table: "backup_artifacts",
            column: "content_hash_sha256",
            filter: "content_hash_sha256 IS NOT NULL");

        migrationBuilder.CreateTable(
            name: "backup_retention_policy_settings",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                hot_retention_days = table.Column<int>(type: "integer", nullable: false),
                warm_retention_days = table.Column<int>(type: "integer", nullable: false),
                cold_retention_years = table.Column<int>(type: "integer", nullable: false),
                cold_storage_enabled = table.Column<bool>(type: "boolean", nullable: false),
                legal_retention_enforced = table.Column<bool>(type: "boolean", nullable: false),
                cloud_provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_by_user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_backup_retention_policy_settings", x => x.id);
            });

        migrationBuilder.InsertData(
            table: "backup_retention_policy_settings",
            columns: new[]
            {
                "id",
                "hot_retention_days",
                "warm_retention_days",
                "cold_retention_years",
                "cold_storage_enabled",
                "legal_retention_enforced",
                "cloud_provider",
                "updated_at_utc",
                "updated_by_user_id"
            },
            values: new object[]
            {
                1,
                30,
                90,
                7,
                false,
                true,
                "Filesystem",
                new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc),
                null
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "backup_retention_policy_settings");
        migrationBuilder.DropIndex(name: "ix_backup_artifacts_content_hash", table: "backup_artifacts");
        migrationBuilder.DropIndex(name: "ix_backup_runs_legal_hold", table: "backup_runs");
        migrationBuilder.DropColumn(name: "cloud_locator", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "cloud_provider", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "compressed_byte_size", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "deduplicated_from_artifact_id", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "moved_to_cold_at_utc", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "immutable_until_utc", table: "backup_artifacts");
        migrationBuilder.DropColumn(name: "legal_hold", table: "backup_runs");
        migrationBuilder.DropColumn(name: "legal_hold_until_utc", table: "backup_runs");
        migrationBuilder.DropColumn(name: "legal_hold_reason", table: "backup_runs");
        migrationBuilder.DropColumn(name: "legal_hold_set_by_user_id", table: "backup_runs");
        migrationBuilder.DropColumn(name: "legal_hold_set_at_utc", table: "backup_runs");
    }
}
