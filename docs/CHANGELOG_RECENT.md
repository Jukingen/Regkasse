# Recent engineering changes

Engineering changelog (not legal advice). Dates reflect documentation / feature delivery waves; individual commits may differ.

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

- `/users` platform vs mandant tabs; tenant invite/reset/remove via `/api/admin/tenants/{id}/users/*`
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

- **Dashboard:** Monatsbeleg compliance table/badge, offline queue card, time-sync drift card; hooks/API wiring.
- **RKSV / fiscal export:** Status and fiscal-export diagnostics pages updated; fiscal export disclaimer session, TSE compat hook, new admin routes (offline transactions, storno/refund audit, fiscal export audit).
- **i18n & nav:** `timeSync`, `fiscalExportAudit`, payments copy; sidebar/registry/permissions tests updated.

### Localization

- `namespace-manifest.json` updates for new admin namespaces.
