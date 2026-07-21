using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260717140000_AddBackupArtifactStorageTier")]
public partial class AddBackupArtifactStorageTier : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "storage_tier",
            table: "backup_artifacts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "ix_backup_artifacts_storage_tier",
            table: "backup_artifacts",
            column: "storage_tier");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_backup_artifacts_storage_tier",
            table: "backup_artifacts");

        migrationBuilder.DropColumn(
            name: "storage_tier",
            table: "backup_artifacts");
    }
}
