using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <inheritdoc />
    public partial class AddNtpAdminSettingsAndCashRegisterDriftMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_server_time_drift_at_utc",
                table: "cash_registers",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "last_server_time_offset_seconds",
                table: "cash_registers",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ntp_admin_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    auto_sync_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_allowed_offset_seconds = table.Column<int>(type: "integer", nullable: false),
                    critical_offset_seconds = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntp_admin_settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ntp_admin_settings");

            migrationBuilder.DropColumn(
                name: "last_server_time_drift_at_utc",
                table: "cash_registers");

            migrationBuilder.DropColumn(
                name: "last_server_time_offset_seconds",
                table: "cash_registers");
        }
    }
}
