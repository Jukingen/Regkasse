#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class SeedDevelopmentModeSettingsHasData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: first migration may already have inserted the singleton row via raw SQL.
            migrationBuilder.Sql(
                """
                INSERT INTO development_mode_settings (
                    id, enabled, bypass_license, bypass_ntp_check, bypass_tse_check,
                    simulate_offline, force_online, valid_days, features, updated_at_utc, updated_by_user_id)
                VALUES (
                    1, false, false, false, false, false, false, 365, '[]'::jsonb, TIMESTAMPTZ '2026-05-14 12:00:00+00', NULL)
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM development_mode_settings WHERE id = 1;");
        }
    }
}
