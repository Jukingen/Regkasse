# Release notes — 2026-08-08

**Audience:** Ops / Super Admin promoting Staging → Canary → Production  
**Hosts:** `api.regkasse.at` · `admin.regkasse.at` · `pos.regkasse.at`  
**Related:** [`../DEPLOYMENT.md`](../DEPLOYMENT.md) · [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) · [`FIXES.md`](FIXES.md)

---

## Summary

This wave hardens Mandanten-Admin fiscal ops and dashboard UX before production:

1. **AuditLog 500** — Manager audit list no longer joins ignored `AuditLog.User` navigation.
2. **DEP export download** — durable history download paths (already present); Soft TSE cert fallback for **demo/sim only**; history `status` serialized as string enum for FA.
3. **Dashboard widget customization (P2)** — Manager license/KPI/activity/ops cards moved into existing `@dnd-kit` catalog + `/api/admin/dashboard/preferences`; Handlungsbedarf stays pinned.
4. **EF snapshot sync** — `20260808214645_SyncDepExportAndPendingModelSnapshot` (empty SQL) aligns `AppDbContextModelSnapshot` with the runtime model after hand-written DEP migrations.

---

## Migrations (Production)

| Migration | SQL? | Notes |
|-----------|------|--------|
| `20260807120000_AddDepExportHistoryDownloadToken` | Yes | download token, expiry, wider `storage_path` |
| `20260807150000_AddDepExportHistoryIsSimulated` | Yes | simulation flag + note |
| `20260808130000_AddDownloadCountToDepExportHistory` | Yes | `download_count` |
| `20260808214645_SyncDepExportAndPendingModelSnapshot` | **No** | snapshot-only; safe to record in `__EFMigrationsHistory` |

Confirm with:

```bash
cd backend
dotnet ef migrations list --project KasseAPI_Final.csproj
dotnet ef migrations has-pending-model-changes --project KasseAPI_Final.csproj
```

Expect: *No changes have been made to the model since the last migration.*

---

## Production configuration gate

Copy from [`backend/appsettings.Production.example.json`](../backend/appsettings.Production.example.json):

| Key | Value |
|-----|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` (process) |
| `Tse:TseMode` | `Device` |
| `Tse:Mode` | `Real` |
| `FinanzOnline:*:UseSimulation` | `false` |
| `Backup:ExecutionAdapterKind` | `PgDump` |
| `TwoFactorAuth:Enabled` | `true` |
| `Security:Csrf:Enabled` | `true` |

---

## Deploy / rollback

1. System **PgDump** backup + `prepare-rollback-backup.sh` (or note GHCR digest).
2. Staging → Canary (tenant soak) → Production (`deploy-production.yml` + compliance phrase).
3. Smoke: [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md).
4. Rollback: previous image / `rollback-production.sh` — **do not** auto-`Down` EF schema.

---

## Test results (pre-release gate, 2026-08-08)

| Suite | Result | Notes |
|-------|--------|--------|
| POS (`frontend` `npm run test`) | **530 passed** | OK |
| Backend critical (Dashboard / DEP / DepExportHistory) | **51 passed** | OK |
| Backend full suite | **3644 passed / 55 failed / 50 skipped** | Failures include pre-existing tenant-gate / Redis-skip noise; **do not** block solely on full green until triage — re-run on CI |
| FA (`frontend-admin` `npm run test`) | **Partial** | Local monorepo dual-React (`Invalid hook call` / `useState` on null dispatcher when `react-dom` resolves from repo root). Pure dashboard utils/registry tests pass. Prefer CI FA workflow as source of truth |

---

## Smoke after Production

- [ ] `GET /api/health/ready` Healthy (TSE Device/Real, FON not simulation)
- [ ] Manager: `GET /api/AuditLog` → 200
- [ ] DEP: export → history download → status shows Completed
- [ ] Dashboard: drag widget → reload → order persisted; Handlungsbedarf still on top
- [ ] SuperAdmin 2FA challenge on login
- [ ] CSRF: mutating admin call without token rejected

---

## Out of scope / deferred

- Free-form 2D widget resize (`react-grid-layout`)
- Making Handlungsbedarf draggable
- Soft TSE cert fallback in **Production** (still throws missing certificate)
