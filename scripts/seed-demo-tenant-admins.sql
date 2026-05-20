-- =============================================================================
-- Demo tenants (dev, cafe, bar) + admin membership backfill
-- =============================================================================
-- Prefer automatic seed on API startup: DemoTenantAdminSeed (creates users with
-- Identity password hash). Default password: DemoTenant1!
--
-- This script is idempotent for tenants and memberships when users already exist.
-- It does NOT insert AspNetUsers (password hash must come from the app).
-- Run after migrations SeedDemoTenantAdmins and AddIsOwnerToUserTenantMemberships.
-- =============================================================================

INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
SELECT 'b0000001-0001-4001-8001-000000000001'::uuid, 'Development', 'dev', NOW(), true, 'active', 'admin@dev.regkasse.at'
WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'dev');

INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
SELECT 'b0000001-0001-4001-8001-000000000002'::uuid, 'Test Cafe', 'cafe', NOW(), true, 'active', 'admin@cafe.regkasse.at'
WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'cafe');

INSERT INTO tenants (id, "Name", "Slug", created_at, is_active, status, email)
SELECT 'b0000001-0001-4001-8001-000000000003'::uuid, 'Test Bar', 'bar', NOW(), true, 'active', 'admin@bar.regkasse.at'
WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE "Slug" = 'bar');

INSERT INTO user_tenant_memberships (id, user_id, tenant_id, is_active, is_owner, created_at_utc)
SELECT gen_random_uuid(), u."Id", t.id, true, true, NOW()
FROM (VALUES
    ('admin@dev.regkasse.at', 'dev'),
    ('admin@cafe.regkasse.at', 'cafe'),
    ('admin@bar.regkasse.at', 'bar')
) AS pair(email, slug)
JOIN "AspNetUsers" u ON lower(u."Email") = lower(pair.email)
JOIN tenants t ON t."Slug" = pair.slug
WHERE NOT EXISTS (
    SELECT 1
    FROM user_tenant_memberships m
    WHERE m.user_id = u."Id" AND m.tenant_id = t.id AND m.is_active = true
);

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "AspNetUsers" u
CROSS JOIN "AspNetRoles" r
WHERE r."Name" = 'Manager'
  AND lower(u."Email") IN (
      'admin@dev.regkasse.at',
      'admin@cafe.regkasse.at',
      'admin@bar.regkasse.at'
  )
  AND NOT EXISTS (
      SELECT 1 FROM "AspNetUserRoles" ur
      WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );
