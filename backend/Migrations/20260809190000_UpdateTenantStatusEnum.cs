using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations;

/// <summary>
/// Lifecycle status expansion: map legacy <c>deleted</c> → <c>archived</c>.
/// Column remains varchar — no schema change.
/// </summary>
public partial class UpdateTenantStatusEnum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE tenants
            SET status = 'archived'
            WHERE LOWER(status) = 'deleted';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE tenants
            SET status = 'deleted'
            WHERE LOWER(status) = 'archived';
            """);
    }
}
