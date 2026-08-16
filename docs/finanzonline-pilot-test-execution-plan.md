# FinanzOnline pilot — executable test plan

Step-by-step procedure paired with **`docs/finanzonline-bmf-test-validation-runbook.md` §10** (navigation + PASS rules §5–§7). Evidence templates: **`docs/finanzonline-pilot-evidence-pack-template.md`**. Short checklist: **`docs/finanzonline-pilot-operator-checklist.md`**. Post-run: **`docs/finanzonline-pilot-blocker-review-template.md`**, **`docs/finanzonline-pilot-go-no-go-gate.md`**.

Derived from `PaymentService`, outbox worker, `FinanzOnlineReconciliationController`, and `FinanzOnlineRetryHostedService`.

**Roles:** **OP** = operator (admin UI / API calls / DB read-only). **DEV** = developer (config, log access, API host restart).

---

## Global run order (once per environment or after config change)

| Step | Who | Action | Exact check | Evidence to capture |
|------|-----|--------|-------------|---------------------|
| G1 | DEV | Apply FO + outbox + retry config per runbook §3 (`FinanzOnline:*`, `FinanzOnlineOutbox`, `FinanzOnlineRetryJob`, `CompanySettings`). | Config keys present; secrets not in git. | Redacted config snapshot or env list. |
| G2 | DEV | Restart API process. | Process healthy; health endpoint if used. | Timestamp of restart. |
| G3 | DEV/OP | Logs: category **`FinanzOnline.TransportStartup`** (and any `FinanzOnlineTransportStartupDiagnostics`). | `Session.UseSimulation`, `Registrierkassen.UseSimulation`, `TransmissionQuery.UseSimulation`, `EnableRealTestSubmission`, `EnableRealTestQuery` match pilot intent (real TEST vs simulation). | Copy log lines. |
| G4 | OP | Optional: `POST /api/FinanzOnline/test-connection` (auth + `FinanzOnline` permissions). | HTTP 200 body; note runbook §7 — **not** rkdb success proof. | Response JSON snippet. |
| G5 | OP | Preconditions: open cash register, valid test customer/cart, POS user can pay with **`tseRequired: true`** when fiscal test is required. | TSE path allows signature (`Tse` options + `TseDevices` as per environment). | Register id, `TseMode` note. |

Then run **S** (success), **F** (failure), **R** (retry) in that order unless F/R reuse S artifacts.

---

## 1) Success case — execution script

**Goal:** One payment commits with receipt; invoice enqueues to outbox; worker reaches acceptable terminal (pilot: usually `ProtocolSuccess` on real TEST).

| Step | Who | Action | Log check | DB check | API check |
|------|-----|--------|-----------|----------|-----------|
| S1 | OP | `POST /api/pos/payment` with valid body + idempotency header if used. | `Payment created successfully` … `Invoice {InvoiceId}`; if TSE required: `TSE signature generated for payment {PaymentId}`. | `payment_details` row exists; `receipts.payment_id` = payment; `invoices.source_payment_id` = payment. | HTTP 201; note `paymentId`. |
| S2 | OP | Note **`invoice_id`** from DB (`invoices` for that `source_payment_id`). Compute **`correlation_id`** = `invoice_id` as **32-char hex no dashes** (`ToString("N")`). | `FinanzOnline outbox enqueued MessageType=RegistrierkassenSubmission AggregateType=Invoice … CorrelationId={correlation_id}` (`FinanzOnlineOutboxService`). | `finanz_online_outbox_messages`: row with `aggregate_type='Invoice'`, `aggregate_id=invoice_id`, `message_type='RegistrierkassenSubmission'`, `correlation_id` matches. | `GET /api/admin/finanzonline-outbox?correlationId={correlation_id}` returns ≥1 item. |
| S3 | OP | Wait ≥ 1× `FinanzOnlineOutbox:PollInterval` (+ extra if `AwaitingProtocol`). | `FinanzOnline TEST submission start … CorrelationId=…` and `FinanzOnline TEST submission finished Success=…` (`FinanzOnlineRegistrierkassenInfrastructure`); then `FinanzOnline outbox processed Id=… Status=… CorrelationId=…`. If transmission: transmission query client logs per runbook §5. | Same outbox row: terminal `status` (e.g. `ProtocolSuccess`); `mode` matches environment (`TEST` for BMF TEST). | `GET /api/admin/finanzonline-outbox/{id}` — status + error fields match DB. |
| S4 | OP | Cross-check payment FO columns (**secondary**). | `FinanzOnline submit failed` should **not** appear for a clean success path unless enqueue returned failure. | `payment_details`: `finanz_online_*` updated; **may still be `Pending`** after enqueue — not alone a failure if outbox terminal is success (runbook §5). | `GET /api/admin/finanzonline-reconciliation?status=Pending` may still list row — expected per runbook. |

**Operator note (OP):** Primary “did BMF pipeline finish?” = **outbox admin**, not green status tile alone.  
**Developer note (DEV):** If outbox `ProtocolSuccess` but payment stays `Pending`, treat as **known decoupling** unless product asks for projection fix.

---

## 2) Failure case — execution script

**Goal:** Failure is **observable** without rolling back payment/receipt; outbox and/or payment show error.

Pick **one** trigger (document which):

| Variant | Who | Action | Log check | DB check | API check |
|---------|-----|--------|-----------|----------|-----------|
| F-A | DEV | Set `FinanzOnline:Registrierkassen:EnableRealTestSubmission` = **false**; restart (G2–G3). | `TEST_REAL_SUBMISSION_DISABLED` or submission finished `Success=False` with disabled message. | Outbox row may move to `RetryableFailure` / terminal with error code per runbook §6. | Outbox list/detail shows error. |
| F-B | OP | New payment whose receipt QR **does not** satisfy RKDB DEP candidate (`RKDB_XML_PAYLOAD_REQUIRED` path). | Worker / submission error logs referencing RKDB / payload. | `last_error_code` / `last_error_message` on outbox row. | Admin outbox detail. |
| F-C | DEV | Invalid TEST credentials / session (temporary). | Session / submission error logs. | Outbox + optional `finanz_online_errors`. | `GET /api/FinanzOnline/errors` last rows. |

| Step | Who | Action | Log check | DB check | API check |
|------|-----|--------|-----------|----------|-----------|
| F1 | OP | `POST` payment (same as S1). | Payment success logs; FO may warn after commit. | Payment + invoice + receipt **still committed**. | HTTP 201. |
| F2 | OP | Wait for worker cycle. | `FinanzOnline outbox processed …` with non-success terminal OR `TEST submission finished Success=False`. | Outbox row terminal or `RetryableFailure` with populated error fields. | `GET /api/admin/finanzonline-outbox?correlationId=…`. |
| F3 | OP | Confirm observability. | No silent drop: at least one of payment FO update, outbox error, or `FinanzOnlineErrors` row. | `payment_details.finanz_online_status` / `finanz_online_error` if `UpdatePaymentFinanzOnlineStateAsync` ran. | Reconciliation list optional. |

**Operator note (OP):** A **201 payment** can coexist with FO failure — instruct staff not to void sale solely because admin shows FO error until ops procedure says so.  
**Developer note (DEV):** Revert F-A config after test; document F-B data setup for repeatability.

---

## 3) Retry case — execution script

**Goal:** Drive `RetryFinanzOnlineSubmitAsync` via admin or hosted job; observe outbox / payment updates without duplicate external semantics violations.

| Step | Who | Action | Log check | DB check | API check |
|------|-----|--------|-----------|----------|-----------|
| R1 | OP | Choose `payment_id` with outbox row in retryable state **or** `payment_details.finanz_online_status` in `Pending`/`Failed` (not `Submitted` if you expect re-enqueue). | — | Baseline: `finanz_online_retry_count`, outbox `status`, `attempt_count`. | `GET /api/admin/finanzonline-outbox?correlationId={invoice:N}`. |
| R2 | OP | `POST /api/admin/finanzonline-reconciliation/retry/{payment_id}` (permission `FinanzOnlineSubmit`). | Audit / payment logs for retry attempt; optional `FinanzOnline retry cycle` if job also enabled. | `finanz_online_retry_count` incremented when path calls `UpdatePaymentFinanzOnlineStateAsync` with `isRetry: true`. Outbox: same idempotency may return existing message — `attempt_count` may change after worker. | HTTP 200 body `FinanzOnlineRetryResponse`. |
| R3 | OP | Wait worker interval; re-query outbox. | `FinanzOnline outbox processed …` if new attempt ran. | Terminal or updated `attempt_count`. | `GET /api/admin/finanzonline-outbox/{id}`. |
| R4 | OP | Idempotency check: if payment already **`Submitted`** in DB, retry should **not** call external submit again (`RetryFinanzOnlineSubmitAsync` early return). | Verify no unexpected second `TEST submission start` for same business case if policy is no-op. | `FinanzOnlineStatus` still `Submitted`; `finanz_online_retry_count` behavior per code path. | Retry response `Success=true` without new outbox row (possible). |

**Operator note (OP):** Retry is **admin** action; track `payment_id` from POS receipt / payments list.  
**Developer note (DEV):** If `FinanzOnlineRetryJob` is on, document overlap with manual retry (metrics + audit noise).

---

## 4) Outbox vs payment state — explicit consistency checks

Run after **S** and after **F**:

| Check | Query / action | Pass if |
|-------|----------------|---------|
| O1 | Outbox terminal = `ProtocolSuccess` | Runbook §5 satisfied for pilot. |
| O2 | `payment_details.finanz_online_status` | If still `Pending` while O1 pass — **document as expected** (enqueue returns non-success); do **not** block pilot on O2 alone. |
| O3 | If payment shows `Failed` but outbox `ProtocolSuccess` | **Inconsistency** — investigate projection / retry job / stale update (known class of bugs). |
| O4 | If outbox `DeadLetter` and payment `Submitted` | **Inconsistency** — investigate. |

---

## 5) Admin operator clarity — quick checks

| Step | Action | Pass if |
|------|--------|---------|
| A1 | Open Report Center / outbox UI (or API) with filter by `correlationId`. | Operator can find the row without developer. |
| A2 | Compare `GET /api/FinanzOnline/status` vs outbox terminal. | Operator understands status is **connectivity/diagnostic**, not submission completion (runbook §7). |
| A3 | Read `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md` if present. | UI copy matches “outbox primary” policy. |

---

## Evidence capture

Use **`docs/finanzonline-pilot-evidence-pack-template.md`** — separate YAML blocks for SUCCESS, FAILURE, and RETRY (expected vs actual fields).

---

## Summary

| Block | Run order |
|-------|-----------|
| Global | G1 → G5 |
| Success | S1 → S4 |
| Failure | Pick F-A/B/C → F1 → F3 |
| Retry | R1 → R4 |
| Consistency | O1–O4 after S/F |
| Clarity | A1–A3 |

Exact log strings referenced: `FinanzOnline.TransportStartup`, `FinanzOnline outbox enqueued`, `FinanzOnline TEST submission start/finished`, `FinanzOnline outbox processed`, `Payment created successfully`, `TSE signature generated for payment`, `FinanzOnline retry cycle` (job).
