using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260720010000_AddDataExportDownloadToken")]
public partial class AddDataExportDownloadToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "download_token",
            table: "tenant_data_rights_requests",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "download_expires_at_utc",
            table: "tenant_data_rights_requests",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_tenant_data_rights_requests_download_token",
            table: "tenant_data_rights_requests",
            column: "download_token",
            unique: true,
            filter: "download_token IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_tenant_data_rights_requests_download_token",
            table: "tenant_data_rights_requests");

        migrationBuilder.DropColumn(name: "download_token", table: "tenant_data_rights_requests");
        migrationBuilder.DropColumn(name: "download_expires_at_utc", table: "tenant_data_rights_requests");
    }
}
