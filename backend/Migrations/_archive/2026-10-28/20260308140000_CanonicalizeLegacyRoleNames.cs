using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Historical: one-time data migration. Legacy admin role in DB was renamed to Admin; single admin role is Admin.
    /// SQL references legacy DB value only; no active role constant.
    /// </summary>
    public partial class CanonicalizeLegacyRoleNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ApplicationUser.Role column (AspNetUsers.role)
            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers""
SET role = 'Admin'
WHERE role = 'Administrator';
");

            // Identity AspNetUserRoles: assign Admin to users who had only Administrator, then remove Administrator
            migrationBuilder.Sql(@"
INSERT INTO ""AspNetUserRoles"" (""UserId"", ""RoleId"")
SELECT ur.""UserId"", (SELECT r.""Id"" FROM ""AspNetRoles"" r WHERE r.""Name"" = 'Admin' LIMIT 1)
FROM ""AspNetUserRoles"" ur
INNER JOIN ""AspNetRoles"" r ON r.""Id"" = ur.""RoleId"" AND r.""Name"" = 'Administrator'
WHERE NOT EXISTS (
  SELECT 1 FROM ""AspNetUserRoles"" ur2
  INNER JOIN ""AspNetRoles"" r2 ON r2.""Id"" = ur2.""RoleId"" AND r2.""Name"" = 'Admin'
  WHERE ur2.""UserId"" = ur.""UserId""
);
");

            migrationBuilder.Sql(@"
DELETE FROM ""AspNetUserRoles""
WHERE ""RoleId"" = (SELECT ""Id"" FROM ""AspNetRoles"" WHERE ""Name"" = 'Administrator');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migration: down not supported (cannot restore original Administrator assignments safely).
            // To revert, restore from backup or re-seed Administrator and reassign manually.
        }
    }
}
