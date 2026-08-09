# Improvements Completed — 2026-08-07

Session summary for DEP export UX/metadata, backup configuration clarity, and related go-live docs.

**Related hubs:** [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md) · [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) · [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md) · [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md)

---

## 1. DEP Export Improvements

### 1.1 Simulation metadata

- Added `IsSimulated` and `SimulationNote` to `RksvDepExportBuildResult` and `RksvDepExportEnvelopeDto`
- Kept BMF JSON root (`Belege-Gruppe`) unchanged (Prüftool-safe — no simulation wrapper inside the download file)
- Response header when simulated: `X-Regkasse-Dep-Export-Simulated: true`
- Clarified: DEP already extracts **real** receipt/JWS data from the DB; Soft TSE / demo mode only means signatures are not legally binding

### 1.2 Last export tracking

- `GET /api/admin/rksv/dep-export/last-export` — latest completed history row (`ExportedAt`, file meta, `isSimulated`, download count)
- `GET /api/admin/rksv/dep-export/status` — current RKSV/TSE simulation flag + last-export summary
- FA shows last export date, download count, and simulation badge

### 1.3 FA UI

- Simulation warning `Alert` on `/admin/rksv/dep-export`
- Last export line on the Export tab
- “Simuliert” / Simulated badge on history + recent lists
- i18n keys in `rksvHub.json` (de / en / tr)

### 1.4 Database

- Migration: `20260807150000_AddDepExportHistoryIsSimulated`
- Columns on `dep_export_history`: `is_simulated`, `simulation_note`
- History record + DEP audit details include `isSimulated`

---

## 2. DEP Download / History (existing + reinforced)

Already present before this session; confirmed as part of the same workstream:

- `GET /api/admin/rksv/dep-export/download/{exportId}` (+ history / token aliases)
- Opaque download tokens (TTL via `DepExportStorageOptions`)
- `dep_export_history` with hot expiry, download count, archive hooks
- `DepExportCleanupHostedService` for hot storage / metadata retention
- Audit events: `RksvDepExportCreated` / `Downloaded` / …

Session additions mainly stamp **simulation flags** on history rows and surface them in FA.

---

## 3. Backup Configuration

### 3.1 Production

- Local / example production config already used `ExecutionAdapterKind=PgDump`
- Updated tracked `backend/appsettings.Production.example.json` with full recommended keys:
  - `VerifyLogicalDumpFileOnDisk`, `PgDumpTimeoutSeconds`, acknowledge flags, schedule/retention
  - `_commentWindows` for Windows path alternatives (PG 18)

### 3.2 Development

- Fake adapter remains the default (expected)
- Manifest `"no real pg_dump"` is **normal** under Development + Fake
- `appsettings.Development.example.json` documents this via `_comment`
- `BackupOptionsValidator` / `BackupConfigurationEvaluation` already block unacknowledged Fake outside Development

### 3.3 Local PgDump helper scripts

- `scripts/test-real-backup.ps1` — user-secrets → PgDump + local `pg_dump.exe`
- `scripts/revert-backup-fake.ps1` — remove those secrets
- Docs: `docs/BACKUP_SYSTEM.md` § Understanding `"no real pg_dump"`; `backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md`

---

## 4. Cloud / Go-Live Documentation (companion)

Already available / used as the go-live pack:

| Doc | Purpose |
|-----|---------|
| [`CLOUD_PRODUCTION_READINESS.md`](CLOUD_PRODUCTION_READINESS.md) | SaaS readiness (GDPR, SLA, pricing pointers, deploy) |
| [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) | Launch checklist |
| [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) | Server preflight + deploy script + Dev/Prod separation |
| [`DPA_TEMPLATE.md`](DPA_TEMPLATE.md) | Art. 28 DPA / AVV template |
| [`SLA_CUSTOMER.md`](SLA_CUSTOMER.md) | Customer-facing SLA |

---

## 5. Files touched this session (high level)

### Backend

- `Models/Export/RksvDepExportDtos.cs`, `DTOs/RksvDepExportRequestDtos.cs`, `DTOs/DepExportHistoryDtos.cs`
- `Services/RksvDepExportService.cs`, `Services/DepExportHistoryService.cs`
- `Models/DepExportHistory.cs`, `Data/AppDbContext.cs`
- `Migrations/20260807150000_AddDepExportHistoryIsSimulated.cs`
- `Controllers/AdminRksvDepExportController.cs`
- Tests: `RksvDepExportServiceTests`, `DepExportHistoryServiceTests`
- `appsettings.Production.example.json`, `appsettings.Development.example.json`

### Frontend-Admin

- `features/rksv/components/DepExportTestPage.tsx`
- `features/rksv/hooks/useDepExport.ts`, `useDepExportHistory.ts`
- `features/rksv/types/depExport.ts`
- `i18n/locales/{de,en,tr}/rksvHub.json`

### Docs / scripts

- `docs/BACKUP_SYSTEM.md`
- `docs/IMPROVEMENTS_COMPLETED.md` (this file)
- `scripts/test-real-backup.ps1`, `scripts/revert-backup-fake.ps1`

---

## 6. Test results (spot check)

| Suite filter | Result |
|--------------|--------|
| `RksvDepExportServiceTests` + `DepExportHistoryServiceTests` + `BackupOptionsValidatorTests` | ✅ **61** passed (2026-08-07) |

Apply DB migration before relying on new history columns:

```bash
dotnet ef database update --project backend
```

---

## 7. Next steps (before go-live)

### Infrastructure (P0)

- [ ] Deploy with `ASPNETCORE_ENVIRONMENT=Production` ([`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md))
- [ ] Run `./scripts/ops/preflight-production.sh` then `REGKASSE_DEPLOY_CONFIRM=YES ./scripts/ops/deploy-production.sh`
- [ ] SSL/TLS + DNS (`api` / `admin` / `pos`.regkasse.at)
- [ ] Production PostgreSQL + verified `pg_dump` path
- [ ] Manual System backup; confirm manifest is **not** Fake / “no real pg_dump”

### TSE & FinanzOnline (P0)

- [ ] TSE Device (not Soft/Demo) + FinanzOnline production mode
- [ ] Startbeleg / outbox smoke tests

### Monitoring & onboarding (P1)

- [ ] Uptime + backup-failure alerts
- [ ] Onboarding mail / DE user manual / support escalation

---

## 8. Known issues / risks

| Issue | Impact | Mitigation |
|-------|--------|------------|
| DEP under RKSV/TSE simulation | Operators may treat signatures as fiscal | FA warning + `IsSimulated` badge / header |
| Dev Fake backups | Manifest says no real dump | Expected in Development; use `test-real-backup.ps1` to verify PgDump |
| Wrong production `pg_dump` path | Backup fails | Production.example + host checklist |
| TSE / FON still in demo on go-live | Compliance failure | Cutover checklist |

---

## 9. Conclusion

### Done

- DEP export simulation metadata + last-export/status APIs + FA warnings
- Backup Fake vs PgDump clarified; production example hardened; local test scripts
- Go-live / cloud readiness docs linked as the operator pack

### Still required for go-live

- Production deploy + real `pg_dump` verification on the server
- TSE / FinanzOnline production cutover
- Monitoring and first-customer onboarding

---

**Last updated:** 2026-08-07
