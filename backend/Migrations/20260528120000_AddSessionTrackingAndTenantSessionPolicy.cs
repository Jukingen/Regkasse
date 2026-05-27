using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
public partial class AddSessionTrackingAndTenantSessionPolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "device_id",
            table: "auth_sessions",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ip_address",
            table: "auth_sessions",
            type: "character varying(45)",
            maxLength: 45,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "last_activity_at_utc",
            table: "auth_sessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "user_agent",
            table: "auth_sessions",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE auth_sessions SET last_activity_at_utc = created_at_utc WHERE last_activity_at_utc IS NULL;");

        migrationBuilder.AddColumn<bool>(
            name: "keep_cart_after_timeout",
            table: "system_settings",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<int>(
            name: "session_timeout_minutes",
            table: "system_settings",
            type: "integer",
            nullable: false,
            defaultValue: 30);

        migrationBuilder.AddColumn<int>(
            name: "session_warning_before_timeout_minutes",
            table: "system_settings",
            type: "integer",
            nullable: false,
            defaultValue: 1);
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
