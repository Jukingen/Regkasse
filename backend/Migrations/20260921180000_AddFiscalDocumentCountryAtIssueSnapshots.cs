using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Additive issue-time country/regime snapshots on invoices, receipts, and payment_details.
/// Existing rows stay null (legacy). Country change must not rewrite these columns —
/// see <c>docs/COUNTRIES.md</c> § Historical Invoice Preservation.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260921180000_AddFiscalDocumentCountryAtIssueSnapshots")]
public partial class AddFiscalDocumentCountryAtIssueSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddSnapshotColumns(migrationBuilder, "invoices");
        AddSnapshotColumns(migrationBuilder, "receipts");
        AddSnapshotColumns(migrationBuilder, "payment_details");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropSnapshotColumns(migrationBuilder, "invoices");
        DropSnapshotColumns(migrationBuilder, "receipts");
        DropSnapshotColumns(migrationBuilder, "payment_details");
    }

    private static void AddSnapshotColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<string>(
            name: "country_code_at_issue",
            table: table,
            type: "character varying(2)",
            maxLength: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "vat_regime_at_issue",
            table: table,
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);
    }

    private static void DropSnapshotColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn(name: "country_code_at_issue", table: table);
        migrationBuilder.DropColumn(name: "vat_regime_at_issue", table: table);
    }
}
