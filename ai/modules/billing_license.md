# Module: Billing tenant license

## Scope

- Super Admin `license_sales` (create, preview, invoice PDF, audit, reminders)
- `Billing.TenantLicenseService` — mandant activate / extend / status / expiring
- `BillingAuditService` / `ReminderService` — Super Admin billing audit trail + expiry reminders
- Billing-format keys: `REGK-{yyyyMMdd}-{slug}-{8chars}`
- **Not** deployment `LicenseService` / `issued_licenses` / machine JWT

## Multi-tenant

- `license_sales`, `billing_audit_log`, and `license_reminders` are tenant-scoped (`tenant_id`).
- Manager extend/activate uses `ICurrentTenantAccessor` + tenant slug match; cross-tenant key → German error, HTTP 400.
- EF: use `.IgnoreQueryFilters()` when resolving sales by key from background jobs, hosted services, or Super Admin ops without ambient tenant.

## API boundaries

### Super Admin — billing sales (`AdminBillingController`)

| Method | Path | Auth |
|--------|------|------|
| `POST` | `/api/admin/billing/license-sales/preview` | `SuperAdmin` |
| `POST` | `/api/admin/billing/license-sales` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales/{id}` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales/by-key/{licenseKey}` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales/{id}/pdf` | `SuperAdmin` |
| `POST` | `/api/admin/billing/license-sales/preview-pdf` | `SuperAdmin` |
| `POST` | `/api/admin/billing/license-sales/{id}/cancel` | `SuperAdmin` |
| `GET` | `/api/admin/billing/stats` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales/expiring` | `SuperAdmin` |
| `GET` | `/api/admin/billing/tenants/{tenantId}/license` | `SuperAdmin` |
| `GET` | `/api/admin/billing/audit` | `SuperAdmin` |
| `GET` | `/api/admin/billing/license-sales/{id}/audit` | `SuperAdmin` |
| `GET` | `/api/admin/billing/tenants/{tenantId}/reminders` | `SuperAdmin` |
| `POST` | `/api/admin/billing/reminders/check` | `SuperAdmin` |
| `POST` | `/api/admin/billing/reminders/send` | `SuperAdmin` |

OpenAPI source: `backend/swagger.json` (regenerate: `node scripts/generate-backend-openapi.mjs`).

### Manager — mandant billing (`LicenseController` partial)

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| `GET` | `/api/license/billing/status` | JWT + tenant | Mandant `TenantLicenseStatus` |
| `POST` | `/api/license/billing/activate` | JWT + tenant + user | Returns `ActivationResult` |
| `POST` | `/api/license/extend` | JWT + tenant + user | Returns `ExtendResult` |

Anonymous POS deployment endpoints remain: `GET /api/license/status`, `POST /api/license/activate` (billing branch requires auth when key format matches).

### Legacy admin paths

| Path | Permission | Service |
|------|------------|---------|
| `POST /api/admin/license/extend` | `settings.manage` | `Billing.ITenantLicenseService` |
| `POST /api/admin/license/mandant/*` | `license.manage` | `AdminTenantLicenseService` (legacy) |

Do not route POS clients to `/api/admin/*`.

## Services & DI

| Interface | Implementation | Notes |
|-----------|----------------|-------|
| `IBillingService` | `BillingService` | Sales CRUD, PDF trigger; uses `IServiceScopeFactory` for reminders (breaks circular DI) |
| `IBillingAuditService` | `BillingAuditService` | `billing_audit_log`; actor via `ICurrentUserService` |
| `IReminderService` | `ReminderService` | Also `IBillingReminderService` (same instance per scope) |
| `IBillingTenantLicenseService` | `Billing.TenantLicenseService` | Activate / extend / expiring |
| `ICurrentUserService` | `CurrentUserService` | HTTP actor id for audit |

Hosted: `Services.Hosted.BillingReminderHostedService` — daily sweep (`CheckAndCreateRemindersAsync` + `SendPendingRemindersAsync`). Manual triggers via admin POST endpoints.

Two `ITenantLicenseService` interfaces:

- `Services.AdminTenants` — key preview / resolve (read-only helpers)
- `Services.Billing` — lifecycle (activate, extend, status)

Register as `IAdminTenantLicenseKeyService` + `IBillingTenantLicenseService` aliases in controllers/tests.

## Audit event types (`BillingAuditEventTypes`)

| Constant | When |
|----------|------|
| `SALE_CREATED` | Super Admin creates sale |
| `SALE_CANCELLED` | Sale cancelled |
| `LICENSE_ACTIVATED` | Manager activates billing key |
| `LICENSE_EXTENDED` | Manager extends with new key |
| `SALE_REFUNDED` | Defined; not wired yet |

## Reminder behaviour

- Anchors: 30 / 15 / 7 / 3 / 1 days before `valid_until_utc` (code constants; `BillingOptions.ReminderDaysBeforeExpiry` exists but not fully wired).
- `SendPendingRemindersAsync` marks rows `sent`; **SMTP email not yet integrated** (reuse `ILicenseReminderEmailSender` planned).
- Sale create schedules future reminders; cancel cancels pending rows.

## Rules

- Extend = activate new sale key + extension audit; does not stack duration on existing expiry without a new sale row.
- German operator messages in service layer; API error `message` field in English or German per existing endpoint (billing uses DE messages in body).
- Do not weaken sale status / expiry / tenant-slug checks.
- High-risk adjacent: tenant row `license_*` columns — keep consistent with `license_sales` when changing activation.

## Tests

- `Tests/Billing/BillingServiceTests.cs` — core sale scenarios + extended validation (harness: `BillingServiceTestHarness.cs`)
- `TenantLicenseServiceTests`, `BillingTenantLicenseServiceTests`, `LicenseControllerManagerTests`, `AdminLicenseExtendTests`
- `BillingAuditServiceTests`, `BillingReminderServiceTests`, `AdminBillingControllerTests`
- FA: `frontend-admin/src/shared/__tests__/billingRoutes.test.ts`

```bash
cd backend && dotnet test --filter "FullyQualifiedName~KasseAPI_Final.Tests.Billing|FullyQualifiedName~BillingAudit|FullyQualifiedName~BillingReminder|FullyQualifiedName~AdminBilling"
```

## Human docs

- `docs/BILLING_TENANT_LICENSE.md`
- `docs/BILLING_TESTING.md`
