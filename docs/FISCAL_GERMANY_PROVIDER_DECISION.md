# Germany KassenSicherheit provider decision (Paket 20)

**Last updated:** 2026-09-21  
**Status:** Decision record only. **Implementation is NOT STARTED.** This is not a legal opinion and does not claim KassenSichV, DSFinV-K, or BSI TR-03153 compliance.  
**Hub:** [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) · [`COUNTRIES.md`](COUNTRIES.md) §16 · Cutover: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)

Do **not** merge this module into Austrian `Tse:` / SIGN AT. Do not sign DE payments until this package is implemented and Compliance signs off.

---

## 1. Recommendation

**Primary provider: fiskaly SIGN DE (cloud TSS), API v2.**

Regkasse’s production POS is a **single shared UI** (`pos.regkasse.at`) with the API on `api.regkasse.at`. A USB stick at a physical till does not fit that model. fiskaly already operates **SIGN AT** for Austrian RKSV (`rksv.fiskaly.com`). SIGN DE is a **different product and host** (`kassensichv.fiskaly.com`) with a different resource model (TSS / client / tx, not SCU). Reuse the **vendor relationship and ops muscle**, not the AT config block or FA pages.

**Hardware (Epson USB TSE / Swissbit USB TSE) is deferred.** Treat them as an optional later adapter for a dedicated on-prem DE till that cannot use cloud TSS — not for the SaaS POS path.

Swissbit **Cloud TSE 2** is a credible cloud alternative (BSI-listed). Do not dual-run it in v1. Keep `IKassenSicherheitService` pluggable so a second provider can land without forking `PaymentService`.

---

## 2. Comparison

Sources checked 2026-09-21: BSI TR product list, fiskaly SIGN DE certification pages, Epson USB TSE product page, Swissbit USB TSE reseller specs. **List prices change; request a current partner quote before a contract.** Values below are operational, not an offer.

| Criterion | **fiskaly SIGN DE** | **Epson DE (USB TSE)** | **Swissbit DE (USB TSE)** |
|-----------|---------------------|-------------------------|---------------------------|
| **KassenSichV / BSI** | Cloud TSS **fiskaly sign Cloud-TSE 1.0.15-1.5.0**. Certificate **BSI-K-TR-0717-2025** (issued 2025-03-31, listed valid through **2033-03-30**), TR-03153. LIVE v2 since 2025-08-24. | USB token. **EPSON USB TSE 1.1.1** is Swissbit USB TSE **rebrand** (**BSI-K-TR-0626-2024**, listed valid through **2031-07-07**). 5-year signature-cert lifetime from manufacture. | USB / SD / microSD. **Swissbit USB TSE** on the BSI TR list (e.g. v1.1 / 1.1.1). Same family as Epson rebrand. **Swissbit Cloud TSE 2** is a separate cloud product (BSI-K-TR-0781-2025) — not this USB column. |
| **API model** | **Cloud TSS (not an SCU).** REST: auth → create/initialize **TSS** (one per location) → **client** (POS / ERS, ≤199 per TSS) → **transaction** start (`ACTIVE`) / finish (`FINISHED`) with `tx_revision`. DSFinV-K export API. | **Local device.** USB to PC or **EPS TSE-Server** (up to 8 modules). Epson SDK / Windows driver. No public multi-tenant REST comparable to SIGN DE. Needs a **local agent** beside Expo POS. | **Local device** (USB/SD) via Swissbit SDK, same hardware generation as Epson. Cloud TSE 2 would be REST/SaaS — out of v1 scope. |
| **Fits `pos.regkasse.at`** | Yes. Signing is HTTPS from the API host. | Poor for SaaS. Stick lives at the till; tablets/shared UI have no USB TSE. TSE-Server is extra on-prem hardware. | Same as Epson for USB. Cloud TSE 2 would fit SaaS but is a second vendor. |
| **Sandbox** | Yes. TEST: `https://kassensichv-middleware.fiskaly.com/api/v2`. LIVE: `https://kassensichv.fiskaly.com/api/v2`. New orgs start in TEST; not billed as LIVE. | No cloud sandbox. Need a physical token + vendor test guidance. | USB: physical token. Cloud TSE 2: vendor TEST (not evaluated here). |
| **Pricing (indicative)** | SaaS: partner quote (typically per TSS + volume). TEST org free. Recertification is vendor-side (2033 horizon). | Capex USB SKU (Epson art. 7112348, “5 years”) + 3-year standard warranty / 20M signatures cap. No per-tx cloud fee. Replacement when cert/signature cap ends. | Capex USB/SD similar 5-year / 20M signature envelope. Cloud TSE 2: SaaS quote. |
| **Integration effort** | **Medium.** New HTTP client (do **not** reuse `IFiskalyTseService` / SIGN AT). Map start/finish tx to checkout. DSFinV-K export. Admin PIN / TSS init. Timeout policy (tx 3–5s; unsigned tx still recorded — DE Ausfall rules differ from AT FON). | **High** for this architecture: native SDK, Windows/agent, device inventory, USB failure UX, no shared-host signing. | **High** for USB (same as Epson). Cloud TSE 2 would be medium (new client). |
| **Ops overlap with AT** | Same dashboard family ([dashboard.fiskaly.com](https://dashboard.fiskaly.com)); **different organization product (SIGN DE vs SIGN AT)**. Separate API keys. | None. | None for USB. |

**AT vs DE fiskaly (do not confuse):**

| | SIGN AT (live today) | SIGN DE (this decision) |
|--|----------------------|-------------------------|
| Host | `https://rksv.fiskaly.com/api/v1` | TEST `kassensichv-middleware.fiskaly.com/api/v2` · LIVE `kassensichv.fiskaly.com/api/v2` |
| Resource | SCU + cash register | TSS + client + transaction |
| Config | `Fiskaly:` + `Tse:Provider=fiskaly` | **`KassenSicherheit:` only** |
| FA | `/admin/tse/fiskaly/*` | **New** DE surfaces (below) |

---

## 3. Phased implementation

Do not skip phases. Staging **and** Production reject `Provider=fake` and `AllowSimulatedTse=true` (`CountryFiscalLockEvaluator`). SIGN DE **TEST** is a real vendor API, not `fake`.

| Phase | Environment | What happens | Exit criteria |
|-------|-------------|--------------|---------------|
| **0. Contract** | — | Partner quote, DPA, BSI cert pack on file. Separate SIGN DE org from SIGN AT. | Legal + Finance sign-off. |
| **1. Sandbox** | Development + SIGN DE TEST | HTTP client, TSS/client fixtures, start/finish tx, DSFinV-K sample. Flag on for a **dev** DE tenant only. | Unit/integration tests; no LIVE keys in git. |
| **2. Staging** | `ASPNETCORE_ENVIRONMENT=Staging`, SIGN DE TEST | Staging host, DE tenant, POS checkout against TEST TSS. Ready probe includes DE lock. | Super Admin smoke: signed TEST tx, export download. AT CountryBaseline still green. |
| **3. Pilot** | Production host, SIGN DE **LIVE**, **one** canary DE mandant (`Deployment:CanaryTenantIds`) | Live TSS + clients for that mandant’s registers. DSFinV-K retained. | Compliance + Ops sign-off; 14-day soak; no AT fiscal regression. |
| **4. Production** | Remaining DE mandants | Provision TSS per location; clients per cash register. | Runbook + FA health green; rollback = flag off + stop new DE sales. |

**Do not** run this cutover in Austrian blackout windows ([`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) §1) — the shared API still restarts.

Rollback: `Fiscal.KassenSicherheitDe` tenant override **false**; `KassenSicherheit:Provider=not-configured`. Leave TSS rows in DB (additive). Do not drop columns. Do not fall back to AT TSE.

---

## 4. Config section changes

Keep the stub root **`KassenSicherheit`**. Do **not** put SIGN DE keys under `Fiskaly:` (that block is SIGN AT).

Planned keys (implement in a later coding package; names are the contract):

```json
"KassenSicherheit": {
  "Provider": "fiskaly-de",
  "AllowSimulatedTse": false,
  "Environment": "TEST",
  "ApiBaseUrl": "https://kassensichv-middleware.fiskaly.com/api/v2",
  "ApiKey": "",
  "ApiSecret": "",
  "AdminPin": ""
}
```

| Key | Production | Notes |
|-----|------------|--------|
| `Provider` | `fiskaly-de` | Allowed: `not-configured` (stub), `fiskaly-de`. Reject `fake` outside Development. Future: `swissbit-usb` / `epson-usb` only with a local agent (not SaaS v1). |
| `AllowSimulatedTse` | `false` | Lock already enforces this. |
| `Environment` | `LIVE` | `TEST` on Staging/Dev. Mirror SIGN AT: org+keys select LIVE vs TEST; still set the URL explicitly. |
| `ApiBaseUrl` | LIVE host | Do not reuse `Fiskaly:ApiBaseUrl`. |
| `ApiKey` / `ApiSecret` | Secret store | Distinct from SIGN AT keys. |
| `AdminPin` | Secret store | TSS admin operations (TR-03153-TS). Never log. |

`IKassenSicherheitService` today is `SignAsync` only. The coding package must grow **StartTransaction / FinishTransaction / ExportDsfinvk** so the DE tx lifecycle is not smashed into a single AT-style sign call.

---

## 5. Migration needs

Additive only. **Do not** store DE TSS signatures in Austrian `payment_details.tse_signature` as if they were RKSV JWS. COUNTRIES.md: DE chain must not reuse the AT RKSV chain table as the same legal instrument.

| Change | Why |
|--------|-----|
| `kassen_sicherheit_tss` | Per-tenant (per location) TSS id, serial, state, provider, environment. |
| `kassen_sicherheit_clients` | Maps `cash_registers.id` → SIGN DE `client_id`. |
| `kassen_sicherheit_transactions` | `tx_id`, revisions, state, signature payload, certificate serial, error, `payment_details_id` (nullable FK). |
| Optional `cash_registers.kassen_sicherheit_client_id` | Fast lookup; still tenant-scoped. |

No backfill from AT fiskaly SCU ids. DE tenants created before this package have no TSS until provisioned.

EF: new migration after `20260921180000_AddFiscalDocumentCountryAtIssueSnapshots`. Expand-then-code ([`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md)).

---

## 6. Audit events

`AuditEventType` today ends at `TenantCountryChangedHistoricalPreserved = 98` and `Other = 99`. **Do not insert before `Other`.** Next coding package adds values **≥ 100**.

Proposed (names only; not in code yet):

| Name | When |
|------|------|
| `KassenSicherheitTssInitialized` | TSS created + admin PIN + INITIALIZED |
| `KassenSicherheitClientRegistered` | Client bound to a cash register |
| `KassenSicherheitTransactionSigned` | Finish tx with signature |
| `KassenSicherheitTransactionUnsigned` | Timeout / TSS down; sale recorded without signature (DSFinV-K rules) |
| `KassenSicherheitExportGenerated` | DSFinV-K (or TAR) export |
| `KassenSicherheitSettingsChanged` | Super Admin changed `KassenSicherheit` overlay |

Never log Admin PIN, API secrets, or raw signature material. Mask serials in activity-feed payloads.

Activity feed: critical unsigned-tx / TSS-down events for Mandanten-Admin + Super Admin (same pattern as AT TSE health).

---

## 7. FA surfaces

Do **not** extend `/admin/tse/fiskaly/*` — those call SIGN AT.

| Surface | Role | Permission |
|---------|------|------------|
| `/admin/kassensicherheit` (new hub) | Super Admin: provider, environment, TSS list | `system.critical` |
| `/admin/kassensicherheit/tss` | Provision / disable TSS | `system.critical` |
| Tenant settings card (DE only) | Mandanten-Admin: health, last signature, export | `settings.view` / `report.export` |
| POS | Show DE TSS health; block or warn per DE Ausfall policy (not AT FON Ausfall) | Cashier |

i18n: `kassenSicherheit.*` in de/en/tr together. POS copy stays German.

OpenAPI + Orval in the same coding PR as the API.

---

## 8. Explicit non-goals (v1)

- Epson / Swissbit USB on SaaS POS
- Reusing SIGN AT SCU ids or `Tse:Provider=fiskaly` for DE
- Claiming BSI certification for **Regkasse** (the **vendor TSS** is certified; the POS still needs its own process)
- ZUGFeRD / XRechnung XML (e-invoicing is Paket 22)

---

## Related docs

- [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) — module stub  
- [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md) — **Austria SIGN AT only**  
- [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) — AT lock pattern  
- [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) §4, §10  
- [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md) — DE XRechnung / Peppol (not TSE)
