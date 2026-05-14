using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddDevelopmentModeSettingsSingleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "development_mode_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_license = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_ntp_check = table.Column<bool>(type: "boolean", nullable: false),
                    bypass_tse_check = table.Column<bool>(type: "boolean", nullable: false),
                    simulate_offline = table.Column<bool>(type: "boolean", nullable: false),
                    force_online = table.Column<bool>(type: "boolean", nullable: false),
                    valid_days = table.Column<int>(type: "integer", nullable: false),
                    features = table.Column<string[]>(type: "jsonb", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_development_mode_settings", x => x.id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO development_mode_settings (
                    id, enabled, bypass_license, bypass_ntp_check, bypass_tse_check,
                    simulate_offline, force_online, valid_days, features, updated_at_utc, updated_by_user_id)
                VALUES (
                    1, false, false, false, false, false, false, 365, '[]'::jsonb, TIMESTAMPTZ '2026-05-14 12:00:00+00', NULL);
                """);

            migrationBuilder.Sql(
                "ALTER TABLE development_mode_settings ADD CONSTRAINT chk_development_mode_settings_singleton CHECK (id = 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE development_mode_settings DROP CONSTRAINT IF EXISTS chk_development_mode_settings_singleton;");

            migrationBuilder.DropTable(
                name: "development_mode_settings");
        }
    }
}
