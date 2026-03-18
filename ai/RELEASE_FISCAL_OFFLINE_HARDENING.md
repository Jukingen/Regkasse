# Release readiness & post-deployment validation (fiscal + offline hardening)

This document supports rollout of migrations and services that touch **offline replay**, **payment_details / invoices CashRegisterId**, **signature_chain_state**, and **fiscal export integrity**. It does **not** change product behaviour; it is operational guidance only.

---

## A) Deployment checklist

### Pre-deploy

- [ ] **Full DB backup** (logical dump + tested restore on a clone).
- [ ] **Maintenance window** agreed if migration runtime is non-trivial (indexes, backfills).
- [ ] **Target stack versions** pinned: backend .NET runtime, PostgreSQL major/minor, mobile + admin build artifacts.
- [ ] **Connection string** for `dotnet ef database update` matches production (or use CI job with secrets).
- [ ] **Extension**: migration may require `pgcrypto` (for `digest` on backfill); confirm DB allows `CREATE EXTENSION IF NOT EXISTS pgcrypto`.
- [ ] **App config**: `NEXT_PUBLIC_API_BASE_URL` (admin), POS API base URL, JWT/TSE/FinanzOnline unchanged unless intentionally released together.
- [ ] **Rollback plan** reviewed (section D); identify last known-good migration name if partial failure occurs.

### Deploy order (typical)

1. **Database**: `dotnet ef database update --context AppDbContext` (from backend project).
2. **Backend API**: deploy new binaries; restart workers if any consume DB.
3. **Admin**: deploy `frontend-admin` after backend (OpenAPI-aligned UI).
4. **Mobile POS**: deploy/store release; users on old builds still send replay without `deviceId`/sequence (nullable paths supported).

### Post-deploy (immediate)

- [ ] API health / readiness responds.
- [ ] At least one **authenticated** call succeeds (e.g. `/api/Auth/me`).
- [ ] Run **B) Post-migration validation** on staging or read-only replica first where possible.

---

## B) Post-migration data validation checklist

Run after migrations complete (staging → production).

| # | Check | Pass criteria |
|---|--------|----------------|
| 1 | **Schema** | Tables/columns exist: `offline_transactions` includes `payload_hash`, `server_received_at_utc`, `device_id`, `client_sequence_number`, integrity flags; `payment_details.cash_register_id` NOT NULL where expected; `signature_chain_state` exists. |
| 2 | **Unique indexes** | No duplicate violations on `(cash_register_id, payload_hash)` and `(cash_register_id, device_id, client_sequence_number)` where applicable (see SQL C1–C3). |
| 3 | **Invoices** | No rows with NULL/empty `CashRegisterId` where business requires POS linkage (see C4). |
| 4 | **Offline queue** | Pending count stable or decreasing after sync; Failed investigated (C5–C6). |
| 5 | **Fiscal export** | For each live register + recent UTC window: call fiscal export; `integrity.signatureChainValid` and `integrity.sequenceContinuous` true for clean registers; document exceptions (C7). |
| 6 | **Audit** | RKSV verifications / audit filters show expected actions after test replay (offline-origin, failed replay if applicable). |
| 7 | **Logs** | No surge of `PaymentCashRegisterIdFk`-related NOTICEs on fresh deploy (if many, run C4 remediation). |

---

## Smoke tests (manual or automated against staging)

Use a **test cash register** and **non-production TSE** where required. All steps assume valid JWT and roles (e.g. payment + admin where noted).

### 1) Payment create (online)

1. Open cart, complete **normal online payment** (fiscal path).
2. **Expect**: HTTP 2xx, payment persisted, receipt with TSE fields.
3. **DB spot-check** (optional): `payment_details` row has `cash_register_id` matching register.

### 2) Offline pending + replay

1. Simulate offline (or use NON_FISCAL_PENDING flow): queue payment; confirm local pending entry has `deviceId` / `clientSequenceNumber` on new builds.
2. Go online; trigger **sync** → `POST /api/offline-transactions/replay` (batch from POS).
3. **Expect**: Items move to Synced with canonical payment/receipt; duplicate replay does **not** create second receipt; response may include `exponentialBackoffHintSeconds` for Pending.
4. **Expect**: Admin receipt detail shows offline-origin metadata; flags only when drift/gap/duplicate rules fire.

### 3) Refund / reversal

1. Perform **refund or storno** per existing product flow (same endpoints as pre-release).
2. **Expect**: Fiscal rules unchanged; new payment/receipt behaviour matches pre-hardening semantics; no duplicate chain break from offline replay in same window.

### 4) Receipt retrieval

1. **Admin**: open receipt by ID (detail API + UI).
2. **Expect**: `clockDriftWarning`, `sequenceGapDetected`, `sequenceDuplicateDetected` present when applicable; no 500 on legacy receipts.

### 5) Fiscal export

1. **GET** ` /api/admin/fiscal-export?cashRegisterId={guid}&fromUtc={iso}&toUtc={iso}` (admin auth).
2. **Expect**: JSON includes `integrity`: `signatureChainValid`, `sequenceContinuous`, `offlineReplayGaps`, `totalOfflineTransactions`, `syncedOfflineTransactions`, `failedOfflineTransactions`.
3. **Expect**: `chainContinuityWarnings` empty on healthy register; non-empty documented for investigation only.

---

## C) Operational verification (SQL / admin)

**Column names**: This project mixes snake_case (`offline_transactions.id`, `created_at`, `payload_hash`, …) and PascalCase on some tables (`invoices."CashRegisterId"`). If a query fails, run:

```sql
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('offline_transactions', 'invoices', 'payment_details', 'cash_registers')
ORDER BY table_name, ordinal_position;
```

### C1 — Offline: Pending / Failed (stuck work)

Column names match EF/Npgsql defaults: `"Status"`, `"CashRegisterId"`, `"RetryCount"`, snake_case for `created_at`, `payload_hash`, `server_received_at_utc`, etc.

```sql
SELECT "Status",
       COUNT(*) AS cnt,
       MIN(created_at) AS oldest_created,
       MAX(updated_at) AS last_update
FROM offline_transactions
GROUP BY "Status"
ORDER BY "Status";
```

```sql
SELECT id,
       "CashRegisterId",
       "Status",
       "RetryCount",
       "LastErrorCode",
       clock_drift_warning,
       sequence_gap_detected,
       sequence_duplicate_detected,
       created_at,
       server_received_at_utc,
       "LastReplayAttemptAt"
FROM offline_transactions
WHERE "Status" IN ('Pending', 'Failed')
ORDER BY created_at DESC
LIMIT 200;
```

**Stuck Pending** (tune interval / retry threshold to your SLA):

```sql
SELECT id, "CashRegisterId", "RetryCount", "LastErrorCode", created_at, updated_at
FROM offline_transactions
WHERE "Status" = 'Pending'
  AND updated_at < NOW() - INTERVAL '24 hours'
ORDER BY updated_at
LIMIT 100;
```

### C2 — Offline: Failed with diagnostics

```sql
SELECT id,
       "Status",
       "LastErrorCode",
       "LastErrorMessageSafe",
       "RetryCount",
       "LastReplayAttemptAt",
       created_at
FROM offline_transactions
WHERE "Status" = 'Failed'
ORDER BY "LastReplayAttemptAt" DESC NULLS LAST
LIMIT 100;
```

### C3 — Dedup / sequence index health (duplicate attempt detection)

```sql
-- Rows sharing same payload_hash per register (should be at most one per hash after dedup)
SELECT "CashRegisterId", payload_hash, COUNT(*) AS n
FROM offline_transactions
WHERE payload_hash IS NOT NULL
GROUP BY "CashRegisterId", payload_hash
HAVING COUNT(*) > 1;
```

### C4 — Invoices: missing or mismatched CashRegisterId

**NULL / zero GUID (invalid for strict POS flows):**

```sql
SELECT id, "InvoiceNumber", "CashRegisterId", "KassenId", "SourcePaymentId"
FROM invoices
WHERE "CashRegisterId" IS NULL
   OR "CashRegisterId" = '00000000-0000-0000-0000-000000000000'::uuid
LIMIT 500;
```

**Mismatch vs payment_details (when invoice is tied to a payment):**

```sql
SELECT i.id AS invoice_id,
       i."CashRegisterId" AS invoice_cr,
       p.cash_register_id AS payment_cr,
       i."SourcePaymentId"
FROM invoices i
JOIN payment_details p ON p.id = i."SourcePaymentId"
WHERE i."SourcePaymentId" IS NOT NULL
  AND i."CashRegisterId" IS DISTINCT FROM p.cash_register_id
LIMIT 500;
```

### C5 — Payments without cash_register_id (should be empty post-migration)

```sql
SELECT COUNT(*) FROM payment_details WHERE cash_register_id IS NULL;
```

### C6 — signature_chain_state per register

```sql
SELECT cash_register_id, last_counter, updated_at
FROM signature_chain_state
ORDER BY updated_at DESC;
```

### C7 — Export integrity (API + optional jq)

After deploy, script or manual:

- Call fiscal export for each register for **yesterday UTC**.
- Parse JSON: `integrity.signatureChainValid === false` or `integrity.sequenceContinuous === false` → open `chainContinuityWarnings` and correlate with receipts/offline rows (C1).

**Example (bash):** fail CI if integrity flags are false:

```bash
curl -s -H "Authorization: Bearer $TOKEN" \
  "$API/api/admin/fiscal-export?cashRegisterId=$CR&fromUtc=...&toUtc=..." \
| jq -e '.integrity.signatureChainValid == true and .integrity.sequenceContinuous == true'
```

**Operational red flags** (from JSON, not raw SQL): `offlineReplayGaps > 0`, `failedOfflineTransactions > 0`, non-empty `chainContinuityWarnings`.

---

## D) Rollout risks and rollback notes

### Risks

| Risk | Mitigation |
|------|------------|
| **Long migration lock** | Run during low traffic; use backup + tested restore. |
| **Unique index violations** on offline dedup/sequence | Investigate duplicate payloads or duplicate device sequence; fix client or data before re-run. |
| **Invoice CashRegisterId** rows not backfilled | Migration may emit NOTICE; run C4 and remediate before relying on credit-note / strict paths. |
| **Legacy POS** without device/sequence | Still works; gaps/duplicates less visible until clients upgrade. |
| **pgcrypto missing** | Grant extension create or pre-install on DB. |

### Rollback (migration-sensitive)

- **EF Core “down”** on production is **risky** if later migrations/data depend on new columns. Prefer **forward fix** (data repair + new migration) over `database update <PreviousMigration>` on live unless disaster recovery.
- If you **must** revert:
  1. Restore DB from **pre-deploy backup** (safest).
  2. Or apply a **compensating migration** that drops only non-critical indexes/columns after business approval (may break running API version — deploy previous API **only** with coordinated DB state).
- **Do not** partially remove: `payment_details.cash_register_id` FK, `offline_transactions` unique indexes, or `signature_chain_state` without engineering sign-off — replay and TSE chaining depend on consistent schema.

### Application rollback

- Reverting **only** the API to an older build **without** reverting DB often fails (missing columns or NOT NULL). Rollback order: **DB restore (or forward migration)** then **API/admin/mobile** alignment.

---

## Reference paths

- Offline replay: `POST /api/offline-transactions/replay` — `backend/Controllers/OfflineTransactionsController.cs`
- Fiscal export: `GET /api/admin/fiscal-export` — `backend/Controllers/FiscalExportController.cs`
- Integrity DTO: `backend/Models/Export/FiscalExportDtos.cs` (`FiscalExportIntegrityDto`)
