-- One-time script: canonicalize legacy role "Administrator" → "Admin"
-- Use when not applying EF migrations (e.g. manual DB update). PostgreSQL.
-- After running, see ai/08_ROLE_MIGRATION.md for alias removal steps.

BEGIN;

-- ApplicationUser.Role (AspNetUsers.role)
UPDATE "AspNetUsers"
SET role = 'Admin'
WHERE role = 'Administrator';

-- Identity: give Admin to users who had Administrator (if they don't already have Admin)
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT ur."UserId", (SELECT r."Id" FROM "AspNetRoles" r WHERE r."Name" = 'Admin' LIMIT 1)
FROM "AspNetUserRoles" ur
INNER JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" AND r."Name" = 'Administrator'
WHERE NOT EXISTS (
  SELECT 1 FROM "AspNetUserRoles" ur2
  INNER JOIN "AspNetRoles" r2 ON r2."Id" = ur2."RoleId" AND r2."Name" = 'Admin'
  WHERE ur2."UserId" = ur."UserId"
);

-- Remove Administrator role from all users
DELETE FROM "AspNetUserRoles"
WHERE "RoleId" = (SELECT "Id" FROM "AspNetRoles" WHERE "Name" = 'Administrator');

COMMIT;
