# FinanzOnline Sonderbeleg SOAP — Implementation Plan (P0-1)

**Date:** 2026-07-29  
**Goal:** Wire the `RksvFinanzOnlineSubmissionClient` skeleton to real BMF `rkdb` / `belegpruefung` submission  
**Source action:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-1**  
**Assessment:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> This document is a technical implementation plan. It is not legal/BMF compliance evidence on its own. If official sources conflict, BMF documents win (`docs/RKSV_OFFICIAL_SOURCES.md`).

---

## 1. Current SOAP skeleton structure

### 1.1 Files and roles

| Type | Class | Role |
|-----|--------|-----|
| Interface | `IRksvFinanzOnlineSubmissionClient` | Start/Jahres submit contract |
| Fake | `FakeRksvFinanzOnlineSubmissionClient` | No network; fake `Verified` / configurable fail |
| Legacy | `NotImplementedRksvFinanzOnlineSubmissionClient` | `NotImplementedException` |
| “Real” skeleton | `RksvFinanzOnlineSubmissionClient` | Config guard + **no network** → `RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED` |

Source: `backend/Services/FinanzOnlineIntegration/RksvFinanzOnlineSubmissionClient.cs`.

### 1.2 Methods on the interface

```csharp
Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(...);
Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(...);
```

Both drop into the same `SubmitCoreAsync(receiptKind, …)` path.

### 1.3 Current “Real” client flow

```text
Enabled == false?
  → RKS_SUBMISSION_DISABLED (+ ManualVerificationRequired)
ValidateEnabledOptions (EndpointUrl HTTPS, Timeout, credential/cert secret *references*)?
  → RKS_SUBMISSION_CONFIG_INCOMPLETE
otherwise
  → log (Production warning)
  → RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED   ← NO network call
```

`AllowOutboundNetworkCalls` is read, but **no HTTP/SOAP is performed** (stated explicitly in a comment).

### 1.4 Missing pieces (not in the skeleton)

| Missing | Description |
|-------|----------|
| Session acquire | FON Session-Webservice `login` → session `id` |
| Credential resolve | Read real tid/benid/password via `ParticipantCredentialsConfigurationKey` / `IFinanzOnlineCredentialProvider` |
| mTLS / client cert | `ClientCertificateSecretName` is validated only; not bound to HttpClient |
| Payload mapping | `QrPayload` → BMF `belegpruefung.beleg` (DEP/machine-code format) |
| RKDB XML generation | `FinanzOnlineRkdbBelegpruefungXmlBuilder` is not wired to this client |
| SOAP send | `rkdb` envelope POST |
| Response parse | return codes / `verificationResultList` → `RksvFinanzOnlineSubmissionResult` |
| Error classification | Transient vs permanent (aligned with codes the outbox handler uses) |
| `SubmitMonatsbelegAsync` | Not on the interface (P1-1 scope) |
| Mode alignment | Outbox enqueue still hardcodes `Mode=TEST` (P1-2) |

The outbox side (`RksvSpecialReceiptFinanzOnlineOutboxHandler`) already calls this client; the gap is **real transport**.

---

## 2. Official BMF WSDL / documentation

The repo does not embed a WSDL file. Primary BMF sources:

| Source | URL / location |
|--------|-------------|
| **WSDL — Registrierkassen-Webservice** | `https://finanzonline.bmf.gv.at/fonws/ws/regKasseService.wsdl` |
| **Endpoint (rkdb)** | typical: `https://finanzonline.bmf.gv.at/fonws/ws/rkdb` (confirm with WSDL/PDF) |
| **Namespace** | typical: `https://finanzonline.bmf.gv.at/rkdb` (config: `FinanzOnline:Registrierkassen:SoapNamespace`) |
| **BMF Registrierkassen-Webservice PDF** | [BMF_Registrierkassen_Webservice.pdf](https://www.bmf.gv.at/dam/jcr:19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf) |
| **Document version notes** | [BMF_Registrierkassen_Webservice_Dokumentenversion.pdf](https://www.bmf.gv.at/dam/jcr:075f0d4d-7df0-4661-9997-57a6e3b147ff/BMF_Registrierkassen_Webservice_Dokumentenversion.pdf) |
| **Handbuch Registrierkassen** | [BMF_Handbuch_Registrierkassen.pdf](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf) |
| **Session WSDL** | `https://finanzonline.bmf.gv.at/fonws/ws/session` (prod example: `FinanzOnline:Session:BaseUrl`) |
| **Hub** | [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md), BMF Registrierkassen start page |

**Operation (primary candidate for Sonderbeleg verification):** `rkdb` **`belegpruefung`** — single beleg, **synchronous** response (BMF PDF: Belegprüfung exception).  
Package content must match the XSD (`regKasse.xsd`); repo builder: `FinanzOnlineRkdbBelegpruefungXmlBuilder`.

**Note:** Startbeleg/Jahresbeleg are not “separate SOAP methods.” Both send maschinenlesbarer Code through the same `rkdb` + `belegpruefung`. Cash register/SCU **registration** (`registrierung_kasse` / `registrierung_se`) is a different operation type and already lives on `SoapFinanzOnlineRegistrierkassenTransport`.

---

## 3. Completion steps (recommended order)

### Step 0 — Contract clarity (Compliance + Backend, ~1 PD)

1. Confirm: do Startbeleg/Jahresbeleg go to FON via **`belegpruefung`**? (The BMF Webservice model points that way.)  
2. `beleg` field: Anlage Z12 maschinenlesbarer Code — **not** the QR wire (`{machineCode}_{compactJws}`); usually **machine code only**, or the exact format BMF requires.  
3. Lock return code → `Verified` / `Failed` / `ManualVerificationRequired` mapping with Compliance.

### Step 1 — Beleg text generation (Backend, ~2–3 PD)

1. `RksvFinanzOnlineSubmissionPayload.QrPayload` is usually `Receipt.QrCodePayload` = `{machineCode}_{jws}` (`RksvReceiptQrPayloadBuilder`).  
2. `FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate` pattern: `^(_[^_]+){13}$` — **full QR usually FAILs**.  
3. Fix: take `SignaturePipeline.TryGetMachineCodeFromCompactJws` from compact JWS **or** split the machine-code segment from the QR; run the validator; if needed, confirm with Ops that the certificate series / AES key is registered in FON (`benutzerschluessel` on cash register registration).

### Step 2 — Session (security model) (Backend + Ops, ~2 PD)

The FON rkdb path is not classic **WS-Security UsernameToken**. Existing architecture:

```text
Session SOAP login (tid / benid / pin)
  → session id
rkdbRequest: tid, benid, id (=session), art_uebermittlung (T|P), <rkdb>…
HTTPS POST + SOAPAction "rkdb"
```

Existing: `SoapFinanzOnlineSessionTransport`, session cache / `GetValidSessionAsync` (submission pipeline).  
**To do:** Have the RKSV client use the same session infrastructure (do not write a new WS-Security layer).

Optional: client certificate (`ClientCertificateSecretName`) — Ops binds it to the HttpClient handler per FON participant requirements.

### Step 3 — Build the SOAP request (Backend, ~2 PD)

1. Fill `FinanzOnlineRkdbBelegpruefungCommand` (`PaketNr`, `SatzNr`, `Beleg`, `TsErstellungUtc`, optional `Kundeninfo`).  
2. `FinanzOnlineRkdbBelegpruefungXmlBuilder.Build(ns, cmd)` → `<rkdb>…<belegpruefung>…`.  
3. Mapper: existing `DefaultFinanzOnlineCommandMapper` + `RkdbBelegpruefung` path is **reusable**.

### Step 4 — Send (Backend, ~1–2 PD)

`IFinanzOnlineRegistrierkassenClient.SubmitAsync` → (TEST gate) → `SoapFinanzOnlineRegistrierkassenTransport.SubmitAsync`:

- Envelope: `soapenv:Envelope` / `rkdbRequest`  
- Header: `SOAPAction: "rkdb"`  
- `art_uebermittlung`: TEST=`T`, PROD=`P`

### Step 5 — Handle the response (Backend, ~2–3 PD)

1. Map `ParseRkdbResponse` output to `RksvFinanzOnlineSubmissionResult`.  
2. Success: `Success=true`, `ExternalReference` (package/row/ref), `VerificationStatus=Verified` or the status BMF returns.  
3. Soft-fail / return code ≠ 0: `Failed` or `ManualVerificationRequired`.  
4. Network errors: `TRANSIENT_*` codes → outbox retry (align with `RksvSpecialReceiptFinanzOnlineOutboxHandler.ClassifyRksvClientFailure`).  
5. `RawResponseSnapshot`: truncated XML/JSON summary with no secrets.

### Step 6 — Rewire `RksvFinanzOnlineSubmissionClient` (Backend, ~3–4 PD)

```text
RksvFinanzOnlineSubmissionClient (Real)
  ├─ options + AllowOutboundNetworkCalls + cutover guard
  ├─ resolve mode (TEST/PROD) — together with P1-2
  ├─ build belegpruefung command from payload
  ├─ map via DefaultFinanzOnlineCommandMapper (or a thin wrapper)
  ├─ obtain session (existing session service)
  └─ IFinanzOnlineRegistrierkassenClient.SubmitAsync
       └─ SoapFinanzOnlineRegistrierkassenTransport
```

DI: inject `IFinanzOnlineRegistrierkassenClient` and session/credential dependencies into the Real client.  
Production: ban `ClientKind=Fake` (host startup or cutover guard).

### Step 7 — Outbox / Mode / cutover (Backend + Ops, ~2 PD)

1. P1-2: remove the hardcoded enqueue `Mode=TEST`.  
2. Run `docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` + `finanzonline-bmf-test-validation-runbook.md`.  
3. Fake vs Real indicator in FA (prevent false “Verified” confidence).

---

## 4. Reuse the existing transport?

### Decision: **Do not write a new SOAP transport — use existing `SoapFinanzOnlineRegistrierkassenTransport`**

| Question | Answer |
|------|--------|
| Need a new `HttpClient` + envelope? | **No** — rkdb envelope, SOAPAction, fault parse, and transient errors already exist |
| What to write? | Thin **orchestration** layer: payload → belegpruefung → session → `IFinanzOnlineRegistrierkassenClient` → result map |
| When a new transport? | Only if BMF requires a new endpoint/operation, or Sonderbeleg-specific behavior does not fit the existing transport (prefer to avoid) |

**Rationale:** `SoapFinanzOnlineRegistrierkassenTransport` is already designed for “WSDL: regKasseService.wsdl, operation rkdb”; it places `RkdbPayloadXml` / `belegpruefung` XML in the body. A parallel SOAP client creates drift and dual-maintenance risk.

**Session:** `SoapFinanzOnlineSessionTransport` — do not rewrite that either.

```text
Recommended dependency direction:

  RksvFinanzOnlineSubmissionClient
        → (session + mapper)
        → IFinanzOnlineRegistrierkassenClient
              → SoapFinanzOnlineRegistrierkassenTransport   ✅ reuse
```

---

## 5. Test strategy

### 5.1 Layers

| Layer | What | Environment |
|--------|-----|--------|
| **L0 Unit** | Machine-code split; validator; result mapping; config guards; Fake still green | CI, no network |
| **L1 Handler** | Outbox handler + mock `IRksvFinanzOnlineSubmissionClient` / mock registrierkassen | Extend existing `RksvSpecialReceiptFinanzOnlineOutboxHandlerTests` |
| **L2 Simulation** | `UseSimulation=true` — outbox state machine, FA UI | Dev; no BMF |
| **L3 BMF TEST** | `UseSimulation=false`, `EnableRealTestSubmission=true`, `art_uebermittlung=T` | FON TEST credentials; **single Startbeleg smoke** |
| **L4 PROD** | Cutover guard + dual approval; first Jahresbeleg controlled | Ops + Compliance signature |

### 5.2 Sandbox / simulation

1. **Default development:** Fake or `UseSimulation` — existing `SimulatedFinanzOnlineRegistrierkassenClient` / `FinanzOnlineDeveloperSimulationEngine`.  
2. **Dev smoke:** `FinanzOnlineDevTestController` enqueue-smoke (synthetic belegpruefung) — pipeline smoke, not rkdb evidence.  
3. **BMF TEST:** runbook [`docs/finanzonline-bmf-test-validation-runbook.md`](finanzonline-bmf-test-validation-runbook.md), E2E [`docs/release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md`](release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md).  
4. **WireMock / recorded SOAP:** Optional — redact real BMF response XML into golden-file parse tests (no BMF dependency in CI).

### 5.3 Security tests

- No pin/session/token in logs.  
- `RawResponseSnapshot` truncate + redaction.  
- Fake client fails startup in Production.  
- Real client opens no network when `AllowOutboundNetworkCalls=false` (feature flag).

### 5.4 Acceptance criteria (P0-1 done)

- [x] `ClientKind=Real` + Enabled + AllowOutbound + valid config → `IFinanzOnlineSubmissionService` / belegpruefung path (unit + mock)
- [x] Jahresbeleg uses the same `SubmitCoreAsync` path
- [x] Fake is rejected on a Production host via `ValidateOnStart` (`AllowFakeClientInProduction` escape hatch)
- [x] Unit + handler tests (CI, no network)
- [ ] At least one **Startbeleg** `belegpruefung` synchronous success on BMF TEST (Ops)
- [ ] Same path for **Jahresbeleg** on BMF TEST (Ops)
- [ ] Outbox: Pending → … → Verified visible in FA (Ops + cutover checklist)
- [x] Assessment / Action Plan “P0-1” code status updated

---

## 6. Risks and open items

| Risk | Mitigation |
|------|---------|
| QR ≠ `beleg` XSD pattern | Machine-code split + validator; verify with a real Startbeleg QR in E2E |
| AES `benutzerschluessel` missing / wrong in FON | Cash register registration (`registrierung_kasse`) as a prerequisite checklist |
| Session lifetime / concurrent outbox | Existing session cache + retry; stale Processing recover |
| False “Verified” (old Fake) | Prod Fake ban + FA environment label |
| Monatsbeleg scope | Out of this P0 → P1-1 |
| Ausfall | Separate P0-3; `ausfall_*` XML can be added to the same rkdb transport later (out of scope for now) |

---

## 7. Rough work breakdown (aligned with P0-1)

| Phase | PD (rough) |
|-----|-------------|
| Contract + beleg mapping | 3–4 |
| Client wiring + session/mapper reuse | 4–6 |
| Response map + error classification + Fake prod ban | 3–4 |
| TEST E2E + runbook + FA indicator | 5–7 |
| Buffer (BMF surprise / XSD) | 3–4 |
| **Total** | **~18–25** ([`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) P0-1) |

**Ownership:** Backend (lead), Ops (credentials/endpoint), Compliance (acceptance/return-code), Frontend (Fake/Real / submission status clarity — small).

---

## 8. First code touches (recommended files)

| File | Change |
|-------|------------|
| `RksvFinanzOnlineSubmissionClient.cs` | Orchestration; remove `SoapTransportNotImplemented` |
| `ApplicationHost.cs` | DI dependencies; Prod Fake guard |
| `FinanzOnlineRkdbBelegpruefungMapping.cs` / new helper | QR/JWS → beleg string |
| `RksvSpecialReceiptService.cs` | Ambient Mode (with P1-2) |
| `RksvFinanzOnlineSubmissionClientTests.cs` (new/expand) | Unit |
| FA receipt/outbox UI | Environment / client kind indicator |

**Reuse without rewrite:** `SoapFinanzOnlineRegistrierkassenTransport`, `SoapFinanzOnlineSessionTransport`, `FinanzOnlineRkdbBelegpruefungXmlBuilder`, outbox handler (error-code classification only if needed).

---

## 9. References

- Code: `RksvFinanzOnlineSubmissionClient.cs`, `SoapFinanzOnlineRegistrierkassenTransport.cs`, `FinanzOnlineRegistrierkassenInfrastructure.cs`, `RksvSpecialReceiptFinanzOnlineOutboxHandler.cs`  
- Docs: `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`, `finanzonline-bmf-test-validation-runbook.md`, `release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md`, `RKSV_CASH_REGISTER_OPERATIONS.md` §4, `ai/05_SECURITY_COMPLIANCE.md`  
- BMF: `regKasseService.wsdl`, Registrierkassen-Webservice PDF, Handbuch  

---

**Last updated:** 2026-07-29 — P0-1 **code complete** (Real client + beleg mapper + result/error classification + Fake prod `ValidateOnStart` + P1-2 Mode). BMF TEST/PROD E2E evidence remains open on the Ops/Compliance checklist.
