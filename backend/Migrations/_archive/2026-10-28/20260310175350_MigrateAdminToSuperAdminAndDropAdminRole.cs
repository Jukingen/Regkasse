using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Data migration: single top-level admin is SuperAdmin. Reassigns every user from Admin to SuperAdmin
    /// (AspNetUsers.role + AspNetUserRoles), then removes Admin role row. Prerequisite: SuperAdmin role must
    /// exist in AspNetRoles (seeded at startup). Down not supported—restore from backup if needed.
    /// </summary>
    public partial class MigrateAdminToSuperAdminAndDropAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) AspNetUserRoles: give SuperAdmin to each user who has Admin, if they do not already have SuperAdmin.
            migrationBuilder.Sql(@"
INSERT INTO ""AspNetUserRoles"" (""UserId"", ""RoleId"")
SELECT ur.""UserId"", (SELECT r.""Id"" FROM ""AspNetRoles"" r WHERE r.""Name"" = 'SuperAdmin' LIMIT 1)
FROM ""AspNetUserRoles"" ur
INNER JOIN ""AspNetRoles"" r ON r.""Id"" = ur.""RoleId"" AND r.""Name"" = 'Admin'
WHERE EXISTS (SELECT 1 FROM ""AspNetRoles"" WHERE ""Name"" = 'SuperAdmin')
  AND NOT EXISTS (
  SELECT 1 FROM ""AspNetUserRoles"" ur2
  INNER JOIN ""AspNetRoles"" r2 ON r2.""Id"" = ur2.""RoleId"" AND r2.""Name"" = 'SuperAdmin'
  WHERE ur2.""UserId"" = ur.""UserId""
);
");

            // 2) ApplicationUser.Role column (AspNetUsers.role) — keep in sync with Identity assignments.
            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers""
SET role = 'SuperAdmin'
WHERE role = 'Admin';
");

            // 3) Remove Admin role assignments (users already have SuperAdmin from step 1 when Admin existed).
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetUserRoles""
WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""AspNetRoles"" WHERE ""Name"" = 'Admin');
");

            // 4) Remove permission claims tied to Admin role before deleting the role row.
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetRoleClaims""
WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""AspNetRoles"" WHERE ""Name"" = 'Admin');
");

            // 5) Delete Admin role from AspNetRoles (idempotent if already absent).
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetRoles""
WHERE ""Name"" = 'Admin';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migration: down not supported (cannot safely restore Admin assignments).
            // Revert via backup or manually re-create Admin role and reassign users.
        }
    }
}
