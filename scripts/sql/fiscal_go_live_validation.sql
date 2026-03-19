-- =============================================================================
-- Fiscal / RKSV-related schema validation (PostgreSQL)
-- Run AFTER migrations on a production-like copy. Read-only checks only.
-- Usage: psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql
-- Exit: review all rows; expect severity OK/WARN only for clean go-live.
-- =============================================================================

CREATE TEMP TABLE fiscal_validation_results (
    check_id   text PRIMARY KEY,
    severity   text NOT NULL, -- OK | WARN | FAIL
    metric     bigint NOT NULL,
    detail     text
);

-- ---------------------------------------------------------------------------
-- 1) Legacy column drift (post-71559 these must NOT exist)
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'drift_payment_details_KassenId',
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'KassenId'
       ) THEN 'FAIL' ELSE 'OK' END,
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'KassenId'
       ) THEN 1 ELSE 0 END,
       'Legacy KassenId column must be dropped after PaymentCashRegisterIdFk.';

INSERT INTO fiscal_validation_results
SELECT 'drift_receipt_sequences_kassen_id',
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'receipt_sequences' AND column_name = 'kassen_id'
       ) THEN 'FAIL' ELSE 'OK' END,
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'receipt_sequences' AND column_name = 'kassen_id'
       ) THEN 1 ELSE 0 END,
       'Legacy kassen_id on receipt_sequences must be dropped.';

INSERT INTO fiscal_validation_results
SELECT 'drift_signature_chain_register_id',
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'signature_chain_state' AND column_name = 'register_id'
       ) THEN 'FAIL' ELSE 'OK' END,
       CASE WHEN EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'signature_chain_state' AND column_name = 'register_id'
       ) THEN 1 ELSE 0 END,
       'Legacy register_id on signature_chain_state must be dropped.';

-- Required columns (current model)
INSERT INTO fiscal_validation_results
SELECT 'drift_missing_cash_register_id_payment',
       CASE WHEN NOT EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'payment_details' AND column_name = 'cash_register_id'
       ) THEN 'FAIL' ELSE 'OK' END,
       0,
       'payment_details.cash_register_id required.';

-- ---------------------------------------------------------------------------
-- 2) Critical unique indexes exist (matches EF / migrations)
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'idx_receipt_sequences_unique',
       CASE WHEN EXISTS (
           SELECT 1 FROM pg_indexes
           WHERE schemaname = 'public' AND tablename = 'receipt_sequences'
             AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%cash_register_id%' AND indexdef ILIKE '%sequence_date%'
       ) THEN 'OK' ELSE 'FAIL' END,
       0,
       'Expect unique index on (cash_register_id, sequence_date).';

INSERT INTO fiscal_validation_results
SELECT 'idx_signature_chain_register_unique',
       CASE WHEN EXISTS (
           SELECT 1 FROM pg_indexes
           WHERE schemaname = 'public' AND tablename = 'signature_chain_state'
             AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%cash_register_id%'
       ) THEN 'OK' ELSE 'FAIL' END,
       0,
       'Expect unique index on signature_chain_state.cash_register_id.';

INSERT INTO fiscal_validation_results
SELECT 'idx_payment_idempotency_partial_unique',
       CASE WHEN EXISTS (
           SELECT 1 FROM pg_indexes
           WHERE schemaname = 'public' AND tablename = 'payment_details'
             AND indexname = 'IX_payment_details_idempotency_key'
       ) THEN 'OK' ELSE 'WARN' END,
       0,
       'Expect IX_payment_details_idempotency_key (partial unique).';

-- Offline replay dedup: unique (CashRegisterId, payload_hash) — required for concurrent replay idempotency
INSERT INTO fiscal_validation_results
SELECT 'idx_offline_transactions_cash_register_payload_hash_unique',
       CASE WHEN NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'offline_transactions')
            THEN 'OK'
            WHEN EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'offline_transactions'
                  AND indexdef ILIKE '%unique%' AND indexdef ILIKE '%cash_register_id%' AND indexdef ILIKE '%payload_hash%'
            ) THEN 'OK' ELSE 'FAIL' END,
       0,
       'Expect unique index IX_offline_transactions_CashRegisterId_payload_hash for replay dedup.';

-- ---------------------------------------------------------------------------
-- 3) Duplicate data that would violate app invariants (even if index missing)
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'dup_receipt_sequences_per_register_day',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT cash_register_id, sequence_date
               FROM receipt_sequences
               GROUP BY 1, 2
               HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM (
               SELECT 1 FROM receipt_sequences
               GROUP BY cash_register_id, sequence_date
               HAVING COUNT(*) > 1
           ) t
       ), 0),
       'More than one receipt_sequences row per register per day.';

INSERT INTO fiscal_validation_results
SELECT 'dup_signature_chain_per_register',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT cash_register_id FROM signature_chain_state GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM (
               SELECT cash_register_id FROM signature_chain_state GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0),
       'Duplicate signature_chain_state rows per cash_register_id.';

INSERT INTO fiscal_validation_results
SELECT 'dup_payment_idempotency_key',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT idempotency_key FROM payment_details
               WHERE idempotency_key IS NOT NULL
               GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM (
               SELECT idempotency_key FROM payment_details
               WHERE idempotency_key IS NOT NULL
               GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0),
       'Duplicate non-null idempotency_key values.';

INSERT INTO fiscal_validation_results
SELECT 'dup_payment_receipt_number',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT "ReceiptNumber" FROM payment_details
               WHERE "ReceiptNumber" IS NOT NULL AND TRIM("ReceiptNumber") <> ''
               GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM (
               SELECT "ReceiptNumber" FROM payment_details
               WHERE "ReceiptNumber" IS NOT NULL AND TRIM("ReceiptNumber") <> ''
               GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0),
       'Duplicate non-empty ReceiptNumber on payment_details.';

INSERT INTO fiscal_validation_results
SELECT 'dup_receipts_receipt_number',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT receipt_number FROM receipts GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM (
               SELECT receipt_number FROM receipts GROUP BY 1 HAVING COUNT(*) > 1
           ) t
       ), 0),
       'Duplicate receipt_number in receipts.';

-- ---------------------------------------------------------------------------
-- 4) Orphan / FK-like integrity
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'orphan_receipts_payment',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM receipts r
           WHERE NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = r.payment_id)
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM receipts r
           WHERE NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = r.payment_id)
       ), 0),
       'Receipts pointing to missing payment_details.';

INSERT INTO fiscal_validation_results
SELECT 'orphan_payment_cash_register',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE NOT EXISTS (SELECT 1 FROM cash_registers cr WHERE cr.id = p.cash_register_id)
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE NOT EXISTS (SELECT 1 FROM cash_registers cr WHERE cr.id = p.cash_register_id)
       ), 0),
       'payment_details.cash_register_id not in cash_registers.';

INSERT INTO fiscal_validation_results
SELECT 'orphan_invoice_source_payment',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM invoices i
           WHERE i."SourcePaymentId" IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = i."SourcePaymentId")
       ), 0) = 0 THEN 'OK' ELSE 'WARN' END,
       COALESCE((
           SELECT COUNT(*) FROM invoices i
           WHERE i."SourcePaymentId" IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = i."SourcePaymentId")
       ), 0),
       'Invoice SourcePaymentId without payment (may be legacy).';

INSERT INTO fiscal_validation_results
SELECT 'orphan_payment_offline_transaction',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE p.offline_transaction_id IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM offline_transactions o WHERE o.id = p.offline_transaction_id)
       ), 0) = 0 THEN 'OK' ELSE 'WARN' END,
       COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE p.offline_transaction_id IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM offline_transactions o WHERE o.id = p.offline_transaction_id)
       ), 0),
       'offline_transaction_id FK target missing.';

INSERT INTO fiscal_validation_results
SELECT 'orphan_payment_original_receipt',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE p.original_receipt_id IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM receipts r WHERE r.receipt_id = p.original_receipt_id)
       ), 0) = 0 THEN 'OK' ELSE 'WARN' END,
       COALESCE((
           SELECT COUNT(*) FROM payment_details p
           WHERE p.original_receipt_id IS NOT NULL
             AND NOT EXISTS (SELECT 1 FROM receipts r WHERE r.receipt_id = p.original_receipt_id)
       ), 0),
       'original_receipt_id without receipts row.';

INSERT INTO fiscal_validation_results
SELECT 'receipt_register_mismatch_payment',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM receipts r
           INNER JOIN payment_details p ON p.id = r.payment_id
           WHERE r.cash_register_id IS DISTINCT FROM p.cash_register_id
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM receipts r
           INNER JOIN payment_details p ON p.id = r.payment_id
           WHERE r.cash_register_id IS DISTINCT FROM p.cash_register_id
       ), 0),
       'receipts.cash_register_id differs from payment_details.cash_register_id.';

-- ---------------------------------------------------------------------------
-- 5) Invoice cash register (71559 may leave zero-UUID)
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'invoice_zero_or_null_cash_register',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM invoices
           WHERE "CashRegisterId" IS NULL
              OR "CashRegisterId" = '00000000-0000-0000-0000-000000000000'::uuid
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM invoices
           WHERE "CashRegisterId" IS NULL
              OR "CashRegisterId" = '00000000-0000-0000-0000-000000000000'::uuid
       ), 0),
       'Invoices without valid CashRegisterId.';

-- ---------------------------------------------------------------------------
-- 6) Offline: payload_hash backfill + Synced consistency
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'offline_payload_hash_null_active',
       CASE
           WHEN NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'offline_transactions')
           THEN 'OK'
           WHEN COALESCE((
               SELECT COUNT(*) FROM offline_transactions
               WHERE payload_hash IS NULL
                 AND "Status" IN ('Pending', 'Synced')
           ), 0) = 0 THEN 'OK'
           ELSE 'WARN' END,
       COALESCE((
           SELECT COUNT(*) FROM offline_transactions
           WHERE payload_hash IS NULL AND "Status" IN ('Pending', 'Synced')
       ), 0),
       'Pending/Synced offline rows without payload_hash (dedup/replay weakened).';

INSERT INTO fiscal_validation_results
SELECT 'offline_payload_hash_coverage_pct',
       'OK',
       COALESCE((
           SELECT CASE WHEN COUNT(*) = 0 THEN 100::bigint
                ELSE (100.0 * COUNT(*) FILTER (WHERE payload_hash IS NOT NULL) / COUNT(*))::bigint
                END
           FROM offline_transactions
       ), 100),
       'Percent of offline rows with payload_hash (100 = all covered or table empty).';

INSERT INTO fiscal_validation_results
SELECT 'offline_synced_without_payment',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM offline_transactions o
           WHERE o."Status" = 'Synced'
             AND (o."SyncedPaymentId" IS NULL
                  OR NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = o."SyncedPaymentId"))
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
           SELECT COUNT(*) FROM offline_transactions o
           WHERE o."Status" = 'Synced'
             AND (o."SyncedPaymentId" IS NULL
                  OR NOT EXISTS (SELECT 1 FROM payment_details p WHERE p.id = o."SyncedPaymentId"))
       ), 0),
       'Synced offline row missing canonical payment.';

INSERT INTO fiscal_validation_results
SELECT 'offline_dup_payload_hash_same_register',
       CASE WHEN COALESCE((
           SELECT COUNT(*) FROM (
               SELECT "CashRegisterId", payload_hash
               FROM offline_transactions
               WHERE payload_hash IS NOT NULL
               GROUP BY 1, 2
               HAVING COUNT(*) > 1
           ) t
       ), 0) = 0 THEN 'OK' ELSE 'FAIL' END,
       COALESCE((
               SELECT COUNT(*) FROM (
               SELECT "CashRegisterId", payload_hash
               FROM offline_transactions
               WHERE payload_hash IS NOT NULL
               GROUP BY 1, 2
               HAVING COUNT(*) > 1
           ) t
       ), 0),
       'Violates unique (CashRegisterId, payload_hash) if index enforced.';

-- ---------------------------------------------------------------------------
-- 7) receipt_sequences vs payments (sanity: sequence not wildly behind — WARN only)
-- ---------------------------------------------------------------------------
INSERT INTO fiscal_validation_results
SELECT 'sequence_vs_payment_count_sanity',
       'OK',
       COALESCE((SELECT COUNT(DISTINCT cash_register_id) FROM receipt_sequences), 0),
       'Distinct registers in receipt_sequences (informational).';

-- ---------------------------------------------------------------------------
-- Summary output
-- ---------------------------------------------------------------------------
SELECT check_id, severity, metric, detail
FROM fiscal_validation_results
ORDER BY
    CASE severity WHEN 'FAIL' THEN 0 WHEN 'WARN' THEN 1 ELSE 2 END,
    check_id;

SELECT CASE WHEN EXISTS (SELECT 1 FROM fiscal_validation_results WHERE severity = 'FAIL')
       THEN 'RESULT: FAIL — do not go-live until resolved.'
       WHEN EXISTS (SELECT 1 FROM fiscal_validation_results WHERE severity = 'WARN')
       THEN 'RESULT: WARN — review before go-live.'
       ELSE 'RESULT: OK — no FAIL/WARN flags.'
       END AS go_live_summary
FROM (SELECT 1) _;
