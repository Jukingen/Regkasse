-- =============================================================================
-- Per-tenant cash_register_settings (POS ensure-ready / auto-open flags)
-- =============================================================================
-- Idempotent INSERT per tenant. Dev tenant: auto_open_sole_closed_register = true.
-- =============================================================================

INSERT INTO cash_register_settings (
    tenant_id,
    effective_default_on_pos_entry,
    auto_open_sole_closed_register,
    auto_open_assigned_closed_register,
    default_auto_open_opening_balance,
    updated_at_utc
)
SELECT
    t.id,
    true,
    CASE WHEN t."Slug" = 'dev' THEN true ELSE false END,
    false,
    0,
    NOW()
FROM tenants t
WHERE t."Slug" IN ('default', 'dev', 'cafe', 'bar')
   OR t.id = '9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c'::uuid
ON CONFLICT (tenant_id) DO UPDATE SET
    effective_default_on_pos_entry = EXCLUDED.effective_default_on_pos_entry,
    auto_open_sole_closed_register = EXCLUDED.auto_open_sole_closed_register,
    auto_open_assigned_closed_register = EXCLUDED.auto_open_assigned_closed_register,
    default_auto_open_opening_balance = EXCLUDED.default_auto_open_opening_balance,
    updated_at_utc = NOW();
