-- Idempotent fix: user may belong to multiple tenants (one row per user_id + tenant_id).
-- Safe to run before/after EF migration 20260522012416_FixUserTenantMembershipUniqueConstraint.
-- Removes global per-user uniqueness (including partial index on is_active = true).

DROP INDEX IF EXISTS "IX_user_tenant_memberships_user_id";

CREATE INDEX IF NOT EXISTS "IX_user_tenant_memberships_user_id"
    ON user_tenant_memberships (user_id);

DROP INDEX IF EXISTS "IX_user_tenant_memberships_user_id_tenant_id";

CREATE UNIQUE INDEX "IX_user_tenant_memberships_user_id_tenant_id"
    ON user_tenant_memberships (user_id, tenant_id);

-- Verification: duplicate (user_id, tenant_id) pairs must return no rows
SELECT user_id, tenant_id, COUNT(*) AS row_count
FROM user_tenant_memberships
GROUP BY user_id, tenant_id
HAVING COUNT(*) > 1;
