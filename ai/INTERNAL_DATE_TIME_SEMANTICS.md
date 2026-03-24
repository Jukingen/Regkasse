# Internal: date/time semantics (Regkasse backend)

**Audience:** maintainers. **Not** end-user documentation.

## Concepts

| Concept | Meaning |
|--------|---------|
| **Instant** | A point in time (`timestamptz`); use `InstantToPersistUtc` for writes, UTC for queries unless binding Austria wall input. |
| **Calendar day (Austria)** | Business day in `Europe/Vienna`; ranges use half-open UTC `[from, to)` via `AustriaInclusiveCalendarRangeUtc` / `CalendarHalfOpenInstantBounds`. |
| **Vienna anchor** | One stored UTC instant per local calendar date at 00:00 Vienna (`ViennaCalendarAnchorToPersistUtc`), e.g. `DailyClosing.ClosingDate`. |

## Endpoints / code paths using **inclusive upper bound** on instants (intentional)

| Location | Semantics (short) |
|----------|-------------------|
| `ReportsController` when **no** `startDate`/`endDate` | **Rolling UTC window**; `<= endBoundUtc` where bound is `UtcNow` (inclusive “now”). |
| `AdminPaymentsController.GetList` default branch | **Rolling UTC window** last 30 days; `CreatedAt <= nowUtc`. |
| `AdminOperationsSummaryController.GetSummary` | **Rolling UTC window** `[fromUtc, toUtc]` on `AuditLog.Timestamp`; `toUtc = UtcNow`. |
| `OfflineIntentCoverageController` | **Observability UTC window**; `CreatedAtUtc >= from && <= to` (normalized query params). |
| `FiscalExportService` export period | **Caller-defined UTC instant window**; `IssuedAt` / `CreatedAt` in `[from, to]` inclusive. |
| `TagesabschlussService.GetClosingHistoryAsync` | **Discrete Vienna anchors** on `ClosingDate`; `<= toUtc` is start-of-last-day anchor (inclusive filter on anchor values, not continuous half-open). |
| `PaymentService` benefit validity | **Domain instant** “valid now” checks with `<= now` / `ValidTo >= now`. |
| `FinanzOnlineRetryHostedService` | **Scheduling instant** comparison vs backoff. |

Half-open **calendar** filters (prefer `< upperExclusive`): `CashRegisterController` transactions, `AuditLogService` date filters, `FinanzOnlineService.GetErrorsAsync`, `FinanzOnlineReconciliationController`, `AdminPaymentsController` when both dates set, `PaymentService.GetPaymentStatisticsAsync`, `ReportsController` when calendar range mode.

## Format helpers (`FormatViennaUtcInstantAs*`)

Non-UTC `DateTime.Kind` is normalized with `InstantToPersistUtc` before Vienna projection — **no** `ToUtcForNpgsql` (that is for Austria wall **query** binding).
