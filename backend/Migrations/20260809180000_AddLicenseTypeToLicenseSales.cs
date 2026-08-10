using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Adds nullable <c>license_type</c> to <c>license_sales</c> and backfills existing rows to Starter (1).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260809180000_AddLicenseTypeToLicenseSales")]
public partial class AddLicenseTypeToLicenseSales : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "license_type",
            table: "license_sales",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE license_sales
            SET license_type = 1
            WHERE license_type IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "license_type",
            table: "license_sales");
    }
}
