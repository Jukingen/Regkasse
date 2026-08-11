using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Step 1–2 default-tenant freeze: rename Wave-0 <c>default</c> → <c>platform</c> and mark inactive for business use.
/// Guid stays <see cref="KasseAPI_Final.Models.Constants.SystemTenantIds.Platform"/> for FK continuity.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260811140000_RenameDefaultTenantToPlatform")]
public partial class RenameDefaultTenantToPlatform : Migration
{
    private const string PlatformId = "9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            UPDATE tenants
            SET
              "Slug" = 'platform',
              "Name" = CASE
                WHEN "Name" IS NULL OR BTRIM("Name") = '' OR LOWER("Name") = 'default' THEN 'Platform'
                ELSE "Name"
              END,
              is_active = false,
              updated_at = NOW()
            WHERE id = '{PlatformId}'
              AND LOWER("Slug") IN ('default', 'platform');
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            UPDATE tenants
            SET
              "Slug" = 'default',
              "Name" = CASE
                WHEN LOWER("Name") = 'platform' THEN 'Default'
                ELSE "Name"
              END,
              is_active = true,
              updated_at = NOW()
            WHERE id = '{PlatformId}'
              AND LOWER("Slug") = 'platform';
            """);
    }
}
