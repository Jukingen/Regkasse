# FinanzOnline TEST mode — end-to-end operational verification

> **Status:** Release verification document. Legacy `POST /api/FinanzOnline/submit-invoice` references are historical/deprecated and not preferred.

Technical runbook (English). This document describes how to **observe** the current FinanzOnline **TEST** pipeline (outbox → session → rkdb → protocol query) with this codebase. It is **not** a legal/compliance sign-off and does **not** assert production readiness.

## A. Current trigger path for TEST submissions

### Primary (production-shaped)

1. **POS payment commit** (`PaymentService` after DB commit): when `effectiveTseRequired` is true (`request.Payment.TseRequired && !TseOptions.IsOff`), the service calls `IFinanzOnlineService.SubmitInvoiceAsync(createdInvoice)` and updates payment FinanzOnline reconciliation fields.
2. **`FinanzOnlineService.SubmitInvoiceAsync`** builds a `FinanzOnlineRegisterSubmissionRequest` with:
   - **Mode:** `ResolveMode(config.Environment)` — `FinanzOnlineConfig.Environment` defaults to **`"Test"`** and is **not** loaded from `CompanySettings` today (`GetConfigAsync` does not map an environment column). So submissions stay in **TEST** unless the in-memory config is changed elsewhere.
   - **Scope:** `RegisterId` from invoice (`KassenId` or cash register id string).
   - **Correlation:** `BusinessKey` = invoice number or invoice id hex; `PayloadHash` = SHA-256 of serialized payload; `CorrelationId` = invoice id hex.
   - **RKDB belegpruefung (optional):** `TryResolveRkdbBelegpruefungAsync` loads the receipt for `invoice.SourcePaymentId` and, if `QrCodePayload` matches the DEP candidate validator (`FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate`), supplies `FinanzOnlineRkdbBelegpruefungCommand` for **TEST** rkdb mapping. Typical RKSV **QR** text is **not** the same as a DEP line; in many flows **belegpruefung will be null** unless the receipt stores a DEP-shaped string.
3. **Outbox enqueue:** `IFinanzOnlineOutboxService.EnqueueSubmissionAsync` persists `FinanzOnlineOutboxMessage` with JSON payload (`FinanzOnlineOutboxPayload`).

### Secondary (reconciliation / retry)

- **`PaymentService.RetryFinanzOnlineSubmitAsync`**: if the payment is not already `Submitted`, loads `Invoice` by `SourcePaymentId` and calls `SubmitInvoiceAsync` again. **Idempotency:** identical aggregate + payload hash yields the **same** outbox row (no duplicate enqueue).

### Not the primary outbox path

- **`POST /api/FinanzOnline/submit-invoice`** (`FinanzOnlineController`) is marked **deprecated** and uses a **legacy simulated** path — it does **not** exercise `SubmitInvoiceAsync` / outbox enqueue as implemented for invoices.

## B. What was missing for “real” end-to-end verification

- **Reliable `belegpruefung` from live data:** only when `Receipt.QrCodePayload` passes **`IsValidDepCandidate`**; otherwise the outbox still enqueues for **TEST** but **real** SOAP submission (`UseSimulation: false`) may lack **`RkdbPayloadXml`** (see `FinanzOnlineSubmissionService` / mapper).
- **Single-button enqueue (optional):** a **development-only** gated endpoint exists (`POST /api/admin/finanzonline-dev-test/enqueue-smoke`) — see **Path 0** below. The **primary** production-shaped path remains **payment + invoice** (or retry).
- **Distinguish two meanings of “real”:**
  - **In-process full pipeline (simulation):** default `UseSimulation: true` on FinanzOnline session, rkdb client, and transmission query — **no BMF network**; still exercises outbox state machine and admin UI.
  - **BMF TEST SOAP (live test environment):** requires `UseSimulation: false`, credentials in `CompanySettings`, `FinanzOnline:Registrierkassen:EnableRealTestSubmission`, valid rkdb XML for real TEST submit, and `FinanzOnline:TransmissionQuery:EnableRealTestQuery` for protocol reconciliation — **outside** this document’s default local setup.

## C. Changes implemented

- **`FinanzOnlineDevTestOptions`** + **`FinanzOnlineDevTestSmoke.BuildSyntheticDepBeleg()`** — `backend/Services/FinanzOnlineIntegration/`.
- **`FinanzOnlineDevTestController`**: `POST /api/admin/finanzonline-dev-test/enqueue-smoke` — **Development host only**, requires `FinanzOnline:DevTest:AllowEnqueueSmokeTest: true` in `appsettings.Development.json` (or user secrets), permission **FinanzOnlineSubmit**. Enqueues **TEST** mode with `aggregateType = DevTestSmoke` and a **valid synthetic** `belegpruefung` (no secrets in code). Returns **404** if flag is false or environment is not Development.
- **Configuration:** `appsettings.Development.json` includes `FinanzOnline:DevTest:AllowEnqueueSmokeTest` (default **false**).

## D. Exact TEST execution steps (local / test environment)

### Preconditions

1. **API running** with valid DB and secrets (see `backend/CONFIGURATION.md`).
2. **`FinanzOnlineOutbox:Enabled`** (default `true` in options) so `FinanzOnlineOutboxHostedService` runs.
3. **Note:** `SubmitInvoiceAsync` does **not** check `CompanySettings.FinanzOnlineEnabled` — that flag is used elsewhere (e.g. status/UI). The payment path enqueues whenever `effectiveTseRequired` is true.
4. **Permissions:** POS payment uses your existing auth; admin outbox uses **FinanzOnlineView** (see `FinanzOnlineOutboxAdminController`); dev enqueue uses **FinanzOnlineSubmit**.

### Path 0 — Development-only synthetic enqueue (no POS payment)

1. Set **`FinanzOnline:DevTest:AllowEnqueueSmokeTest`** to **`true`** in `appsettings.Development.json` (or user secrets), **Development** only.
2. Obtain a JWT for an admin user with **`finanzonline.submit`**.
3. `POST http://localhost:5183/api/admin/finanzonline-dev-test/enqueue-smoke` with header `Authorization: Bearer <token>` and optional body `{ "registerId": "DEV-RK-SMOKE" }`.
4. Expect **200** with `outboxMessageId`, `aggregateId`, `businessKey`. If **404**, the host is not Development or the flag is false.
5. Follow logs / DB / admin UI as in **Path 1** (same outbox worker). **Aggregate filter:** `aggregateType=DevTestSmoke`.

### Path 1 — Full in-process pipeline (simulation, recommended for smoke)

1. Configure **simulation** (typical defaults):
   - `FinanzOnline:Session:UseSimulation` = **true**
   - `FinanzOnline:Registrierkassen:UseSimulation` = **true**
   - `FinanzOnline:TransmissionQuery:UseSimulation` = **true**
2. Perform a **payment** through the normal POS/API path with **`TseRequired: true`** and TSE not **Off**, so `PaymentService` reaches `SubmitInvoiceAsync` after commit (see `PaymentService` — if TSE is strict hardware mode, use dev **Fake** / **soft TSE** settings per your `appsettings.Development.json`).
3. **Logs:** search for `FinanzOnline outbox enqueued`, then `FinanzOnline outbox processed`, then reconciliation logs for `AwaitingProtocol` → `ProtocolSuccess` (simulated query returns a terminal status such as `Submitted` / success mapping).
4. **Admin UI:** open **frontend-admin** `/rksv/finanz-online-outbox`, filter **Mode = TEST**, refresh — expect status transitions ending in **ProtocolSuccess** (or **ManualReview** / failures if you intentionally break config).

### Path 2 — Real BMF TEST SOAP (optional, operator/network dependent)

1. Set **simulation flags to false** and point SOAP URLs to FinanzOnline TEST endpoints; store **TEST** credentials in `CompanySettings` (never commit secrets).
2. Set `FinanzOnline:Registrierkassen:EnableRealTestSubmission` = **true** and ensure the mapper produces **`RkdbPayloadXml`** (requires valid **belegpruefung** / receipt DEP data — see `FinanzOnlineSubmissionService` when not in simulation).
3. Set `FinanzOnline:TransmissionQuery:EnableRealTestQuery` = **true** for the **status_kasse** reconciliation path after submit.
4. Execute the same **payment** path as Path 1; verify SOAP and outbox in logs and admin.

### Path 3 — Retry-only (existing invoice)

- Use **`POST /api/admin/finanzonline-reconciliation/retry/{paymentId}`** (permission **FinanzOnlineSubmit**) only when an invoice exists for that payment and status allows retry. Same **idempotency** rules apply.

## E. Expected outbox state transitions

Typical **simulated** happy path:

| Stage | Outbox status (approx.) | Notes |
|--------|-------------------------|--------|
| Enqueued | `Pending` | Row created; idempotent duplicate returns same row. |
| Worker claims | `Processing` | |
| RK submit success + transmission id | `AwaitingProtocol` | `TransmissionId` set; payload updated with `RkdbTsErstellungIso` / `RkdbSatzNr` when returned. |
| Protocol query reconciles | `ProtocolSuccess` | Simulated query returns success + status mapped to terminal success. |

If submit fails: `RetryableFailure` / `PermanentFailure` / etc. per `FinanzOnlineOutboxHostedService` classification.

**SQL (PostgreSQL):** `SELECT id, status, mode, aggregate_type, business_key, transmission_id, correlation_id, created_at FROM finanz_online_outbox_messages ORDER BY created_at DESC LIMIT 20;`

## F. Expected admin UI observations

- **Route:** frontend-admin **`/rksv/finanz-online-outbox`** (list).
- **Filter:** **Mode = TEST**; for Path 0, filter or search **`aggregateType = DevTestSmoke`** or **`businessKey`** prefix `dev-smoke-`.
- **Detail:** open row → **status** transitions from **Queued** → **Processing** → **Awaiting protocol** → **Protocol success** (labels from `FinanzOnlineOutboxAdminController` mapping).
- **API parity:** `GET /api/admin/finanzonline-outbox` and `GET /api/admin/finanzonline-outbox/{id}` — no raw credentials/XML in responses.

## G. What is verified now (by following this runbook)

- **Code path linkage:** payment → `SubmitInvoiceAsync` → outbox enqueue → background worker → submission service → (simulated or real) session + rkdb + protocol query → DB status updates; **or** Path 0 → direct `EnqueueSubmissionAsync` → same worker pipeline.
- **Operational visibility:** logs (`FinanzOnline outbox enqueued`, worker processing, protocol reconciliation) + admin outbox list/detail + **TEST** mode filter in UI.
- **PROD mode** is **not** required and must remain blocked unless cutover guard explicitly allows it (`FinanzOnlineService.ResolveMode`).

## H. What still remains unproven / assumed

- **`FinanzOnlineEnabled` vs enqueue:** the code path above can enqueue outbox rows even when the “FinanzOnline disabled” business flag is false — align product expectations separately if needed.
- **Legal / regulatory compliance** of payloads, timing, or BMF responses — **not** claimed here.
- **Path 2 (real SOAP)** depends on **your** credentials, network, BMF TEST availability, and valid RKDB XML — must be validated in your environment.
- **`FinanzOnlineConfig.Environment`** is **not** persisted via `GetConfigAsync` / `UpdateConfigAsync`; PROD-like behavior is **not** driven from DB in the current mapping — only relevant if you add storage later.
- **Idempotent retry** does not create a **new** outbox message for identical payload; troubleshooting “stuck” rows may require DB or process inspection rather than repeated API retry.
