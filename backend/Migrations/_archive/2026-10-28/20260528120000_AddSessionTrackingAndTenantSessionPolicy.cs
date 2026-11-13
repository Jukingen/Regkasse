using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
public partial class AddSessionTrackingAndTenantSessionPolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // These columns are also added by 20260527212105_AddOperationalReportSchedules.
        // A fresh database applies both, so the second add must be a no-op when the column exists.
        // Databases that already applied this migration do not run Up again.
        migrationBuilder.Sql(
            """
            ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS device_id character varying(200);
            ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS ip_address character varying(45);
            ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS last_activity_at_utc timestamp with time zone;
            ALTER TABLE auth_sessions ADD COLUMN IF NOT EXISTS user_agent character varying(500);
            UPDATE auth_sessions SET last_activity_at_utc = created_at_utc WHERE last_activity_at_utc IS NULL;
            ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS keep_cart_after_timeout boolean NOT NULL DEFAULT true;
            ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS session_timeout_minutes integer NOT NULL DEFAULT 30;
            ALTER TABLE system_settings ADD COLUMN IF NOT EXISTS session_warning_before_timeout_minutes integer NOT NULL DEFAULT 1;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "device_id", table: "auth_sessions");
        migrationBuilder.DropColumn(name: "ip_address", table: "auth_sessions");
        migrationBuilder.DropColumn(name: "last_activity_at_utc", table: "auth_sessions");
        migrationBuilder.DropColumn(name: "user_agent", table: "auth_sessions");
        migrationBuilder.DropColumn(name: "keep_cart_after_timeout", table: "system_settings");
        migrationBuilder.DropColumn(name: "session_timeout_minutes", table: "system_settings");
        migrationBuilder.DropColumn(name: "session_warning_before_timeout_minutes", table: "system_settings");
    }
}
