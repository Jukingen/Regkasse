using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Persist simulation metadata on DEP §7 history rows so FA can badge exports
/// created under RKSV demo / Soft TSE (signatures not legally binding).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260807150000_AddDepExportHistoryIsSimulated")]
public partial class AddDepExportHistoryIsSimulated : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_simulated",
            table: "dep_export_history",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "simulation_note",
            table: "dep_export_history",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "is_simulated", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "simulation_note", table: "dep_export_history");
    }
}
