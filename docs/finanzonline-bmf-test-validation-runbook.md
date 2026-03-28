# FinanzOnline — BMF TEST validation runbook

Operational checklist for **live BMF TEST SOAP** (not in-process simulation). Based on backend paths: `FinanzOnlineService.SubmitInvoiceAsync` → `IFinanzOnlineOutboxService` → `FinanzOnlineOutboxHostedService` → `IFinanzOnlineSubmissionService` / `TestModeFinanzOnlineRegistrierkassenClient` → optional `TestModeFinanzOnlineTransmissionQueryClient`.

---

## 1. Purpose

Confirm that staging (or a dedicated test host) can:

- Enqueue a **TEST** `RegistrierkassenSubmission` outbox message.
- Process it with **real** session + rkdb SOAP against BMF TEST.
- When required, complete **transmission / protocol reconciliation** via the real TEST query client.

This runbook does **not** sign off production (PROD) or legal compliance.

---

## 2. Preconditions

- PostgreSQL migrated; `finanz_online_outbox_messages` exists.
- API process can reach BMF TEST endpoints from the network (egress, TLS, allowlists).
- BMF TEST **credentials** are provisioned (values stored outside this doc).
- A trigger exists: **POS payment** with `effectiveTseRequired` (see `PaymentService` + `TseOptions`), **or** admin retry `PaymentService.RetryFinanzOnlineSubmitAsync`, **or** (Development only) `POST /api/admin/finanzonline-dev-test/enqueue-smoke` per `docs/release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md`.
- For payment-shaped runs: receipt data must allow **RKDB belegpruefung** when real XML is required — `Receipt.QrCodePayload` must pass `FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate` (`FinanzOnlineService.TryResolveRkdbBelegpruefungAsync`). Otherwise expect worker failures such as `RKDB_XML_PAYLOAD_REQUIRED`.

---

## 3. Required configuration

Set these **exact section names** (no secret values here — use env / secrets store with `__` nesting as in `backend/CONFIGURATION.md`).

| Area | Section / root | Required values (conceptual) |
|------|----------------|------------------------------|
| Session transport | `FinanzOnline:Session` | `UseSimulation` = **false**; `BaseUrl`, `SoapNamespace`, timeouts set; credentials via `DefaultCredential` or company merge (below). |
| Registrierkassen (rkdb) | `FinanzOnline:Registrierkassen` | `UseSimulation` = **false**; `EnableRealTestSubmission` = **true**; `BaseUrl`, `SoapNamespace`, `SoapAction`, timeout set. |
| Transmission query | `FinanzOnline:TransmissionQuery` | `UseSimulation` = **false**; `EnableRealTestQuery` = **true** (needed when outbox stays `AwaitingProtocol` and worker reconciles). |
| Credential source | `FinanzOnline:Connectivity` | `UseCompanySettings` = **true** *or* false with full `FinanzOnline:Session:DefaultCredential` (and scoped rows if used). |
| Company row (if `UseCompanySettings`) | `CompanySettings` (DB) | Populate **field names only**: `FinanzOnlineEnabled`, `FinanzOnlineApiUrl`, `FinanzOnlineUsername`, `FinanzOnlinePassword`, `FinanzOnlineTelematikId`, `FinanzOnlineHerstellerId` — all required by your BMF TEST onboarding. |
| Outbox worker | `FinanzOnlineOutbox` | `Enabled` = **true**; tune `PollInterval`, `MaxAttempts`, backoff if needed. |
| PROD guard | `FinanzOnline:CutoverGuard` | For TEST-only runs: keep **PROD** blocked (`AllowProdMode` false is typical); not used when mode stays TEST. |
| Retry job (optional) | `FinanzOnlineRetryJob` | `Enabled` as needed; retries **re-call** `SubmitInvoiceAsync` / outbox idempotency — does not replace outbox evidence. |

**Mode:** `FinanzOnlineConfig.Environment` defaults to **`Test`** in `GetConfigAsync` (no `CompanySettings` column maps it today) → `FinanzOnlineIntegrationMode.TEST` for invoice submit.

---

## 4. Exact execution steps

High-level sequence (aligned with **`docs/finanzonline-pilot-test-execution-plan.md`** global steps **G1–G5** and scenario **S/F/R**):

1. Apply configuration; restart API.
2. Confirm startup is not simulating all layers: check logs category **`FinanzOnline.TransportStartup`** (and any extended FO startup diagnostics if present) — expect `Session.UseSimulation=false`, `Registrierkassen.UseSimulation=false`, `TransmissionQuery.UseSimulation=false`, `EnableRealTestSubmission=true`, `EnableRealTestQuery=true`.
3. **Optional smoke (not success proof):** `POST /api/FinanzOnline/test-connection` — only valid when transports are **not** simulated; still **not** proof of rkdb success.
4. Trigger enqueue (payment path, reconciliation retry, or dev smoke per preconditions).
5. Wait for **`FinanzOnlineOutboxHostedService`** poll cycles (`FinanzOnlineOutbox:PollInterval`).
6. If outbox shows `AwaitingProtocol`, wait for **`ReconcileOneAsync`** (same worker loop) and `EnableRealTestQuery=true`.

For **step numbers, roles (OP/DEV), per-step log/DB/API checks, failure variants F-A/B/C, and retry procedure**, use the execution plan — this section stays the canonical **BMF TEST** intent summary.

---

## 5. How to verify success

Treat **all** of the following as primary evidence:

| Evidence | PASS condition |
|----------|----------------|
| **Logs** | `FinanzOnline outbox enqueued` → `FinanzOnline TEST submission start` / `finished` with **Success=true** (see `TestModeFinanzOnlineRegistrierkassenClient`) → `FinanzOnline outbox processed` with terminal **Status** acceptable below. |
| **DB** `finanz_online_outbox_messages` | Row reaches **`ProtocolSuccess`** OR a documented acceptable terminal for your test case; `Mode` = `TEST`; `LastErrorCode` / `FailureCategory` empty or non-fatal per ops policy. |
| **Admin API** | `GET /api/admin/finanzonline-outbox` / `GET /api/admin/finanzonline-outbox/{id}` show the same terminal status and error fields. |
| **Protocol query logs** | If `TransmissionId` was set: log line from `TestModeFinanzOnlineTransmissionQueryClient` with query outcome supporting transition to `ProtocolSuccess`. |

**Secondary (do not use alone):** `GET /api/admin/finanzonline-reconciliation` — payment `FinanzOnlineStatus` may remain **`Pending`** after enqueue (`FinanzOnlineSubmitResponse.Success` is false for queued path); that is **not** a failure of SOAP if outbox succeeded.

---

## 6. Known failure modes

| Signal | Likely cause | Action |
|--------|----------------|--------|
| `TEST_REAL_SUBMISSION_DISABLED` | `EnableRealTestSubmission` false | Set `FinanzOnline:Registrierkassen:EnableRealTestSubmission` = true. |
| `TEST_REAL_QUERY_DISABLED` | `EnableRealTestQuery` false | Set `FinanzOnline:TransmissionQuery:EnableRealTestQuery` = true. |
| Any transport still simulated | `UseSimulation` true | Set all three `UseSimulation` to false (Session, Registrierkassen, TransmissionQuery). |
| `SESSION_REQUIRED` / login errors | Bad credentials or `BaseUrl` | Fix `CompanySettings` or `DefaultCredential`; verify BMF TEST account. |
| `RKDB_XML_PAYLOAD_REQUIRED` / RKDB validation | No valid DEP beleg from receipt QR | Use receipt with `QrCodePayload` passing `IsValidDepCandidate`, or dev synthetic enqueue. |
| Stuck `Pending` outbox | Worker off | `FinanzOnlineOutbox:Enabled` = true; confirm process running. |
| Stuck `AwaitingProtocol` | Query disabled or BMF delay | Enable real test query; wait; inspect `LastErrorCode` on outbox row. |
| `DeadLetter` / `PermanentFailure` | Business validation or max attempts | Read `last_error_message` / admin outbox detail; fix payload or credentials; re-drive with new idempotency context if applicable. |

---

## 7. What does NOT count as success

- **`GET /api/FinanzOnline/status`** showing “connected” or green — reflects **SOAP session probe cache** and/or TSE-oriented fields; **not** rkdb/outbox completion.
- **`POST /api/FinanzOnline/test-connection`** success — **diagnostic only** (`IFinanzOnlineAdminConnectivityService.RunTestConnectionAsync`).
- **`PaymentDetails.FinanzOnlineStatus` = `Submitted`** after outbox enqueue — current enqueue path returns **not** `Success` on submit; **`Submitted` here is not the outbox PASS signal** (see `PaymentService.UpdatePaymentFinanzOnlineStateAsync`).
- **Simulation defaults** (`UseSimulation: true`) with “everything green” in UI — **no BMF network proof**.
- **`POST /api/FinanzOnline/submit-invoice`** — deprecated, **legacy simulated** path; not the outbox invoice pipeline.

---

## 8. Rollback / re-enable simulation

1. Set `FinanzOnline:Session:UseSimulation` = **true** (and/or Registrierkassen / TransmissionQuery) — restores in-process sim clients per `Program.cs`.
2. Set `FinanzOnline:Registrierkassen:EnableRealTestSubmission` = **false** and `FinanzOnline:TransmissionQuery:EnableRealTestQuery` = **false** to block real TEST rkdb/query paths.
3. Optionally `FinanzOnlineOutbox:Enabled` = **false** to stop worker (queues will backlog — ops decision).
4. Restart API; confirm `FinanzOnline.TransportStartup` (or equivalent) shows simulation flags **true** / real-test flags **false**.

---

## 9. Source-of-truth references

| Topic | Location |
|-------|----------|
| End-to-end TEST paths (incl. dev smoke, simulation vs BMF) | `docs/release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md` |
| Admin UI vs reconciliation vs outbox | `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md` |
| Outbox worker + statuses | `backend/Services/FinanzOnlineIntegration/FinanzOnlineOutbox.cs` (`FinanzOnlineOutboxHostedService`, `FinanzOnlineOutboxStatuses`) |
| Real TEST rkdb gate | `backend/Services/FinanzOnlineIntegration/FinanzOnlineRegistrierkassenInfrastructure.cs` (`TestModeFinanzOnlineRegistrierkassenClient`) |
| RKDB XML / belegpruefung | `backend/Services/FinanzOnlineIntegration/SimulatedFinanzOnlineAdapters.cs` (`FinanzOnlineSubmissionService`), `backend/Services/FinanzOnlineService.cs` (`TryResolveRkdbBelegpruefungAsync`) |
| Admin outbox API | `backend/Controllers/FinanzOnlineOutboxAdminController.cs` — `GET /api/admin/finanzonline-outbox` |
| Payment-row reconciliation API | `backend/Controllers/FinanzOnlineReconciliationController.cs` — `GET /api/admin/finanzonline-reconciliation` |
| Configuration entry points | `backend/Program.cs` (FinanzOnline options + hosted services), `backend/CONFIGURATION.md` |
| Pilot step-by-step execution (S/F/R, G-steps, evidence YAML) | `docs/finanzonline-pilot-test-execution-plan.md` |
| Per-run evidence record templates (success / failure / retry) | `docs/finanzonline-pilot-evidence-pack-template.md` |
| Short operator/developer checklist | `docs/finanzonline-pilot-operator-checklist.md` |
| Blocker review (after evidence packs) | `docs/finanzonline-pilot-blocker-review-template.md` |
| Pilot GO / NO-GO gate | `docs/finanzonline-pilot-go-no-go-gate.md` |

---

## 10. Execution plan (pilot dry runs)

**Document:** `docs/finanzonline-pilot-test-execution-plan.md`

### When to read **this runbook** (`finanzonline-bmf-test-validation-runbook.md`)

- Enabling or auditing **live BMF TEST** (vs simulation): required config keys, what counts as PASS (§5), what does **not** count (§7), known signals (§6), rollback (§8).
- Answering “did rkdb / protocol reconciliation succeed?” against BMF TEST.

### When to use the **execution plan**

- Running a **repeatable pilot dry run**: global **G1–G5**, then **success (S1–S4)**, **failure (F1–F3)** with variant **F-A / F-B / F-C**, **retry (R1–R4)**.
- Capturing **evidence** per step (logs, SQL fields, admin API URLs).

### Source of truth (submission state)

- **Runtime data** for “where is this invoice in the FO pipeline?”: **`finanz_online_outbox_messages`** and **`GET /api/admin/finanzonline-outbox`** (see also `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md`).
- **These Markdown docs** do not replace the database: they define **how to validate** and **how to execute** tests. **PASS rules** for BMF TEST are authoritative in this runbook **§5–§7**; **procedure** is authoritative in the execution plan for step order and checks.

### Evidence artifacts

Fill one record per scenario using `docs/finanzonline-pilot-evidence-pack-template.md`. Use `docs/finanzonline-pilot-operator-checklist.md` for a condensed pass/fail sheet.

After runs: complete **`docs/finanzonline-pilot-blocker-review-template.md`** (gap table + §3 focused PASS/FAIL), then **`docs/finanzonline-pilot-go-no-go-gate.md`** (staging → BMF TEST → pilot customer).
