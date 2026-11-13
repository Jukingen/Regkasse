# FinanzOnline automatic retry and alerting

## Changed files

| File | Change |
|------|--------|
| `backend/Options/FinanzOnlineRetryJobOptions.cs` | Config: Interval, MaxRetryCount, BaseDelaySeconds, BackoffCapSeconds, BatchSize, AlertFailedThreshold, RegisterRepeatedFailureThreshold, Enabled. Namespace: `KasseAPI_Final.Configuration`. |
| `backend/Services/FinanzOnlineMetrics.cs` | `IFinanzOnlineMetrics` (IncrementSubmitTotal, IncrementSubmitFailed(kind), GetSnapshot), `IFinanzOnlineAlertSink`, `NoOpFinanzOnlineAlertSink`. |
| `backend/Services/FinanzOnlineRetryHostedService.cs` | Background job: pending retry with exponential backoff, max retry, mark max-retries-exceeded as Failed, emit alerts. |
| `backend/Services/PaymentService.cs` | Optional `IFinanzOnlineMetrics`; IncrementSubmitTotal/IncrementSubmitFailed on create and retry paths. |
| `backend/Controllers/FinanzOnlineReconciliationController.cs` | GET `metrics` endpoint; ctor `IFinanzOnlineMetrics`. |
| `backend/Program.cs` | Register FinanzOnlineRetryJobOptions, IFinanzOnlineMetrics, IFinanzOnlineAlertSink, FinanzOnlineRetryHostedService. |
| `backend/appsettings.json` | `FinanzOnlineRetryJob` section. |
| `docs/release/FINANZONLINE_RECONCILIATION.md` | Sections 5, 8, 9, 10, 11, 12 updated. |

---

## Retry strategy

- **What is retried:** Only rows with `FinanzOnlineStatus == "Pending"` and `FinanzOnlineRetryCount < MaxRetryCount` (default 5) whose backoff window has elapsed.
- **Backoff:** `BaseDelaySeconds * 2^RetryCount` seconds, capped at `BackoffCapSeconds` (for example 3600). After the first attempt, the second is at earliest BaseDelaySeconds later.
- **Outcome:** Success → `Submitted`. Transient fail → `Pending` (retry count increases). Permanent/Unknown fail → `Failed`. After MaxRetryCount is reached and one more fail occurs, status becomes `Failed` and the error text gets "(Max retries exceeded)."
- **Duplicate submit:** The job selects Pending only. `RetryFinanzOnlineSubmitAsync` does not call the external API if already Submitted; duplicate-submit risk is unchanged.

---

## How Ops monitors

1. **Metrics:** `GET /api/admin/finanzonline-reconciliation/metrics` (FinanzOnlineView) → `submitTotal`, `submitFailedTotal`, `submitFailedTransient`, `submitFailedPermanent`, `submitFailedUnknown`. Reset on process restart.
2. **List:** `GET /api/admin/finanzonline-reconciliation?status=Pending,Failed` for pending and failed rows.
3. **Log alert:** Log lines containing "FinanzOnlineAlert": Failed count over threshold, or repeated failure on the same cash register. Bind a log-based alert rule to those messages.
4. **Alert sink (optional):** If an `IFinanzOnlineAlertSink` implementation (webhook, queue, and similar) is registered, `OnFailedCountThresholdExceeded` and `OnRegisterRepeatedFailure` are called. Default: `NoOpFinanzOnlineAlertSink`.
5. **Disable the job:** `FinanzOnlineRetryJob:Enabled: false` turns off automatic retry; only manual retry remains.
