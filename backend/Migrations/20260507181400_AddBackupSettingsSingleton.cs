using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>Mirrors defaults in <see cref="KasseAPI_Final.Models.Backup.BackupSettings"/>.</summary>
    internal static class BackupSettingsSingletonMigrationDefaults
    {
        internal const int Id = KasseAPI_Final.Models.Backup.BackupSettings.SingletonId;
        internal const bool Enabled = false;
        internal const string ScheduleCron = KasseAPI_Final.Models.Backup.BackupSettings.DefaultScheduleCron;
        internal const int RetentionDays = KasseAPI_Final.Models.Backup.BackupSettings.DefaultRetentionDays;
        internal static readonly DateTime UpdatedAtUtc = new(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public partial class AddBackupSettingsSingleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_cron = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_settings", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "backup_settings",
                columns: new[]
                {
                    "id", "enabled", "schedule_cron", "retention_days", "last_run_at", "next_run_at", "updated_at_utc"
                },
                values: new object[]
                {
                    BackupSettingsSingletonMigrationDefaults.Id,
                    BackupSettingsSingletonMigrationDefaults.Enabled,
                    BackupSettingsSingletonMigrationDefaults.ScheduleCron,
                    BackupSettingsSingletonMigrationDefaults.RetentionDays,
                    null,
                    null,
                    BackupSettingsSingletonMigrationDefaults.UpdatedAtUtc
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_settings");
        }
    }
}
