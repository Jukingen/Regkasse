# Ausfall / Wiederinbetriebnahme notification — design plan (P0-3)

**Date:** 2026-07-29  
**Action:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-3** (~12–16 person-days)  
**Dependency:** P0-1 (rkdb SOAP transport reuse) is preferred; can be developed in parallel with simulation  
**Related:** [`FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md`](FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md), [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md), [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> This document is a design plus operator runbook draft. **It is not legal advice**; if BMF primary sources conflict, they win. Do not enable automatic FON submit without Compliance approval.

---

## 1. Legal / BMF channel (summary)

### 1.1 What is reported?

The RKSV / FinanzOnline Registrierkassen-Webservice model supports notifying BMF of security-unit or cash-register **failure (Ausfall)**, **return to service (Wiederinbetriebnahme)**, and **permanent decommission (Außerbetriebnahme)**.

That is not the same as an internal “TSE Offline” log: a **formal FON record** is required.

### 1.2 Channel and form

| Channel | Form / operation | Note |
|---------|------------------|------|
| **Primary (automation)** | FinanzOnline **Registrierkassen-Webservice** — SOAP `rkdb` | WSDL: `https://finanzonline.bmf.gv.at/fonws/ws/regKasseService.wsdl` |
| **Manual (portal)** | FinanzOnline web UI — Registrierkassen / security-unit operations | Ops fallback |
| **File upload** | (optional) async package / DataBox protocol | Outside webservice; secondary in this P0 |

**BMF documents:**

- [Registrierkassen-Webservice PDF](https://www.bmf.gv.at/dam/jcr:19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf)
- [Handbuch Registrierkassen](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf)
- Hub: [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md)

### 1.3 Relevant `rkdb` elements

One `rkdb` package has **one operation type** (BMF rule). Typical Ausfall options:

| Element | Identity | Content (summary) |
|---------|----------|-------------------|
| `ausfall_se` | `zertifikatsseriennummer` | `ausfall` **or** `ausserbetriebnahme` (`begruendung` + `beginn_ausfall`) |
| `wiederinbetriebnahme_se` | `zertifikatsseriennummer` | `ende_ausfall` |
| `ausfall_kasse` | `kassenidentifikationsnummer` | same `ausfall` / `ausserbetriebnahme` |
| `wiederinbetriebnahme_kasse` | `kassenidentifikationsnummer` | `ende_ausfall` |

- `beginn_ausfall` / `ende_ausfall`: xs:dateTime; **must not be in the future** (Ausfall start).
- `satznr`, optional `kundeninfo`, package `paket_nr` + `ts_erstellung`.
- Session: Session-Webservice `login` first; `rkdbRequest` includes `tid`, `benid`, `id`, `art_uebermittlung` (`T`/`P`).

**Regkasse mapping (recommendation):**

| Event | Default rkdb type | Rationale |
|-------|-------------------|-----------|
| SCU/TSE device cannot sign; certificate known | **`ausfall_se`** | Signaturerstellungseinheit |
| Interruption / decommission at cash-register id | **`ausfall_kasse`** / `ausserbetriebnahme` | RegisterNumber = Kassen-ID |
| Primary back after failover | **`wiederinbetriebnahme_se`** (or kasse) | `ende_ausfall` |

Compliance must approve in writing the “how many minutes Offline = mandatory Ausfall” threshold (see §3.3).

### 1.4 Separate concept: Ausnahmezustand on the receipt

Beleg machine code / Besonderheit (`see-ausfall` and similar) is **receipt content**; it does not replace a FON `ausfall_se` record. They can complement each other. This plan focuses on **FON rkdb Ausfall/Wiederinbetriebnahme**.

---

## 2. Current state (detection exists, FON does not)

### 2.1 Detection and internal notification

| Component | What it does | FON? |
|-----------|--------------|------|
| `TseHealthCheckService` | Periodic probe; cached Online/Degraded/Offline | No |
| `TseFailoverBackgroundService` | `CheckAndFailoverAsync` + cert expiry for primaries | No |
| `TseFailoverService` | Automatic/manual failover, revert | No |
| `TseFailoverNotificationService` | Activity: `TseFailoverStarted/Activated/Failed/Reverted/…` | No (activity/email only) |
| `TseIncidentService` | Internal incident CRUD (`/admin/tse/incidents`) | No |
| FA `/rksv/incident` | Correlation-ID investigation (replay + audit + FO **reconciliation** rows) | No Ausfall enqueue |
| FA `/admin/tse/failover` | Failover ops | No FON |
| `FinanzOnlineSubmissionKind` | `Register` \| `SignatureUnit` | **No Ausfall** |

### 2.2 Capture points (hooks)

Recommended **single publish surface** for automatic enqueue:

```text
ITseAusfallEventPublisher  (new, thin)
  ← TseFailoverNotificationService.NotifyFailoverCompleted / Failed / Reverted
  ← TseHealthMonitor Offline transition (after debounce)
  ← Manual API (FA “Ausfall melden”)
  ← Cash register Schlussbeleg / decommission (ausserbetriebnahme — separate flow)
```

**Capture strategy:**

1. **Failover activated** (primary unhealthy → backup): candidate `ausfall_se` (old primary certificate) + optional register note.
2. **Revert to primary** / primary Online stable: candidate `wiederinbetriebnahme_se`.
3. **Offline duration ≥ threshold, no failover:** candidate Ausfall (Compliance threshold).
4. **Manual:** operator form + approval from FA.

Existing activity events stay as **source signals**. Do not write the FON outbox directly from Activity; use a central publisher (idempotency + debounce).

---

## 3. Notification mechanism (outbox)

### 3.1 Yes — new message type + handler

Reuse the existing `FinanzOnlineOutbox` infrastructure (retry, dead-letter, idempotency).

| Piece | Recommendation |
|-------|----------------|
| Message types | `RksvAusfallSeSubmission`, `RksvWiederinbetriebnahmeSeSubmission`, `RksvAusfallKasseSubmission`, `RksvWiederinbetriebnahmeKasseSubmission` (or one type + `Kind`) |
| Aggregate | `TseDevice` / `CashRegister` + `AusfallEpisodeId` |
| BusinessKey | `ausfall\|{tenant}\|se\|{certSerial}\|beginn\|{utc:o}` (blocks duplicate send) |
| Payload | `zertifikatsseriennummer` or `kassenidentifikationsnummer`, `begruendung`, `beginn_ausfall` / `ende_ausfall`, `satznr`, mode |
| XML | New builder: `FinanzOnlineRkdbAusfallXmlBuilder` (same pattern as belegpruefung builder) |
| Transport | **`SoapFinanzOnlineRegistrierkassenTransport`** + session (same as P0-1) — no new SOAP client |
| Handler | `RksvAusfallFinanzOnlineOutboxHandler` → map → `IFinanzOnlineRegistrierkassenClient.SubmitAsync` |
| Status table | `rksv_ausfall_finanz_online_submissions` (similar to Startbeleg FO submission row) |

### 3.2 Flow

```text
Trigger (auto/manual)
  → Debounce / policy gate (Demo/Soft → skip; Production lock OK)
  → Create episode row (Open)
  → Enqueue outbox (Pending)
  → Worker + session + rkdb ausfall_*
  → Submitted / Verified / Failed / ManualVerificationRequired
  → (recovery) Wiederinbetriebnahme enqueue (ende_ausfall)
  → Episode Closed
```

### 3.3 Automatic vs approved automatic (recommended policy)

| Mode | Behavior |
|------|----------|
| **`Ausfall:AutoEnqueue=false`** (default first release) | Activity + FA “pending Ausfall suggestion” only; enqueue when the operator **approves** |
| **`Ausfall:AutoEnqueue=true`** | Enqueue directly when the threshold is exceeded (after Compliance approval) |
| Demo / Soft / `TseMode=Off` | **Never** go to FON |

**Debounce:** for example Offline ≥ `AusfallGraceMinutes` (default 30, config) and still Offline → suggestion/enqueue. Short glitches are not reported.

**Begründung codes:** map to the BMF XSD/PDF reason field (Compliance fixed list + i18n FA select).

### 3.4 P0-1 dependency

- While transport is a skeleton: test the state machine with outbox + Fake/Simulation; real BMF TEST after P0-1.
- `RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED` → outbox retry/dead-letter (same classification as Startbeleg).

---

## 4. FA UI

### 4.1 Yes — add a card + list

| Place | Content |
|-------|---------|
| **`/admin/tse/failover`** or new **`/admin/tse/ausfall`** | Episode list: device, certificate, beginn/ende, FON status Tag, outbox link |
| **`/rksv/finanz-online-outbox`** | MessageType filter: Ausfall / Wiederinbetriebnahme |
| **`/rksv/incident`** | Link to FO Ausfall row when correlation exists (beside existing FO reconciliation) |
| **`/admin/tse-management`** | Device detail actions “Ausfall melden” / “Wiederinbetriebnahme” |
| Activity bell | New events: `TseAusfallReported`, `TseWiederinbetriebnahmeReported`, `TseAusfallEnqueueSuggested` |

### 4.2 Card fields (recommendation)

- Status: Suggested \| PendingApproval \| Submitted \| Verified \| Failed \| Closed
- Scope: SE vs Kasse
- `beginn_ausfall` / `ende_ausfall` (Vienna display)
- Begründung
- OutboxId → `/rksv/finanz-online-outbox?outboxId=`
- Actions: Approve & send, Retry, Mark manual (done in portal), Cancel suggestion

### 4.3 Permissions

- View: `finanzonline.view` or TSE admin
- Send / approve: `finanzonline.submit` (+ optional dual Super Admin in Production)

i18n: `tseAusfall.*` (de/en/tr). No hardcoded strings.

---

## 5. Operator documentation (runbook)

### 5.1 When is FON notification required?

Compliance checklist (example — **must be approved**):

1. The signature unit (SCU) cannot produce signatures for a long time and the legal Ausfall duration is exceeded.
2. Planned maintenance / card swap (Ausfall → then Wiederinbetriebnahme).
3. Permanent cash-register close → `ausserbetriebnahme` (clarify vs Schlussbeleg flow).
4. Short network outage + offline queue within limits → usually **no FON Ausfall** (internal incident is enough).

### 5.2 When an automatic suggestion appears

1. FA → **TSE Ausfall** list (or Activity “Enqueue suggested”).
2. Check device, certificate serial, `beginn_ausfall`.
3. Select Begründung.
4. **Approve & send** → outbox Pending.
5. Watch `/rksv/finanz-online-outbox` (retry / dead-letter).
6. If the BMF TEST/PROD return code is not Verified, open an incident; correct manually in the portal.

### 5.3 Manual trigger (FA)

1. `/admin/tse-management` → pick device.
2. **Ausfall melden** → form: SE/Kasse, Begründung, Beginn (default: detection UTC).
3. Confirm modal (strong warning in Production).
4. Send → outbox.
5. After recovery **Wiederinbetriebnahme** → `ende_ausfall` ≥ beginn.

### 5.4 Manual trigger (FinanzOnline portal — fallback)

1. Sign in to [FinanzOnline](https://finanzonline.bmf.gv.at/).
2. Registrierkassen / security-unit menu (current Handbuch path).
3. Fill the Ausfall / Wiederinbetriebnahme form.
4. Close the related episode in FA with **Mark manual (portal)**; evidence note + timestamp.

### 5.5 Monitoring

| Question | Where |
|----------|-------|
| Sent? | Outbox + episode status |
| Retry? | Outbox AttemptCount / NextAttemptAt |
| Internal failover? | `/admin/tse/failover` + Activity |
| Correlation investigation | `/rksv/incident?correlationId=` |

### 5.6 Do not

- Treat Demo/Soft as “Verified”.
- Double-enqueue the same `beginn_ausfall` + certificate (BusinessKey).
- Send Wiederinbetriebnahme without Ausfall (FON rejection risk).
- Log secrets/PINs.

### 5.7 Rollback

- Wrong Ausfall: Compliance + BMF process; software “cancel suggested” only for records not yet sent.
- Outbox DeadLetter: fix payload → manual re-enqueue (watch the idempotent key).

---

## 6. Implementation breakdown (P0-3)

| Phase | Work | Role | Person-days |
|-------|------|------|-------------|
| 0 | Compliance: threshold, Begründung list, auto vs approve | Compliance | 1–2 |
| 1 | Episode entity + migration + DTOs | Backend | 2 |
| 2 | XML builder + mapper + outbox types + handler | Backend | 3–4 |
| 3 | Hooks (failover notification + health debounce + manual API) | Backend | 2–3 |
| 4 | FA list/card/actions + i18n | Frontend | 3–4 |
| 5 | Simulation tests + BMF TEST (after P0-1) + Ops finalize this runbook | Backend / Ops | 2–3 |

**Total:** ~12–16 person-days.

### Acceptance criteria

- [x] At least `ausfall_se` + `wiederinbetriebnahme_se` XML + outbox handler (simulation/unit).
- [x] Failover activated → Suggested or Auto enqueue (config).
- [x] Demo/Soft → no FON enqueue.
- [x] FA status + manual trigger + outbox link (`/admin/tse/ausfall`).
- [ ] Operator runbook (§5) signed by Ops.
- [ ] BMF TEST Ausfall + Wiederinbetriebnahme round-trip (after P0-1).

---

## 7. Decision summary

| Question | Decision |
|----------|----------|
| Legal channel | FON **rkdb** `ausfall_*` / `wiederinbetriebnahme_*` (+ portal fallback) |
| Current detection | Failover/health **is captured**; **not** bound to FON — add a hook |
| Outbox? | **Yes** — new message type + handler; reuse transport |
| FA UI? | **Yes** — TSE Ausfall list + outbox + incident link |
| First-release auto? | Default **approved suggestion**; full auto after Compliance |

---

**Last updated:** 2026-07-29 — P0-3 **code complete** (`rksv_ausfall_episodes`, XML builder, failover hooks, FA). BMF E2E still open in Ops.
