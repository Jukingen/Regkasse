# FinanzOnline submit — reconciliation & alerting

## Purpose

After the payment / invoice / receipt DB transaction commits, FinanzOnline submit can leave a temporary mismatch between the DB and external reporting. That mismatch must not stay silent: persist state, raise alerts/events, and keep operational visibility.

**Important:** DB truth is not rolled back. Only reconciliation state and retry/alerting are added.

---

## 1. Post-commit flow and error behavior

- **Location:** `PaymentService.CreatePaymentAsync` — after the transaction commit (payment + invoice + receipt + stock), immediately after audit logs.
- **Condition:** `effectiveTseRequired == true` (TSE-signed payment).
- **Action:** `IFinanzOnlineService.SubmitInvoiceAsync(createdInvoice)` is called. The result is written **best-effort** to reconciliation fields on `PaymentDetails`. If submit or the state update fails, the payment is still treated as successful (no DB rollback).

---

## 2. Failure classification

| Kind | Description | Example | Retry / Alert |
|------|-------------|---------|---------------|
| **Transient** | Temporary network/server error | `HttpRequestException`, `TaskCanceledException`, timeout, 5xx | Retry is appropriate; status **Pending**. |
| **Permanent** | Permanent / validation / duplicate | Message contains "duplicate", "already submitted", "validation", "forbidden" | No automatic retry; status **Failed**. |
| **Unknown** | Other | Unexpected exception | Retry may be attempted; status **Failed**. |

Classification: `FinanzOnlineService.ClassifyFailure` and, on the payment catch path, `PaymentService.ClassifyFinanzOnlineFailure` (same logic).

---

## 3. New state model (PaymentDetails)

| Field | Description |
|-------|-------------|
| `finanz_online_status` | **NotSent** (unused), **Pending**, **Submitted**, **Failed**, **NeedsReconciliation** |
| `finanz_online_error` | Last error message (truncated to 500); null when Submitted. |
| `finanz_online_reference_id` | External-system reference ID (set when Submitted). |
| `finanz_online_last_attempt_at_utc` | Last attempt time (UTC). |
| `finanz_online_retry_count` | Manual/automatic retry counter. |

**Rules:**

- After the first submit: **Submitted** (success) or **Pending** (Transient) / **Failed** (Permanent/Unknown).
- A successful retry becomes **Submitted**; a failed retry stays **Pending** or **Failed**.
- If status is already **Submitted**, a retry **does not call** external submit again (duplicate-submit risk is managed).

---

## 4. Retry-safe reconciliation & event/log

- **FinanzOnlineSubmission:** One row per submit attempt (success or failure); `InvoiceId`, `Success`, `ErrorMessage`, `ResponseStatusCode`, `ResponseBodyJson`, `SubmittedAt`.
- **FinanzOnlineError:** One row per **failed** submit; `ErrorType = "Submission"`, `InvoiceNumber`, `CashRegisterId`, `ReferenceId`, `Status = "Active"`.
- **Log:** Success: `Invoice sent to FinanzOnline: {InvoiceId}, ReferenceId=…`. Failure: `FinanzOnline submit failed for Invoice {InvoiceId}: {Error}, FailureKind=…`. If the state update fails: `Failed to update PaymentDetails FinanzOnline state for PaymentId=…; reconciliation view may be stale.`

Alerting: After the automatic retry job runs, if the Failed count or the repeated-failure threshold on the same cash register is exceeded, write a log (FinanzOnlineAlert) and optionally emit an event via `IFinanzOnlineAlertSink`. Metrics: `GET /api/admin/finanzonline-reconciliation/metrics` (`finanzonline_submit_total`, `finanzonline_submit_failed_total` by FailureKind).

---

## 5. Ops dashboard / queryable status

- **GET /api/admin/finanzonline-reconciliation**  
  Queryable list: `status` (Pending, Failed, NeedsReconciliation), `cashRegisterId`, `fromUtc`, `toUtc`, `limit`.  
  Response: `total`, `items[]` (PaymentId, ReceiptNumber, CreatedAt, TotalAmount, CashRegisterId, FinanzOnlineStatus, FinanzOnlineError, FinanzOnlineReferenceId, FinanzOnlineLastAttemptAtUtc, FinanzOnlineRetryCount).

- **POST /api/admin/finanzonline-reconciliation/retry/{paymentId}**  
  Manual retry. If already Submitted, returns 200 + “Submitted”; external submit is not called again.

- **GET /api/admin/finanzonline-reconciliation/metrics**  
  Counters: submitTotal, submitFailedTotal, submitFailedTransient/Permanent/Unknown (reset when the application restarts).

- **SQL (trend / dashboard):**  
  `SELECT finanz_online_status, COUNT(*) FROM payment_details WHERE created_at >= @from AND created_at <= @to GROUP BY finanz_online_status`

---

## 6. Support / admin visibility

- Reconciliation list: **Pending / Failed / NeedsReconciliation** can be filtered.
- Retry: Retry a single payment with “Retry submit”.
- Fiscal export: FinanzOnlineStatus/Error/ReferenceId currently exist only for **DailyClosing**. Payment-level FO status is tracked through this endpoint and the `payment_details` columns.

---

## 7. Duplicate external submit risk

- A retry for the same payment **does not call** `SubmitInvoiceAsync` when status is **Submitted**; the response is 200 with the existing `FinanzOnlineReferenceId`.
- If the real FinanzOnline API later returns “already submitted,” that can be classified as **Permanent** (duplicate), status set to **Submitted**, and the existing referenceId kept (currently simulation).

---

## 8. Automatic retry job (background) and metrics

- **HostedService:** `FinanzOnlineRetryHostedService` — periodically (default 2 min) selects Pending rows; applies exponential backoff (BaseDelaySeconds × 2^RetryCount, cap BackoffCapSeconds) and a max retry (default 5). Success → Submitted; Transient fail → Pending; Permanent/Unknown fail → Failed. After max retry is exceeded, status becomes Failed and "(Max retries exceeded)." is appended to the error text.
- **Duplicate submit:** The job selects only status = Pending. `RetryFinanzOnlineSubmitAsync` does not call external submit if already Submitted.
- **Alert:** At the end of each loop: (1) If Failed count > AlertFailedThreshold, log "FinanzOnlineAlert: Failed count … exceeds threshold …" + `IFinanzOnlineAlertSink.OnFailedCountThresholdExceeded`. (2) If Failed or max-retry Pending count on the same cash register ≥ RegisterRepeatedFailureThreshold, log + `OnRegisterRepeatedFailure`.
- **Metrics:** `IFinanzOnlineMetrics` — `IncrementSubmitTotal` on every submit attempt; `IncrementSubmitFailed(FailureKind)` on failure. Read via GET `/api/admin/finanzonline-reconciliation/metrics` (submitTotal, submitFailedTransient/Permanent/Unknown).
- **Config:** `appsettings` → `FinanzOnlineRetryJob`: Enabled, Interval, MaxRetryCount, BaseDelaySeconds, BackoffCapSeconds, BatchSize, AlertFailedThreshold, RegisterRepeatedFailureThreshold.

---

## 9. Changed / added files

| File | Change |
|------|--------|
| `backend/Services/IFinanzOnlineService.cs` | `FinanzOnlineSubmitResponse.FailureKind`, `FinanzOnlineFailureKind` enum. |
| `backend/Models/PaymentDetails.cs` | FinanzOnline reconciliation columns. |
| `backend/Data/AppDbContext.cs` | PaymentDetails FO column config + index. |
| `backend/Migrations/*_AddPaymentDetailsFinanzOnlineReconciliation.cs` | Table migration. |
| `backend/Services/FinanzOnlineService.cs` | SubmitInvoiceAsync: submission/error row, failure classification. |
| `backend/Services/PaymentService.cs` | Post-commit FO state update; `RetryFinanzOnlineSubmitAsync`; `UpdatePaymentFinanzOnlineStateAsync`, `ClassifyFinanzOnlineFailure`; optional `IFinanzOnlineMetrics`. |
| `backend/Controllers/FinanzOnlineReconciliationController.cs` | GET list, POST retry, GET metrics. |
| `backend/Options/FinanzOnlineRetryJobOptions.cs` (namespace Configuration) | Retry job config. |
| `backend/Services/FinanzOnlineMetrics.cs` | IFinanzOnlineMetrics, IFinanzOnlineAlertSink, NoOpFinanzOnlineAlertSink. |
| `backend/Services/FinanzOnlineRetryHostedService.cs` | Background job: Pending retry + backoff + max retry + alert. |
| `backend/Program.cs` | FinanzOnlineRetryJob options, IFinanzOnlineMetrics, IFinanzOnlineAlertSink, FinanzOnlineRetryHostedService. |
| `backend/KasseAPI_Final.Tests/FinanzOnlineReconciliationTests.cs` | Retry success, already-submitted idempotency, payment not found. |
| `docs/release/FINANZONLINE_RECONCILIATION.md` | This document. |

---

## 10. Previous risk (before this work)

- After a successful commit, a failed FinanzOnline submit left **no durable state** anywhere; only a log line.
- Payments that were not sent externally **could not be queried**.
- There was **no marker** for retry or manual review.
- Duplicate-submit risk was limited to “do not retry”; now a Submitted row is not sent again on retry.

---

## 11. Remaining gaps

- **DailyClosing:** `TagesabschlussService` still writes FO status after submit with only try/catch. State is not improved from `Success`/`FailureKind` on the response (out of scope).
- **Fiscal export:** A payment-level FO status summary (count/percent) was not added to the export JSON. It can be added later under `integrity` or a separate field if needed.
- **Prometheus:** Metrics are currently in-memory plus the GET metrics endpoint. The same counters can be exposed if Prometheus scrape is added.

---

## 12. How to follow this in operations

1. **Metrics:** GET `/api/admin/finanzonline-reconciliation/metrics` → `submitTotal`, `submitFailedTotal`, `submitFailedTransient/Permanent/Unknown` (reset when the application restarts).
2. **List:** GET `/api/admin/finanzonline-reconciliation?status=Pending,Failed` for Pending/Failed rows.
3. **Alert (log):** Alert rule on "FinanzOnlineAlert" messages (Failed count threshold or register repeated failure). Optional: register an `IFinanzOnlineAlertSink` implementation (webhook, queue) to send events to an external system.
4. **Retry strategy:** The automatic job retries Pending rows with exponential backoff up to MaxRetryCount (default 5); then status becomes Failed. Manual retry is always available (POST retry/{paymentId}).
5. **Duplicate submit:** The job and manual retry do not resend Submitted rows; only Pending/Failed are selected / targeted.
