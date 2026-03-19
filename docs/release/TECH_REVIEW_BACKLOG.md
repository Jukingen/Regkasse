# Technical review backlog — risk/impact/effort

Backlog derived from technical review findings. Implementation status and references to repo paths.

---

## P0 — Critical

| # | Item | Risk | Impact | Effort | Status | Repo reference |
|---|------|------|--------|--------|--------|-----------------|
| P0.1 | PostgreSQL concurrent replay + idempotency + unique index + advisory lock **test proof** | Data corruption / duplicate payments if concurrency or uniqueness fails | High: fiscal correctness | Medium | **Done** | `backend/KasseAPI_Final.Tests/PostgreSqlOfflineReplayConcurrencyTests.cs`, `PostgreSqlReplayFixture.cs` (Testcontainers or REGKASSE_TEST_POSTGRES). Tests: `AdvisoryLock_SecondAcquireWaitsUntilFirstScopeDisposed`, `ConcurrentReplay_SameRegisterSameOfflineId_ProducesSinglePayment`, `ConcurrentReplay_DifferentOfflineIdsSamePayload_OneCanonicalPayment`, `ConcurrentCreatePayment_SameIdempotencyKey_SingleRow_PostgresUniqueIndex`, `SecondReplayAfterSync_ReturnsSyncedWithoutNewReceipt`. |
| P0.2 | Production-like **migration validation** SQL/scripts | Wrong schema or missing indexes in prod | High: go-live safety | Low | **Done** | `scripts/sql/fiscal_go_live_validation.sql` — drift, unique indexes (receipt_sequences, signature_chain_state, payment_details idempotency, **offline_transactions CashRegisterId+payload_hash**), duplicate/orphan checks, offline payload_hash coverage. Run after migrations: `psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql`. |
| P0.3 | Export integrity: **best-effort signals** (no hard guarantee) | Misinterpretation of export as legal proof | Medium: compliance clarity | Low | **Done** | `docs/release/FISCAL_EXPORT_DIAGNOSTICS.md`, `FISCAL_EXPORT_TERMINOLOGY_GUIDE.md`, `FiscalExportService.cs` in-code note: integrity booleans are diagnostic only. |
| P0.4 | **Legacy payload-hash mismatch** risk measurement + repair approach | Replay dedup weakened; duplicate rows if hash inconsistent | High: offline correctness | Medium | **Done** | Measurement: `POST /api/admin/offline-payload-hash/analyze` (RuntimeMismatchCount), `scripts/sql/offline_payload_hash_legacy.sql` (informational). Repair: `OfflinePayloadHashMaintenanceService` + `POST .../repair` (DryRun then apply), lazy align on replay in `OfflineTransactionService.TryAlignStoredPayloadHashToRuntimeCanonicalAsync`. See `docs/release/LEGACY_PAYLOAD_HASH_MISMATCH.md`. |

---

## P1 — High

| # | Item | Risk | Impact | Effort | Status | Repo reference |
|---|------|------|--------|--------|--------|-----------------|
| P1.1 | Replay **batch correlation id** | Harder to trace multi-item replays | Medium | Low | **Done** | `OfflineReplayBatchCorrelationId` on replay; `OfflineIntentCoverageSample.ReplayBatchCorrelationId`; migration `20260319002427_OfflineReplayBatchCorrelation`. |
| P1.2 | **Old mobile build** coverage metric | Unknown share of replays without deviceId/sequence | Low | Medium | **Done** | `OfflineIntentCoverageSample`, `OfflineTransactionService` sampling, `GET /api/admin/offline-intent-coverage`, `docs/release/DEVICE_SEQUENCE_COVERAGE.md`. |
| P1.3 | **FinanzOnline** post-commit reconciliation / alerting | FO submission failures unnoticed | Medium | Medium | **Done** | `PaymentDetails` FO columns, `RetryFinanzOnlineSubmitAsync`, `FinanzOnlineReconciliationController`, failure kind (Transient/Permanent/Unknown), `docs/release/FINANZONLINE_RECONCILIATION.md`. |

---

## P2 — Medium

| # | Item | Risk | Impact | Effort | Status | Repo reference |
|---|------|------|--------|--------|--------|-----------------|
| P2.1 | **Structural fallback** simplification | Complexity and edge cases in legacy path | Low | Medium | **Done** | `OfflineReplayOptions` (AllowStructuralFallback, StructuralPayloadFallbackLimit), uniqueness guard, `docs/release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md`. |
| P2.2 | Export **terminology / docs** hardening | Same as P0.3 | Low | Low | **Done** | See P0.3. |
| P2.3 | Admin/support **forensic** visibility: fallback/legacy-path markers | Harder to diagnose replay path | Low | Low | **Optional** | Audit/response fields or UI badges for “replayed via structural fallback” or “payload_hash repaired on replay”; not yet implemented. |

---

## Ordering by risk/impact/effort (for implementation)

1. **P0.2** — Validation SQL (low effort, high impact).
2. **P0.4** — Measurement + repair doc (medium effort, high impact).
3. **P0.1** — Already covered by existing PG tests; add validation SQL check for offline unique index and short test-class comment.
4. **P0.3** — Already done in prior session.
5. P1/P2 items — Already implemented; P2.3 optional for future.

---

## How to run

- **PostgreSQL replay tests:** `dotnet test --filter "Category=PostgreSql"` (requires Docker or `REGKASSE_TEST_POSTGRES`).
- **Fiscal validation:** `psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql`.
- **Payload-hash analyze:** `POST /api/admin/offline-payload-hash/analyze` (ReportExport). **Repair:** `POST .../repair` with `DryRun: true` first (SystemCritical for actual repair).
