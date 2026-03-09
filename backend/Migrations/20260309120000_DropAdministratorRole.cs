using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Authorization: remove legacy admin role from AspNetRoles (single admin role is Admin).
    /// Run after CanonicalizeLegacyRoleNames. SQL uses DB value for legacy role name; not an active constant.
    /// </summary>
    public partial class DropAdministratorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete legacy role row if present (idempotent). Value in SQL is historical DB data only.
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetRoles""
WHERE ""Name"" = 'Administrator';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreating the role is optional; no data to restore. Leave empty for fail-safe.
        }
    }
}
