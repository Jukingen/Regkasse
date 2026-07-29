using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>P2-2: track pre-F5 legacy JWS count on DEP export history for Prüftool compatibility UI.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260729230000_AddDepExportHistoryLegacyJwsCount")]
public partial class AddDepExportHistoryLegacyJwsCount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "legacy_jws_count",
            table: "dep_export_history",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "legacy_jws_count",
            table: "dep_export_history");
    }
}
