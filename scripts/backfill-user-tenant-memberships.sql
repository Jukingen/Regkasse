-- =============================================================================
-- Backfill: active default-tenant membership for users without one
-- =============================================================================
-- Run manually against PostgreSQL after migration UserTenantMemberships is applied.
-- Idempotent: safe to re-run; skips users who already have an active membership row.
--
-- Default tenant id must match LegacyDefaultTenantIds.Primary in code / tenant seed.
-- =============================================================================

INSERT INTO user_tenant_memberships (id, user_id, tenant_id, is_active, created_at_utc)
SELECT gen_random_uuid(), u."Id", '9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c'::uuid, true, NOW()
FROM "AspNetUsers" u
WHERE NOT EXISTS (
    SELECT 1
    FROM user_tenant_memberships m
    WHERE m.user_id = u."Id" AND m.is_active = true
);
