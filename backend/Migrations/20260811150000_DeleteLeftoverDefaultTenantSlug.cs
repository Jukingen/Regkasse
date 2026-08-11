using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Cleanup: remove any leftover Wave-0 row still using slug <c>default</c>.
/// Does <b>not</b> delete the platform sentinel (<c>slug=platform</c>, same Guid) — audit/FK rows still reference it.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260811150000_DeleteLeftoverDefaultTenantSlug")]
public partial class DeleteLeftoverDefaultTenantSlug : Migration
{
    private const string PlatformId = "9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            -- Only remove orphan slug=default leftovers. Platform sentinel must remain.
            DELETE FROM tenants
            WHERE id = '{PlatformId}'
              AND LOWER("Slug") = 'default';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible cleanup — platform row (if present) already covers the Guid.
    }
}
