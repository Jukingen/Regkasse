using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Snapshot-only sync: many recent additive migrations were authored without Designer files,
/// so <c>AppDbContextModelSnapshot</c> lagged the runtime model. Schema DDL remains in the
/// earlier dated migrations (DEP download token / is_simulated / download_count, product
/// catalog, TSE Signaturkarte, etc.). This migration applies <b>no</b> SQL — it only
/// advances the EF model snapshot so <c>dotnet ef migrations has-pending-model-changes</c>
/// is clean before production deploys.
/// </summary>
public partial class SyncDepExportAndPendingModelSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty — see class summary.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty — see class summary.
    }
}


