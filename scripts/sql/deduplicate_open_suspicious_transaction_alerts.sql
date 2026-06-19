-- One-time cleanup: duplicate OPEN suspicious_transaction_alerts per (tenant_id, dedup_key).
-- Keeps the newest row (detected_at_utc); acknowledges older siblings.
-- Safe to re-run: only groups with rn > 1 are updated.

WITH ranked AS (
    SELECT
        id,
        tenant_id,
        dedup_key,
        detected_at_utc,
        ROW_NUMBER() OVER (
            PARTITION BY tenant_id, dedup_key
            ORDER BY detected_at_utc DESC, created_at DESC, id DESC
        ) AS rn
    FROM suspicious_transaction_alerts
    WHERE status = 1
      AND is_active = true
)
UPDATE suspicious_transaction_alerts AS a
SET
    status = 2,
    updated_at = NOW(),
    updated_by = 'script:deduplicate_open_suspicious_transaction_alerts'
FROM ranked AS r
WHERE a.id = r.id
  AND r.rn > 1;

-- Preview (optional): rows that would be acknowledged
-- SELECT r.* FROM ranked r WHERE r.rn > 1;
