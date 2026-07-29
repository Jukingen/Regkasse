using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>RKSV 7-year DEP export file archive + purge metadata on history rows.</summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260725180000_AddDepExportHistoryArchiving")]
public partial class AddDepExportHistoryArchiving : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "archived_at",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "archive_path",
            table: "dep_export_history",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "archive_checksum",
            table: "dep_export_history",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "retention_until",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "purged_at",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "purge_reason",
            table: "dep_export_history",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_tenant_id_archived_at",
            table: "dep_export_history",
            columns: new[] { "tenant_id", "archived_at" });

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_retention_until",
            table: "dep_export_history",
            column: "retention_until",
            filter: "\"retention_until\" IS NOT NULL AND \"purged_at\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_tenant_id_archived_at",
            table: "dep_export_history");

        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_retention_until",
            table: "dep_export_history");

        migrationBuilder.DropColumn(name: "archived_at", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "archive_path", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "archive_checksum", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "retention_until", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "purged_at", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "purge_reason", table: "dep_export_history");
    }
}
