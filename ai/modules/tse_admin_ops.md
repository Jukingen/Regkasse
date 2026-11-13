# Module: TSE Super Admin operations (diagnostic / ops)

## Scope

Platform **Super Admin** TSE operations under `/api/admin/tse/*` and FA `/admin/tse/*`.

This surface is **mostly diagnostic and operational**. It is **not** the fiscal signing pipeline (cart → payment → receipt → DEP).

**Inventory (human):** [`docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`](../../docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md) §3.5 / §4  
**Fiscal / FinanzOnline core:** [`tse_finanzonline.md`](tse_finanzonline.md) · [`../07_DO_NOT_TOUCH.md`](../07_DO_NOT_TOUCH.md) §2–3, §6

## Permission

- FA + API: **`system.critical`** (`AppPermissions.SystemCritical`)
- Do **not** grant Mandanten-Admin by default

## Classification (keep straight)

| Class | Examples | Agent rule |
|-------|----------|------------|
| **Diagnostic only** | health, logs, analytics, anomalies (statistical), SLA, capacity, sustainability, knowledge, training, blockchain anchors, recommendations (workflow markers), API gateway config | No DEP / cert / Startbeleg / signature-chain rewrite |
| **Ops (device selection)** | failover, auto-healing (optional failover), resource pools, updates tracking, DR **simulation** drills | May change **which device signs**; still must not rewrite receipt bytes / DEP / chain state |
| **Fiscal-adjacent** | offline TSE intents UI/API, tse-management provision/revoke | Touch carefully; follow existing offline + provisioning modules |

## Hard rules

1. **Do not** use ops APIs to mutate fiscal signature material, receipt chains, DEP exports, or Startbeleg content.
2. **Blockchain** anchors = simulated hash ledger — **not** RKSV / BMF legal proof.
3. **Anomaly / predictive / sustainability** = heuristics / indicative metrics — not certified ML or LCA.
4. **Auto-scaling:** recommendations by default; soft provision stubs only when explicitly enabled in **Development**.
5. **Auto-healing:** default **disabled**; `AllowAutoFailover` default **false**. Prefer re-probe / clear transient `ErrorMessage` over failover.
6. Cross-tenant → HTTP **404**. Prefer `IgnoreQueryFilters()` only for Super Admin fleet queries with explicit tenant checks.
7. Dual offline remains: legacy intents (`/admin/tse/offline-transactions`) ≠ full snapshots (`/rksv/offline-orders`).

## Code map

| Layer | Path |
|-------|------|
| Controllers | `backend/Controllers/AdminTse*.cs` (+ `AdminTseManagementController`, simulator) |
| Services | `backend/Services/Tse/` (`ITse*`, implementations) |
| DTOs | `backend/DTOs/Tse*Dtos.cs` |
| Models / migrations | `backend/Models/Tse*.cs`, `backend/Migrations/20260723*_AddTse*.cs` |
| DI | `backend/ApplicationHost.cs` (`AddScoped<ITse…>`) |
| FA pages | `frontend-admin/src/app/(protected)/admin/tse/*/page.tsx` |
| FA features | `frontend-admin/src/features/tse-*` |
| i18n | `frontend-admin/src/i18n/locales/{de,en,tr}/tse*.json` + `localization/namespace-manifest.json` |
| Nav / RBAC | `adminSidebarRegistry.ts`, `routePermissions.ts` (`SYSTEM_CRITICAL`) |

## Representative API prefixes

`/api/admin/tse/health`, `…/failover`, `…/auto-healing`, `…/auto-scaling`, `…/anomalies`, `…/knowledge`, `…/recommendations`, `…/updates`, `…/disaster-recovery`, `…/webhooks`, `…/blockchain`, `…/training`, `…/compliance`, `…/capacity`, `…/sla`, `…/resource-pools`, `…/api-gateway`, `…/developer-tools`, …

Full table: comprehensive doc §3.5.

## When changing this area

1. Keep changes **additive** (new table/endpoint/page) unless the task explicitly says otherwise.
2. Update FA i18n **de/en/tr** + `namespace-manifest.json` + sidebar/nav/`routePermissions`.
3. Add/adjust unit tests under `backend/KasseAPI_Final.Tests/Tse*Tests.cs`.
4. Refresh the inventory row in `docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`.
5. If the change could affect signing device selection, call that out in the PR / task notes (fiscal-adjacent).

## Providers

- Factory may list Fiskaly / Epson / Swissbit.
- **Fiskaly** = real pipeline path; Epson/Swissbit may be **stubs** (`UnsupportedVendorTseProvider`) — do not claim production readiness without code evidence.
