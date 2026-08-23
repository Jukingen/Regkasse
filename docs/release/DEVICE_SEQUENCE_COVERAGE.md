# DeviceId / ClientSequence coverage (offline replay observability)

## Purpose

Measure cases where older mobile builds still omit `DeviceId` or `ClientSequenceNumber`, and make rollout risk visible. Domain behavior does not change; only observability is added.

---

## How coverage is calculated

- **Source:** On every offline replay request, a **sample** is written to the `offline_intent_coverage_samples` table for each valid item (non-empty `OfflineTransactionId` and `CashRegisterId`).
- **Fields (per sample):** `created_at_utc`, `cash_register_id`, `has_device_id` (true if sent), `has_client_sequence` (true if sent), `replay_batch_correlation_id`.
- **deviceId missing rate** = count of samples with `has_device_id = false` / total sample count (for a time window and optional cash-register filter).
- **sequence missing rate** = count of samples with `has_client_sequence = false` / total sample count.
- **Coverage by cash register:** Group the same table by `cash_register_id` to produce total / withDeviceId / withSequence counts per cash register.
- **Time-based trend:** Group by `created_at_utc` date/hour to produce daily/hourly coverage rates (SQL or the admin endpoint).

Sample writes are **best-effort** in the replay flow. If insert fails, only a warning is logged; the replay is not treated as failed.

---

## When the fraud-resistance path degrades

Sequence-based fraud protection on offline replay runs in this block:

- **Condition:** `!string.IsNullOrWhiteSpace(offline.DeviceId) && offline.ClientSequenceNumber.HasValue`
- **Action:** Compare against the previous maximum `ClientSequenceNumber` for the same `(CashRegisterId, DeviceId)`. On gap or duplicate, apply the related audits and status updates (Failed / gap flag).
- **Unique index:** `(CashRegisterId, DeviceId, ClientSequenceNumber)` — in Postgres, **null** values are not treated as unique, so rows **missing** DeviceId or ClientSequenceNumber **do not** get duplicate protection from this index.

**Result:** For intents that omit DeviceId or ClientSequenceNumber (older mobile builds):

- Sequence-based gap/duplicate checks **do not run**.
- Multiple “empty sequence” intents from the same device are not blocked by the unique index (nulls can repeat).
- The fraud-resistance path is **off for that intent**. If the share of old clients is high during rollout, risk increases.

This behavior was not changed in `OfflineTransactionService`. What is new is the ability to measure what share of intents do not get this protection.

---

## Changed / added files

| File | Change |
|------|--------|
| `backend/Models/OfflineIntentCoverageSample.cs` | New entity (observability sample). |
| `backend/Data/AppDbContext.cs` | `OfflineIntentCoverageSamples` DbSet + table configuration. |
| `backend/Migrations/20260319003746_AddOfflineIntentCoverageSamples.cs` | `offline_intent_coverage_samples` table. |
| `backend/Services/OfflineTransactionService.cs` | After a valid item in the replay loop, call `RecordOfflineIntentCoverageAsync`; private method inserts the sample (try/catch). |
| `backend/Models/Export/FiscalExportDtos.cs` | `FiscalExportIntegrityDto`: `OfflineIntentCoverageTotal`, `OfflineIntentCoverageWithDeviceId`, `OfflineIntentCoverageWithSequence`. |
| `backend/Services/FiscalExportService.cs` | Coverage query in the export window; counts + diagnostic note on Integrity. |
| `backend/Controllers/OfflineIntentCoverageController.cs` | `GET /api/admin/offline-intent-coverage` (`fromUtc`, `toUtc`, optional `cashRegisterId`). |
| `docs/release/DEVICE_SEQUENCE_COVERAGE.md` | This document. |

---

## How to watch this in operations

1. **Admin endpoint:** `GET /api/admin/offline-intent-coverage?fromUtc=&toUtc=&cashRegisterId=`  
   - Response: `total`, `withDeviceId`, `withSequence`, `deviceIdMissingRate`, `sequenceMissingRate`, `byRegister` (per cash register).  
   - Default: last 24 hours. If `cashRegisterId` is omitted, all cash registers.

2. **Fiscal export:** `GET /api/admin/fiscal-export?...` → `integrity.offlineIntentCoverageTotal`, `offlineIntentCoverageWithDeviceId`, `offlineIntentCoverageWithSequence`, plus a summary sentence in `integrityDiagnosticNotes` (DeviceId/Sequence coverage % and the “Low coverage increases replay/fraud-resistance risk” warning).

3. **SQL (trend):**  
   - Daily: group by `created_at_utc::date`; count total / with_device_id / with_client_sequence.  
   - Hourly: same metrics with `date_trunc('hour', created_at_utc)`.

4. **Log:** If sample insert fails, `OfflineTransactionService` warning: “Offline intent coverage sample insert failed for CashRegisterId=…; replay continues.”

---

## Use for rollout decisions

- A high **deviceIdMissingRate** or **sequenceMissingRate** means many old clients are still replaying; sequence-based fraud resistance is off for those intents.
- **byRegister** can show low coverage on specific cash registers/locations so you can plan a target build update or training.
- A time-based trend can confirm rates drop after a new-build rollout. If they stay high, old-client usage is continuing.
- Fiscal export fields can report “in this period, X% of offline intents had DeviceId/Sequence” in audit/support packages.

---

## Related references

- Offline replay flow: `backend/Services/OfflineTransactionService.cs` (ReplayOfflineTransactionsAsync, CreateOfflineTransactionRowAsync).
- Sequence check: “Step 2: client sequence tracking” block and unique-index comment in the same file.
- Fiscal export semantics: `docs/release/FISCAL_EXPORT_DIAGNOSTICS.md`.
