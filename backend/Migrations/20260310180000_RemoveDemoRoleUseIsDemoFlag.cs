using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasseAPI_Final.Migrations
{
    /// <summary>
    /// Demo is no longer a role: demo behavior is ApplicationUser.IsDemo only.
    /// Reassigns users from Demo role to Cashier, sets is_demo true, removes Demo from AspNetRoles.
    /// Requires Cashier role to exist (RoleSeedData). Idempotent where Demo role is absent.
    /// </summary>
    public partial class RemoveDemoRoleUseIsDemoFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Anyone with role column Demo gets demo flag and moves to Cashier column for sync with Identity
            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers""
SET is_demo = true,
    role = 'Cashier'
WHERE role = 'Demo';
");

            // 2) Users who have Demo in AspNetUserRoles: add Cashier if not already assigned (Cashier must exist)
            migrationBuilder.Sql(@"
INSERT INTO ""AspNetUserRoles"" (""UserId"", ""RoleId"")
SELECT ur.""UserId"", (SELECT r.""Id"" FROM ""AspNetRoles"" r WHERE r.""Name"" = 'Cashier' LIMIT 1)
FROM ""AspNetUserRoles"" ur
INNER JOIN ""AspNetRoles"" r ON r.""Id"" = ur.""RoleId"" AND r.""Name"" = 'Demo'
WHERE EXISTS (SELECT 1 FROM ""AspNetRoles"" c WHERE c.""Name"" = 'Cashier')
  AND NOT EXISTS (
    SELECT 1 FROM ""AspNetUserRoles"" ur2
    INNER JOIN ""AspNetRoles"" r2 ON r2.""Id"" = ur2.""RoleId"" AND r2.""Name"" = 'Cashier'
    WHERE ur2.""UserId"" = ur.""UserId""
);
");

            // 3) Ensure is_demo for users who still only had Demo in AspNetUserRoles but column was not Demo
            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers"" u
SET is_demo = true
WHERE EXISTS (
  SELECT 1 FROM ""AspNetUserRoles"" ur
  INNER JOIN ""AspNetRoles"" r ON r.""Id"" = ur.""RoleId"" AND r.""Name"" = 'Demo'
  WHERE ur.""UserId"" = u.""Id""
);
");

            // 4) Remove Demo role assignments
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetUserRoles""
WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""AspNetRoles"" WHERE ""Name"" = 'Demo');
");

            // 5) Remove permission claims on Demo role then delete role
            migrationBuilder.Sql(@"
DELETE FROM ""AspNetRoleClaims""
WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""AspNetRoles"" WHERE ""Name"" = 'Demo');
");

            migrationBuilder.Sql(@"
DELETE FROM ""AspNetRoles""
WHERE ""Name"" = 'Demo';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down not supported: cannot safely restore Demo role assignments.
        }
    }
}
