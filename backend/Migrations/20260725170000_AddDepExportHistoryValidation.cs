using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>Persist automatic DEP export validation status on history rows.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725170000_AddDepExportHistoryValidation")]
public partial class AddDepExportHistoryValidation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "validation_status",
            table: "dep_export_history",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "validated_at",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "validation_report_json",
            table: "dep_export_history",
            type: "jsonb",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_tenant_id_validation_status",
            table: "dep_export_history",
            columns: new[] { "tenant_id", "validation_status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_tenant_id_validation_status",
            table: "dep_export_history");

        migrationBuilder.DropColumn(name: "validation_status", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "validated_at", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "validation_report_json", table: "dep_export_history");
    }
}
