# TSE production configuration lock — design proposal (P0-2)

**Date:** 2026-07-29  
**Action:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-2** (~3–5 PD)  
**Related:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md), [`FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md`](FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md)

> Goal: When `ASPNETCORE_ENVIRONMENT=Production`, **fail-closed** block Soft/Fake/Off TSE from producing “looks legal” signatures.
>
> **Status (2026-07-29):** ✅ Implemented — `TseProductionOptionsValidator` (`ValidateOnStart`), `TseFiscalConfigHealthCheck` (`/health/tse/mode`), escape hatch `Tse:AllowUnsafeFiscalModesInProduction`, FA banner via `GET /api/rksv/environment` lock fields.

---

## 1. Current state

### 1.1 Two separate axes (`TseOptions`)

Source: `backend/Models/TseOptions.cs` (`SectionName = "Tse"`).

| Property | Values | Meaning |
|----------|--------|---------|
| **`TseMode`** | `Off` \| `Demo` \| `Device` | Payment / QR policy |
| **`Mode`** | `Fake` \| `Real` | Closing / provider signature backend (`ITseProvider`) |
| **`Provider`** | `fiskaly` \| `epson` \| `swissbit` \| `fake` \| `soft` | Real (or soft) vendor |
| **`Environment`** | e.g. `Production` / `Test` | Vendor API label (informational) |

Helpers:

- `IsOff` → `TseMode == Off` (TSE off; `tseRequired` ignored; NON_FISCAL QR)
- `UseSoftTseWhenNoDevice` → `TseMode == Demo` (Soft TSE when no device)
- `IsFakeSigningMode` → `Mode == Fake` (simulated JWS without hardware)
- `AllowSimulatedDailyClosing` → allow daily closing only with fake/simulated provider (dev safety valve)

Defaults (code): `TseMode=Device`, `Mode=Real`.  
Prod example (`appsettings.Production.example.json`): `TseMode=Device`, `Mode=Real`, `Provider=fiskaly`.

### 1.2 `TseMode` / `Mode` are not enums

These are **string** fields; there is no separate C# `enum TseMode`. Comparison is `OrdinalIgnoreCase`.

### 1.3 Second config surface: `RKSV:*`

`RksvEnvironmentService` also reads:

- `RKSV:Mode` (Demo / Production)
- `RKSV:TseMode` (`Simulation` → `IsTseSimulated() == true`)

DEP / FA “DEMO” labels come from here. **The Production lock must apply not only to `Tse:*` but also to `RKSV:TseMode=Simulation` and `RKSV:Mode=Demo`.** Runtime overlay (database `rksv_runtime_config`, FA `/admin/rksv/config`) is evaluated with the same lock — Demo/Simulation on Production/Staging → HTTP 409 unless the escape hatch is on. See [`RKSV_RUNTIME_CONFIG.md`](RKSV_RUNTIME_CONFIG.md).

### 1.4 Runtime behavior

| Component | Role |
|-----------|------|
| `PaymentService` | `effectiveTseRequired`; signature requirement drops when Off |
| `TseService` / `SignaturePipeline` | Signature production |
| `ApplicationHost` DI | `IsFakeSigningMode` → `FakeTseProvider`; otherwise Real + fiskaly/soft |
| `TseProvisioningService` | Skip provision when Off; Demo/Fake can look “ready” |
| `TseCachedHealthCheck` | Device probe cache; Offline → **Degraded** (does not drop LB ready) |

**Gap today:** If Production is configured with `TseMode=Off` / `Demo` or `Mode=Fake`, the API **does not reject at startup**; fiscal operations can fall onto soft/fake signature or an unsigned path.

---

## 2. Critical control — block Fake / Off (and Demo) in Production

### 2.1 Recommended primary mechanism: `IValidateOptions<TseOptions>` + `ValidateOnStart`

Existing pattern: `BackupOptionsValidator` (`ValidateOnStart` → unsafe config → **process start fail**).

```text
TseProductionOptionsValidator : IValidateOptions<TseOptions>
  + IHostEnvironment
  + IConfiguration (for RKSV:*)

host.IsProduction() && !AllowUnsafeFiscalModesInProduction?
  TseMode in { Off, Demo }           → Fail
  Mode == Fake                       → Fail
  Provider in { fake, soft }          → Fail (Device expected)
  AllowSimulatedDailyClosing == true → Fail
  RKSV:TseMode == Simulation         → Fail
  RKSV:Mode == Demo                  → Fail (Production host)
  Provider=fiskaly && SCU/Api empty  → Fail (or separate Unhealthy — below)
```

Registration (`ApplicationHost`, same style as Backup):

```csharp
services.AddSingleton<IValidateOptions<TseOptions>, TseProductionOptionsValidator>();
services.AddOptions<TseOptions>()
    .Bind(configuration.GetSection(TseOptions.SectionName))
    .ValidateOnStart();
```

**Why startup validation (not middleware)?**

| Approach | Plus | Minus |
|----------|------|-------|
| **ValidateOnStart** | Pod never takes traffic; fail-closed; consistent with Backup | Cannot loosen via config hot-reload (intentional) |
| Middleware | Per-request block | Process stays up with bad config; partial endpoint leak risk |
| Payment gate only | Payments blocked | Closing / Sonderbeleg / provision can still be on the soft path |

**Recommendation:** Startup is **required**; optional second defense on the payment path (defense in depth) — not required.

### 2.2 Environment definition

- Trigger: `IHostEnvironment.IsProduction()` (`ASPNETCORE_ENVIRONMENT=Production`).
- **Staging:** Same strict rules as Production by default (recommendation). Optional `Tse:EnforceProductionLockInStaging=true` (default true).
- **Development / Test:** Soft/Demo/Fake allowed (do not break existing tests).

### 2.3 Emergency escape hatch (narrow)

Similar to FON cutover, **default off**:

```json
"Tse": {
  "AllowUnsafeFiscalModesInProduction": false,
  "UnsafeFiscalModesApprovalToken": null
}
```

- `AllowUnsafeFiscalModesInProduction=true` **only** with an explicit dual-approval token (Ops runbook); every startup **Critical** log + Activity `TseUnsafeProductionModeEnabled`.
- This flag must **never** be true in normal prod; checklist item.

Middleware alone is not enough; even the escape hatch is evaluated inside ValidateOnStart.

---

## 3. Health check (readiness)

### 3.1 Today’s `/health/ready`

```csharp
Predicate = check => check.Tags.Contains(DatabaseHealthCheck.ReadyTag)
// Degraded → 200, Unhealthy → 503
```

TSE is **not included**. `TseCachedHealthCheck` (`deps` tag) is the device Online/Offline cache; Offline → Degraded → does not drop ready.

### 3.2 Recommendation: config lock ≠ device probe

| Endpoint | What it checks | Prod Fake/Off |
|----------|----------------|---------------|
| `/health/live` | Process up | Unaffected |
| `/health/ready` | DB (current) | Process already gone via config ValidateOnStart |
| **`/health/tse/mode`** (new) | Fiscal config posture | Unhealthy (503) |
| `/health/tse` or existing `tse` deps | Device probe cache | Degraded (keep traffic; fiscal path adapts) |

**Add config Unhealthy into `/health/ready`?**

- **For:** Orchestrator cuts a bad image before traffic (even if ValidateOnStart is skipped).
- **Against:** Inflates Ready; breaks DB-only ready semantics.

**Design decision (recommended):**

1. Startup `ValidateOnStart` = primary fail-closed.
2. New `TseFiscalConfigHealthCheck` (name: `tse-fiscal-config`) → **`/health/tse/mode`** (parallel to FON `/health/finanzonline/mode`, Backup `/health/backup/mode`).
3. **Do not add** to `/health/ready` for now — optional later phase: `ReadyTag` + Unhealthy only for config (device Offline still Degraded).

Temporary device Offline must **not** drop ready (keep current `TseCachedHealthCheck` policy).

### 3.3 Fiskaly credentials on readiness?

- Missing `SignatureCreationUnitId` / ApiKey: Fail at startup **or** `tse-fiscal-config` Unhealthy.
- Recommendation: Production + `Provider=fiskaly` → SCU id + key secret reference required (value not logged).

---

## 4. Error handling

### 4.1 Layered behavior

| Situation | Behavior |
|-----------|----------|
| Production + forbidden mode, normal flags | **Startup abort** (`OptionsValidationException`); container CrashLoop → deploy rollback |
| Production + escape hatch open | Start **allowed**; Critical log + audit/activity; `/health/tse/mode` = **Degraded** (or Unhealthy per policy) |
| Development + Off/Demo/Fake | Normal; log Information |
| Runtime options change (rare) | `IOptionsMonitor` + health check catch on next probe; Production must **Validate**-block a hot-reload into unsafe |

### 4.2 Logging (English, no secrets)

```text
Critical: TSE production lock rejected TseMode={TseMode} Mode={Mode} Provider={Provider} RksvTseMode={RksvTseMode}
```

- Never log ApiKey / ApiSecret / raw PEM.
- Structured: fixed `EventId` (for example `TseProductionConfigRejected`).

### 4.3 API surface (if process is up — hatch or bug)

- Fiscal payment / Sonderbeleg: existing signature requirement + extra guard `ITseFiscalProductionGate.EnsureAllowed()` → 503 `TSE_UNSAFE_PRODUCTION_CONFIG`.
- FA diagnostics: `GET /api/rksv/environment` already returns Demo/Simulated; extend: `fiscalConfigLock: { ok, reasons[] }`.

### 4.4 Do not

- Silently fall back to Soft TSE.
- Treat Production `TseMode=Off` payments as “successful non-fiscal”.
- Rely on UI warning only (backend unlocked).

---

## 5. Admin warning (FA)

### 5.1 Yes — banner required (defense in depth + operator visibility)

Even with a backend lock, FA should show:

| Signal | Source | UI |
|--------|--------|-----|
| Demo / Simulated | `GET /api/rksv/environment` (`isSimulated`, environment) | Persistent top banner (RKSV hub + layout) |
| Fiscal config lock fail | New field or `/health/tse/mode` (Super Admin) | Red Alert: “Production TSE lock: …” |
| Provider Soft/Fake (Staging hatch) | environment DTO | Orange “nicht fiskal” |

### 5.2 Placement

1. **Global (Super Admin / Mandanten-Admin fiscal pages):** `RksvEnvironmentBanner` — strengthen the existing DEMO label.
2. **`/admin/tse-management`:** Config posture row (`TseMode`, `Mode`, `Provider`, lock OK/FAIL).
3. **i18n:** `tseManagement.productionLock.*` (de/en/tr) — no hardcoded strings.

### 5.3 Banner copy (German example)

> **Produktive TSE-Konfiguration unsicher.** TseMode/Demo/Off oder Mode=Fake ist in Production nicht zulässig. Signaturen sind nicht rechtsgültig. Bitte Ops/Compliance kontaktieren.

### 5.4 Permission

- Detailed config: `system.critical` / TSE admin.
- “You are in a demo environment” summary: all FA users with fiscal permission (to break false confidence).

---

## 6. Implementation plan (short)

| Step | Work | Role | PD |
|------|------|------|-----|
| 1 | `TseProductionOptionsValidator` + `ValidateOnStart` + unit tests | Backend | 1–1.5 |
| 2 | `TseFiscalConfigHealthCheck` + `/health/tse/mode` | Backend | 0.5–1 |
| 3 | Extend `RksvEnvironmentStatusDto` + OpenAPI | Backend | 0.5 |
| 4 | FA banner + tse-management indicator + i18n | Frontend | 1–1.5 |
| 5 | `appsettings.Production.example.json` + Ops checklist item | Ops / Docs | 0.5 |

**Total:** ~3–5 PD (aligned with the action plan).

### Test strategy

- Unit: Production host mock → Off/Demo/Fake → `ValidateOptionsResult.Fail`; Device+Real+fiskaly → Success.
- WebApplicationFactory: Production env + unsafe config → host build throws.
- Development + Demo → Success.
- FA: simulated environment → banner visible (component test).

---

## 7. Acceptance criteria (P0-2 done)

- [ ] Production `Tse:TseMode=Off|Demo` or `Tse:Mode=Fake` → process does not start.
- [ ] Production `RKSV:TseMode=Simulation` / `RKSV:Mode=Demo` → does not start.
- [ ] `Provider=fake|soft` + Production → does not start (Device expected).
- [ ] `/health/tse/mode` returns Unhealthy (without hatch; after ValidateOnStart the pod is normally gone).
- [ ] FA Demo/Simulated / lock-fail banner (i18n).
- [ ] Development tests (Demo/Fake) stay green.
- [ ] Escape hatch documented + default `false`.

---

## 8. Decision matrix summary

| Question | Decision |
|----------|----------|
| How to block? | **`IValidateOptions` + `ValidateOnStart`** (primary); not middleware |
| `/health/ready`? | Keep DB; **separate** `/health/tse/mode` for config |
| Device Offline ready? | No — Degraded (current) |
| FA banner? | **Yes** |
| Escape hatch? | Narrow, default off, audit + Critical log |

---

**Last updated:** 2026-07-29 — P0-2 design proposal.
