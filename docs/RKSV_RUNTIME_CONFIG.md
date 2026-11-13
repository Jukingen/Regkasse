# RKSV runtime config (Demo / Production overlay)

**Audience:** Super Admins, backend/FA maintainers.  
**Not a legal guarantee:** This document describes product controls. It is not official BMF/RKSV certification.

**Related:** [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md), [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md), FA `/admin/rksv/config`.

---

## What this is

Instance-wide overlay for **receipt labelling** and RKSV presentation:

| Field | Values | Effect |
|-------|--------|--------|
| `RKSV.Mode` | `Demo` / `Production` | Demo vs production fiscal presentation |
| `RKSV.TseMode` | `Simulation` / `Real` | Overlay for lock evaluation + TSE sim badges. **`Real` disables Development TSE health bypass** so signing can run locally. |
| `RKSV.FinanzOnlineMode` | `Simulation` / `Real` | Overlay for lock evaluation + FON sim badges (not live SOAP credentials) |
| `RKSV.ShowDemoLabel` | `true` / `false` | Receipt footer **DEMO / NICHT FISKAL** |
| `BypassTseInDevelopment` | `true` / `false` | Development-only TSE health-probe bypass. Default **false**. Ignored when `TseMode=Real` and never applied on Production hosts. Also seeded from `DevelopmentOptions:BypassTseInDevelopment`. |

Persisted in PostgreSQL table **`rksv_runtime_config`** (singleton `id = 1`). Survives API restarts. **Not tenant-scoped** — one overlay per deployment.

This is **not** a replacement for hardware `Tse:TseMode` / `Tse:Provider`. It **does** override Development TSE **health-check bypass**: `TseMode=Real` always runs real probes/signing on a Development host, even if `DevelopmentOptions:BypassTseInDevelopment` or FA Entwicklungsmodus `bypassTseCheck` is on.

TSE health bypass is **opt-in** (default false):

1. `DevelopmentOptions:BypassTseInDevelopment` (appsettings, default `false`)
2. Overlay `BypassTseInDevelopment` on this page (default `false`)
3. FA `/settings/development-mode` `bypassTseCheck` (seed/default `false`)

All three are ignored outside Development. Overlay `TseMode=Real` wins over all three.

This API does **not** switch hardware TSE (`Tse:TseMode` / `Tse:Provider`) or rewrite historical receipts.

---

## Read order (effective config)

1. If row `id=1` exists → **database overlay wins**.
2. Else → **appsettings** `RKSV:*` (and Development/Staging default `Mode=Demo` when `RKSV:Mode` is empty).
3. First `GET`/`POST` `/api/admin/rksv/config` **seeds** the row from appsettings (`RksvRuntimeConfigEnsure`) so later restarts keep the same values.

In-process cache is ~30 seconds. New receipts/closings pick up the overlay without an API restart. Existing printed receipts stay unchanged.

Receipt DEMO text is backend-driven (`IRksvEnvironmentService.ShowDemoLabel()` → `ReceiptService` / POS `showDemoLabel`). POS and FA receipt views both consume that DTO.

---

## APIs and UI

| Surface | Detail |
|---------|--------|
| `GET /api/admin/rksv/config` | Super Admin (`system.critical`). Ambient tenant **not** required (exact path exemption; not `/api/admin/rksv/*` DEP). |
| `POST /api/admin/rksv/config` | Same. Body: `mode`, `tseMode`, `finanzOnlineMode`, `showDemoLabel`, `bypassTseInDevelopment`, optional `reason`. |
| FA | `/admin/rksv/config` |
| Live badges | `GET /api/rksv/environment` (FA) and POS overview `rksvEnvironment` |

Audit: `AuditEventType.RksvRuntimeConfigChanged`. Activity: `ActivityEventType.RksvRuntimeConfigChanged` (platform tenant).

---

## Production lock

On Production/Staging hosts, Demo / Simulation overlays are rejected with HTTP **409** `RKSV_PRODUCTION_LOCK` unless `Tse:AllowUnsafeFiscalModesInProduction` is true. File-level `RKSV:Mode=Demo` on Production still fails startup validation (`TseProductionOptionsValidator`). The DB overlay cannot be used to sneak Demo onto a locked Production host.

---

## Migration from appsettings-only

1. Deploy the EF migration `AddRksvRuntimeConfig` (`rksv_runtime_config`).
2. Leave `RKSV:*` in the environment’s appsettings as the **seed and fallback**.
3. After deploy, open FA `/admin/rksv/config` (or `GET` the API) once so the singleton row is created from the current file values.
4. Super Admin then changes Demo/Production via the UI. File values remain the fallback if the row is deleted (do not delete in production).
5. Cutover to fiscal Production: set Mode=`Production`, TseMode=`Real`, FinanzOnlineMode=`Real`, ShowDemoLabel=`false` in the UI **and** keep matching appsettings so a fresh database still seeds safely. Follow [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md).

Do not treat FA “TEST (Entwicklungsmodus)” as this overlay. Operators who need to hide **DEMO / NICHT FISKAL** must use `/admin/rksv/config`, not TSE health bypass.
