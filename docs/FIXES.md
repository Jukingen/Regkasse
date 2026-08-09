# Fixes Log

## 2026-08-08: EF model snapshot sync (production gate)

**Problem:** `dotnet ef migrations has-pending-model-changes` reported drift because several additive migrations (DEP download token / simulated / download_count, and older catalog/TSE files) were authored without Designer updates, so `AppDbContextModelSnapshot` lagged the runtime model.

**Fix:** Added `20260808214645_SyncDepExportAndPendingModelSnapshot` with **empty** `Up`/`Down` (no SQL) and a current Designer/snapshot. Schema continues to come from the dated additive migrations.

**Gate:** Before Production migrate, expect *No changes have been made to the model since the last migration.*

---

## 2026-08-08: DEP Soft TSE cert fallback + history status string enum

### Problems
1. `GET /api/admin/rksv/dep-export` returned `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` for historical thumbprints after Soft TSE process restart (in-memory cert registry changes).
2. History Recent Exports download button stayed disabled: API emitted `status: 2` while FA compared to `'Completed'`.

### Solution
1. **Demo/simulation only:** `RksvDepExportService` falls back to the current Soft TSE leaf (and chain) when the stamped thumbprint is missing. Production still throws `RksvDepExportCertificateMissingException`.
2. `[JsonConverter(typeof(JsonStringEnumConverter))]` on `DepExportStatus` → JSON `"Completed"`.
3. FA helpers `isDepExportHistoryCompleted` / `normalizeDepExportHistoryStatus` accept string or legacy numeric `2`.

### Files Changed
- `backend/Services/RksvDepExportService.cs`
- `backend/Models/DepExportHistory.cs`
- `backend/KasseAPI_Final.Tests/RksvDepExportServiceTests.cs`
- `frontend-admin/src/features/rksv/hooks/useDepExportHistory.ts`
- `frontend-admin/src/features/rksv/hooks/__tests__/depExportHistoryStatus.test.ts`
- `frontend-admin/src/features/rksv/components/DepExportTestPage.tsx`

### Status
✅ Implemented

---

## 2026-08-08: DEP Export Download (already implemented — no new code)

### Problem (reported)
- DEP export files are created but users cannot download them / no download link

### Finding
The sketched solution is **already landed** in the working tree. Re-implementing the prompt as-is would duplicate endpoints and diverge from the production-safe path (tenant 404 semantics, hot expiry, audit on every attempt, blob download helpers).

### Existing backend
- `GET /api/admin/rksv/dep-export/download/{exportId}` → `AdminRksvDepExportController.DownloadExport`
- Alias: `GET …/history/{id}/download`
- Token path: `POST …/download/{exportId}/token`, `GET …/download/token/{token}`
- Persist on complete + `ExpiresAt` + optional download token: `DepExportHistoryService.RecordCompletedAsync`
- Stamp: `MarkDownloadedAsync` (`DownloadedAt`, `DownloadCount++`)
- Audit: `RksvDepExportDownloaded` + DEP audit trail on success/failure
- Create envelope returns `exportId`, `historyId`, `downloadUrl`, `expiresAt`, `fileName`

### Existing FA
- `useDepExport` always requests `includeEnvelope=true` and maps `exportId` / `downloadUrl`
- `DepExportTestPage`: success notification with **Jetzt herunterladen** → `downloadDepExportHistoryFile`
- Recent Exports history list with per-row download / delete
- i18n (`rksvHub.depExportPage.*`): `exportSuccess`, `exportReady`, `downloadNow`, `downloadFailed`, `exportExpired`, `historyTitle`, `noExports`

### Status
✅ Feature present — **do not apply the sketch as a rewrite**. If downloads still fail in UI, debug the specific failure (`RKSV_DEP_EXPORT_*` code, missing `StoragePath`, hot expiry, or password-change / permission gate) instead of adding a second endpoint.

---

## 2026-08-08: TSE Backup type mismatch (`varchar = uuid`)

### Problem
- TSE DR backup failed with PostgreSQL: `operator does not exist: character varying = uuid`
- Location: `TseBackupService.LoadTenantDevicesAsync` comparing legacy `TseDevices.KassenId` (varchar) to `Guid` register ids in SQL

### Root Cause
- CLR property `TseDevice.KassenId` is `Guid`, but the DB column is still `character varying(50)`
- EF translated `registerIds.Contains(t.KassenId)` as uuid compare against varchar

### Solution
- Prefer SQL filters on `TenantId` / `CashRegisterId` (uuid)
- Match legacy `KassenId`-only rows in memory after materialization (`registerIdStrings`)
- EF value converter `TseDeviceKassenIdConverter` maps Guid ↔ varchar on read/write
- Same commit also fixed force-close `cash_register_transactions.UserId` FK (placeholder/`system` → real AspNetUsers id) and FA TSE backup tenant slug override

### Test Results
- Unit: `TseBackupServiceTests.CreateTseBackupAsync_IncludesLegacyKassenIdOnlyDevices`
- Unit: `CashRegisterShiftServiceTests` force-close actor resolution
- Commit: `f25a7d4e` — `fix: resolve TSE backup type mismatch and shift auto-close FK`

### Files Changed
- `backend/Services/TseBackupService.cs`
- `backend/Data/AppDbContext.cs`
- `backend/Services/CashRegisterShiftService.cs`
- `backend/KasseAPI_Final.Tests/TseBackupServiceTests.cs`
- `backend/KasseAPI_Final.Tests/CashRegisterShiftServiceTests.cs`
- `frontend-admin/src/features/tse-management/api/tseManagement.ts`
- `frontend-admin/src/app/(protected)/admin/tse-management/page.tsx`

### Status
✅ Done (already landed; no further code change needed for the proposed string-cast SQL fix)

---

## 2026-08-08: AuditLog 500 Error for Manager Role

### Problem
- `GET /api/AuditLog` was returning 500 Internal Server Error
- Affected: Both Manager and SuperAdmin roles
- Error: `InvalidOperationException` - navigation property `User` was ignored but `Include` was still called

### Root Cause
- `AuditLog.User` is ignored in EF configuration (`Ignore(a => a.User)`)
- `AuditLogService.GetAuditLogsPagedAsync` was still using `.Include(a => a.User)`
- Search filters were referencing `a.User.*` fields

### Solution
- Removed `.Include(a => a.User)` from the query
- Removed search filter references to `a.User.*`
- Actor names are now resolved via `IActorDisplayNameResolver`

### Test Results
- `GET /api/AuditLog` → 200 OK (Manager: 5 records)
- `GET /api/AuditLog/statistics` → 200 OK
- Permission check (`audit.view`) works correctly

### Files Changed
- `backend/Services/AuditLogService.cs`
- `backend/Services/AuditLogQueryExtensions.cs`
- `backend/Controllers/AuditLogController.cs` (resolve actor display names after materialization)
