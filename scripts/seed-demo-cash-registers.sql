-- =============================================================================
-- Demo cash registers for default/dev/test demo tenants
-- =============================================================================
-- Use when you want to backfill one default cash register per demo tenant
-- directly in PostgreSQL.
--
-- Important schema notes:
-- - cash_registers.Status is an INTEGER enum (Closed = 1)
-- - several columns keep legacy PascalCase names and therefore must be quoted
-- - this script is idempotent per tenant when that tenant already has any register
-- =============================================================================

INSERT INTO cash_registers (
    id,
    tenant_id,
    "RegisterNumber",
    "Location",
    "Status",
    "StartingBalance",
    "CurrentBalance",
    "LastBalanceUpdate",
    created_at,
    updated_at,
    is_active
)
SELECT
    gen_random_uuid(),
    t.id,
    'KASSE-001',
    'Hauptkasse',
    1,
    0,
    0,
    NOW(),
    NOW(),
    NOW(),
    true
FROM tenants t
WHERE t."Slug" IN ('default', 'cafe', 'bar', 'test', 'dev')
  AND t.status = 'active'
  AND t.is_active = true
  AND t.deleted_at_utc IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM cash_registers r
      WHERE r.tenant_id = t.id
  );
