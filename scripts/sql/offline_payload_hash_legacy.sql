-- Offline transactions: informational counts only.
-- True "runtime mismatch" rate requires the same canonicalization as OfflinePayloadHashing.NormalizeAndHash (C#).
-- Migration backfill used: encode(digest("PayloadJson"::text, 'sha256'), 'hex')

SELECT
  COUNT(*) AS total_offline_rows,
  COUNT(*) FILTER (WHERE payload_hash IS NULL OR btrim(payload_hash) = '') AS null_or_empty_hash,
  COUNT(*) FILTER (WHERE payload_hash IS NOT NULL AND btrim(payload_hash) <> '') AS with_hash
FROM offline_transactions;

-- Rows that still match migration-style hash (digest of jsonb text) — subset of "aligned with old rule"
SELECT COUNT(*) AS rows_where_stored_hash_equals_pg_text_digest
FROM offline_transactions
WHERE payload_hash IS NOT NULL
  AND lower(replace(payload_hash, ' ', '')) = encode(digest("PayloadJson"::text, 'sha256'), 'hex');  -- column name as created by EF
