# Recent engineering changes

Engineering changelog (not legal advice). Dates reflect documentation / feature delivery waves; individual commits may differ.

---

## 2026-07-02 — Backup permissions (Manager / tenant scoping)

**Reference:** [`docs/BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) · AI: [`ai/modules/backup_permissions.md`](../ai/modules/backup_permissions.md)

### Backend

- New permission `backup.manage` (`AppPermissions.BackupManage`) — tenant-scoped manual trigger + schedule/retention
- **Manager** default matrix: `settings.view` + `backup.manage` (not `settings.manage`)
- `settings.manage` implies `backup.manage` (+ `settings.backup`); escalation guard — `backup.manage` alone does not imply `settings.manage`
- `POST /api/admin/backup/trigger`, `PUT /api/admin/backup/settings`, legacy `POST /api/settings/backup/now` → `backup.manage`
- Execution mode / artifact download remain `settings.manage`
- Tenant guard on manual trigger: non–Super Admin without tenant context → `400 TENANT_CONTEXT_REQUIRED`
- Tests: `RolePermissionMatrixTests`, `EndpointAuthorizationRepresentativeTests`, `AdminBackupTriggerTenantScopingTests`

### Frontend Admin

- `PERMISSIONS.BACKUP_MANAGE`, `permissionImplication` mirror, `useBackupPermissions` / `useBackupManagementAccess` (`canManageBackup`)
- `BackupSettings.tsx`, `BackupDrDashboard`, `AdminBackupPage` — UI gated by `backup.manage` vs `settings.manage`
- `routePermissions.ts` comments for `/settings/backup-dr`, `/admin/backup`
- i18n: tenant-scoped backup copy uses *„Backups verwalten“* instead of *„Einstellungen verwalten“*

### Known limitation (2026-07-02 wave)

- Per-tenant **access** was JWT/idempotency-only; see **2026-07-07** for `tenant_id` column.

---

## 2026-07-07 — Backup tenant scoping Phase 3 (access + `tenant_id`)

**Reference:** [`docs/BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md)

### Backend

- Migration `AddTenantIdToBackupRuns`: nullable `backup_runs.tenant_id`, backfill from idempotency keys
- `BackupRunAccessEvaluator` centralizes read/download/trigger scope (column-first, shared scheduled runs, legacy fallback)
- Scoped reads: `runs`, `status/latest`, `verification/latest`, `dashboard/stats`, `recoverability-summary`
- Tenant-scoped manual queue (duplicate-active per tenant scope)
- HTTP integration: `BackupTriggerDownloadIntegrationTests` (trigger → orchestrator → download)

### Known limitation

- Data plane remains one deployment-wide PostgreSQL dump; `tenant_id` is **access control**, not per-tenant dump files.

---

## 2026-06-27 — Offline system (full rollout)

**Index:** [`docs/OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md)

### Wave 1 — Order snapshots + sequence reservation

**Backend:**

- **Table:** `offline_orders` (`20260627002059_AddOfflineOrdersTable`) — tenant-scoped full order snapshots (`order_data` JSONB), 72 h expiry, max 3 sync attempts
- **Services:** `OfflineOrderService`, `ISequenceReservationService` / `SequenceReservationService` (batch BelegNr reserve / tail release)
- **POS API:** `PosOfflineOrdersController` at `/api/pos/offline-orders` (save, pending, replay, status)
- **Admin API:** `AdminOfflineOrdersController` at `/api/admin/offline-orders` (list, single replay, replay-all)
- **Hosted:** `OfflineOrderCleanupHostedService` (6 h interval — delete expired pending rows)
- **Payment:** `CreatePaymentRequest.ReservedReceiptNumber` for pre-reserved BelegNr on replay
- **Tests:** `SequenceReservationServiceTests` (PostgreSQL)

**Frontend POS:**

- `offlineConfig.ts`, `offlineStorage.ts`, `offlineOrderManager.ts`, `offlineSyncService.ts`, `useOfflineOrderManager.ts`, `useOfflineStatus.ts`, `OfflineBanner.tsx`
- Legacy parallel queue: `offlineOrderQueue.ts`; reconnect sync in `useApiManager.ts`

**Frontend Admin:**

- Page `/rksv/offline-orders` — filters, table, single/batch sync (i18n de/en/tr)
- Page `/settings/offline` — tenant limits UI (`settings.manage`); API client targets `/api/admin/settings/offline`
- Orval hooks: `useGetApiAdminOfflineOrders`, `usePostApiAdminOfflineOrdersIdReplay`, `usePostApiAdminOfflineOrdersReplayAll`
- RKSV menu + route permission `payment.view`

### Wave 2 — Monitoring, alerting, dashboard widget

**Backend:**

- **Monitoring:** `IOfflineMonitoringService` / `OfflineMonitoringService` — tenant-scoped status, stats, anomalies, sync health (orders + legacy transactions)
- **API:** `AdminOfflineMonitoringController` at `/api/admin/offline-monitoring/*` (`payment.view`)
- **Config:** `OfflineMonitoringOptions`, `OfflineAlertRules` in `appsettings.example.json`
- **Alerting:** `OfflineAlertService` (background) — critical anomalies → activity feed
- **Activity types:** `OfflineOrdersBacklogGrowing`, `OfflineOrdersExpiringSoon`, `OfflineSyncStalled`
- **Dashboard catalog:** widget `offline-system-status` (`DashboardWidgetCatalog`)
- **DTOs:** `OfflineSystemStatus`, `OfflineOrderStats`, `OfflineAnomaly`, `SyncHealth` in `BillingDtos.cs`
- **Tests:** `OfflineMonitoringServiceTests`, `OfflineAlertServiceTests`

**Frontend Admin:**

- `OfflineStatusWidget` — dashboard pending counts, sync health, link to `/rksv/offline-orders`
- `useOfflineMonitoring`, `offlineMonitoringApi.ts` (30 s refresh)
- i18n: `dashboard.offlineStatusWidget.*` (de/en/tr)

### Wave 3 — QA & operations documentation

- [`docs/OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md) — E2E + API test plan
- [`docs/OFFLINE_MANUAL_TEST_CHECKLIST.md`](OFFLINE_MANUAL_TEST_CHECKLIST.md) — manual QA
- [`docs/OFFLINE_PRODUCTION_DEPLOYMENT.md`](OFFLINE_PRODUCTION_DEPLOYMENT.md) — deploy checklist, verify gate, Day 1 / Week 1 monitoring, rollback
- [`scripts/test-offline-system.mjs`](../scripts/test-offline-system.mjs), [`scripts/test-offline-system.sh`](../scripts/test-offline-system.sh)

**Note:** Coexists with legacy `offline_transactions` / `/admin/tse/offline-transactions` — separate table, APIs, services, and admin nav. Do not merge.

**OpenAPI:** Regenerated 2026-06-27 — offline **orders** routes in swagger; monitoring routes consumed via FA `customInstance` until added to OpenAPI.

**Migration applied (local):** `20260627002059_AddOfflineOrdersTable` via `dotnet ef database update`.

---

## 2026-06-23 — Billing tenant license (license_sales)

**Backend:**

- `Billing.TenantLicenseService` — mandant activate/extend/status against `license_sales` + `tenants` row
- `POST /api/admin/license/extend` for Managers (`settings.manage`)
- Billing key format `REGK-{yyyyMMdd}-{slug}-{8chars}`; activation on `POST /api/license/activate` when format matches
- Unit tests: `TenantLicenseServiceTests`, `AdminLicenseExtendTests`, `BillingTenantLicenseServiceTests`

**Documentation:** [`docs/BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md), [`ai/modules/billing_license.md`](../ai/modules/billing_license.md).

**Follow-up:** Wire FA `tenantLicense.ts` to `/api/admin/license/extend`; align permissions and rate limiting with legacy mandant extend.

---

## 2026-06-12 — Access & roles hub + admin permission filter

**Frontend Admin:**

- **Zugriff & Rollen hub** under Verwaltung: `/admin/access`, `/admin/users`, `/admin/access/roles`, `/admin/access/matrix`
- Secondary nav (`AccessSecondaryNav`), route guards, sidebar `grp-access` nested group
- Role management moved to full page at `/admin/access/roles`; read-only matrix at `/admin/access/matrix`
- Role-based menu visibility contract tests (`adminRoleMenuVisibility.test.ts`); `users/page.test.tsx` split SuperAdmin vs Manager scenarios
- i18n namespace `access`; orphan locale namespaces registered in `namespace-manifest.json`
- Command palette keys aligned to `adminShell.commandPalette.*` for usage CI

**Backend:**

- **Admin app permission profile** on login and `/me`: filters JWT permissions when `app_context=admin` (`AdminAppPermissionProfile.cs`)
- Cashier FA login whitelist; Manager strips POS-terminal permissions from admin session
- Contract tests: `AdminAppPermissionProfileTests`, `RoleAdminMenuContractTests`

**Documentation:** `frontend-admin/docs/ACCESS_AND_ROLES_HUB.md`, updates to user/role management docs.

---

## 2026-05-22 — Remove email invitation system

**Backend:**

- Removed `POST /api/admin/users/invite` and `POST /api/admin/tenants/{tenantId}/users/invite` endpoints
- Removed `TenantInvitationEmailSender` and user-invitation email sending
- Added direct user creation: `POST /api/admin/users` and `POST /api/admin/tenants/{tenantId}/users` with one-time `generatedPassword` in the response (password not audited)
- User-create audit action `USER_CREATED` with metadata `createdByUserId`, `tenantId`, `role` (no password in logs)

**Frontend Admin:**

- Replaced «Einladen» / invite flows with **Benutzer anlegen** (`CreateUserModal`)
- One-time password modal after successful create; `useCreateUser` / `createUser` API helper
- Removed invitation acceptance route `(public)/invite/accept` (if present)
- **Bestehenden Benutzer hinzufügen** unchanged (`AddExistingUserModal` → membership assign only)

**Documentation:**

- Updated user management docs to reflect direct creation flow
- SMTP no longer required for day-to-day user provisioning (optional welcome email on tenant onboarding only)

---

## 2026-05-21 — Multi-tenant isolation & tenant UX (FA + API)

### Multi-tenant isolation

- JWT `tenant_id` applied after host/dev resolution (`TenantContextMiddleware`); dev `X-Tenant-Id` / `?tenant=` disabled in Production
- Cross-tenant IDOR remains **404** (`TenantIsolationTests`)
- Offline queue and fiscal tables retain `tenant_id` through replay

### Tenant management UI (complete)

- Super Admin `/admin/tenants` CRUD, suspend, soft-delete, detail tabs (users, registers, license, settings)
- `GET /api/tenants/switcher` — DB-backed dev header switcher for all tenants (Super Admin) or memberships only
- Impersonation production handoff: `{slug}.regkasse.at/impersonate-callback`

### Customer onboarding wizard

- `CreateTenantWizard` + atomic `TenantOnboardingService` / `TenantProvisioningService` with transaction rollback
- Slug availability + suggestions APIs; structured error modal
- Optional welcome email via `WelcomeEmailService` (`Email:Smtp`)

### License display clarity

- Mandantenlizenz separated from Server-Lizenz (`/admin/license`); Manager header badge + expiry banner; Super Admin platform mode suppresses misleading warnings

### Cash register decommission

- RKSV Schlussbeleg + `Decommissioned` status; `PUT /api/admin/cash-registers/{id}/decommission`; audit `CASH_REGISTER_DECOMMISSION`
- FA `/kassenverwaltung` + tenant detail registers tab; dev-only hard delete guarded

### User management separation

- `/users` platform vs mandant tabs; tenant create/reset/remove via `/api/admin/tenants/{id}/users/*` (invite endpoints since removed — see 2026-05-22 entry)
- `ownerAdminEmail` on tenant list for switcher 🟢/🟡 indicators

**Docs:** `TENANT_MANAGEMENT.md`, `CUSTOMER_ONBOARDING.md`, `LICENSE_SYSTEM.md`, `CASH_REGISTER_LIFECYCLE.md`, `USER_MANAGEMENT.md`, `MULTI_TENANT.md`, `CHANGELOG_TENANT_MANAGEMENT.md`.

---

## 2026-05-07 — Fiscal & compliance

### Backend

- **NTP synchronization:** Background NTP sync with persisted settings (`NtpAdminSettings` migration), coordinator + SNTP client, `/api/system/time/status` and admin time-sync APIs; fiscal payment path consults `NtpTimeSyncStatus` when enabled.
- **QR receipt images:** `QrImageService` version/ECC sweep and UTF-8 byte-cap fallback for oversized RKSV payloads (voucher-heavy receipts).
- **RKSV reminders:** `RksvReminderService` + `RksvController` status DTOs for Startbeleg, Monatsbeleg/Jahresbeleg windows, company “December Monatsbeleg as Jahresbeleg” setting; mirrors on `CashRegister` / `CompanySettings` (migrations).
- **TSE health & offline:** TSE health snapshots, failure thresholds in `TseOptions`, simplified `TseController`; voucher payments cannot be accepted into non-fiscal offline queue when TSE offline (`PaymentService` partial `PaymentService.TseOffline.cs`).
- **Storno vs refund:** `StornoReason` enum and `PaymentDetails` column (migration); create-payment contract requires reason/original receipt where applicable; admin payment audit/storno-refund audit endpoints and DTOs.
- **Fiscal export / DEP diagnostics:** Disclaimer service, optional `RequireDisclaimerAcknowledgment` filter (`X-Disclaimer-Acknowledged`), disclaimer URL constant, deferred generate + download ticket flow, PDF generator, audit log reader, `AdminFiscalExportAuditController`; `FiscalExportController` behavior extended.
- **Offline admin:** `AdminOfflineTransactionsController`, retry/export-failed listing, generated client models.
- **Other:** `PosCriticalActionAuditService` touchpoints; `RksvMonatsbelegPolicy` / `RksvStartbelegPolicy` / `RksvSpecialReceiptService` adjustments; OpenAPI/swagger updates.

### Frontend (POS)

- **Time sync & TSE UX:** `TimeSyncBanner`, `useTimeSyncStatus`, `TseStatusBanner`, `TseHealthContext` / `useTseHealth`, settings and cash-register integration; contact constant for support messaging.
- **Payments:** `PaymentModal` storno/refund selection, `StornoRefundSelection`, `posStornoRefundGate`, checkout i18n; `paymentService` and `rksvSpecialReceiptsService` contract alignment.

### frontend-admin

- **Access & roles hub:** Zugriff & Rollen under Verwaltung (`grp-access`); routes `/admin/access`, `/admin/users`, `/admin/access/roles`, `/admin/access/matrix`; admin permission filter on login/`/me` (`AdminAppPermissionProfile`).
- **Dashboard:** Monatsbeleg compliance table/badge, offline queue card, time-sync drift card; hooks/API wiring.
- **RKSV / fiscal export:** Status and fiscal-export diagnostics pages updated; fiscal export disclaimer session, TSE compat hook, new admin routes (offline transactions, storno/refund audit, fiscal export audit).
- **i18n & nav:** `timeSync`, `fiscalExportAudit`, payments copy; sidebar/registry/permissions tests updated.

### Localization

- `namespace-manifest.json` updates for new admin namespaces.
