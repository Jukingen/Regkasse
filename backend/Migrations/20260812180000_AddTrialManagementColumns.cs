using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Additive SaaS trial management columns on <c>tenants</c>.
/// <c>trial_status</c> is nullable (no default) so existing non-trial tenants stay unset.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260812180000_AddTrialManagementColumns")]
public partial class AddTrialManagementColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "trial_started_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "trial_ends_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "trial_status",
            table: "tenants",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "trial_reminder_7d_sent",
            table: "tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "trial_reminder_3d_sent",
            table: "tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "trial_reminder_1d_sent",
            table: "tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "trial_converted_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "trial_deleted_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "trial_grace_period_ends_at_utc",
            table: "tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_tenants_trial_status",
            table: "tenants",
            column: "trial_status");

        migrationBuilder.CreateIndex(
            name: "IX_tenants_trial_ends_at",
            table: "tenants",
            column: "trial_ends_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_tenants_trial_ends_at",
            table: "tenants");

        migrationBuilder.DropIndex(
            name: "IX_tenants_trial_status",
            table: "tenants");

        migrationBuilder.DropColumn(name: "trial_grace_period_ends_at_utc", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_deleted_at_utc", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_converted_at_utc", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_reminder_1d_sent", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_reminder_3d_sent", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_reminder_7d_sent", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_status", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_ends_at_utc", table: "tenants");
        migrationBuilder.DropColumn(name: "trial_started_at_utc", table: "tenants");
    }
}
