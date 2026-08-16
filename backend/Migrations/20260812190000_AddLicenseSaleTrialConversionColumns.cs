using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Tracks license sales that originated from SaaS trial conversion.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260812190000_AddLicenseSaleTrialConversionColumns")]
public partial class AddLicenseSaleTrialConversionColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "converted_from_trial",
            table: "license_sales",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "remaining_trial_days_added",
            table: "license_sales",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "trial_converted_at_utc",
            table: "license_sales",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "trial_converted_at_utc", table: "license_sales");
        migrationBuilder.DropColumn(name: "remaining_trial_days_added", table: "license_sales");
        migrationBuilder.DropColumn(name: "converted_from_trial", table: "license_sales");
    }
}
