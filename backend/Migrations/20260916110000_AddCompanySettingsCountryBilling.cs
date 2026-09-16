using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Additive country/billing columns on <c>company_settings</c>. The existing <c>country</c> column stays
/// the operating country and is not touched — see <c>docs/COUNTRIES.md</c>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260916110000_AddCompanySettingsCountryBilling")]
public partial class AddCompanySettingsCountryBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "billing_country",
            table: "company_settings",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true,
            defaultValue: null);

        migrationBuilder.AddColumn<string>(
            name: "vat_regime",
            table: "company_settings",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "AT_RKSV_STANDARD");

        migrationBuilder.AddColumn<bool>(
            name: "tax_exempt",
            table: "company_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // PostgreSQL ADD COLUMN ... DEFAULT already backfills existing rows; these guards make the
        // intended state explicit and keep the migration idempotent on partially applied databases.
        migrationBuilder.Sql(
            """
            UPDATE company_settings
            SET vat_regime = 'AT_RKSV_STANDARD'
            WHERE vat_regime IS NULL OR vat_regime = '';
            """);

        migrationBuilder.Sql(
            """
            UPDATE company_settings
            SET tax_exempt = false
            WHERE tax_exempt IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "billing_country",
            table: "company_settings");

        migrationBuilder.DropColumn(
            name: "vat_regime",
            table: "company_settings");

        migrationBuilder.DropColumn(
            name: "tax_exempt",
            table: "company_settings");
    }
}
