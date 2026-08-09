using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// DEP §7 download fix: opaque 24h download tokens, hot-file expiry, last-download stamp,
/// and wider <c>storage_path</c> for on-disk JSON under <c>App_Data/dep-exports</c>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260807120000_AddDepExportHistoryDownloadToken")]
public partial class AddDepExportHistoryDownloadToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "download_token",
            table: "dep_export_history",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "download_token_expires_at_utc",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "expires_at",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "downloaded_at",
            table: "dep_export_history",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "storage_path",
            table: "dep_export_history",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(500)",
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_download_token",
            table: "dep_export_history",
            column: "download_token",
            unique: true,
            filter: "\"download_token\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_tenant_id_expires_at",
            table: "dep_export_history",
            columns: new[] { "tenant_id", "expires_at" });

        // exported_at already indexed with tenant+register; add created-style lookup for ops cleanup.
        migrationBuilder.CreateIndex(
            name: "IX_dep_export_history_exported_at",
            table: "dep_export_history",
            column: "exported_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_download_token",
            table: "dep_export_history");

        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_tenant_id_expires_at",
            table: "dep_export_history");

        migrationBuilder.DropIndex(
            name: "IX_dep_export_history_exported_at",
            table: "dep_export_history");

        migrationBuilder.DropColumn(name: "download_token", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "download_token_expires_at_utc", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "expires_at", table: "dep_export_history");
        migrationBuilder.DropColumn(name: "downloaded_at", table: "dep_export_history");

        migrationBuilder.AlterColumn<string>(
            name: "storage_path",
            table: "dep_export_history",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(1024)",
            oldMaxLength: 1024,
            oldNullable: true);
    }
}
