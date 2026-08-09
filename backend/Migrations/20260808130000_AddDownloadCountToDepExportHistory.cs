using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Adds <c>download_count</c> to <c>dep_export_history</c>.
/// The column was intended in AddDepExportHistoryDownloadToken but is missing from the live schema.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260808130000_AddDownloadCountToDepExportHistory")]
public partial class AddDownloadCountToDepExportHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "download_count",
            table: "dep_export_history",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "download_count",
            table: "dep_export_history");
    }
}
