using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDevelopmentModeDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "development_mode_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "bypass_license", "bypass_ntp_check", "bypass_tse_check", "enabled", "force_online", "updated_at_utc" },
                values: new object[] { true, true, true, true, true, new DateTime(2026, 6, 11, 12, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "development_mode_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "bypass_license", "bypass_ntp_check", "bypass_tse_check", "enabled", "force_online", "updated_at_utc" },
                values: new object[] { false, false, false, false, false, new DateTime(2026, 5, 14, 12, 0, 0, 0, DateTimeKind.Utc) });
        }
    }
}
