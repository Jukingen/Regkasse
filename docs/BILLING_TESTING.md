# Billing & license management — test guide

> **Scope:** Super Admin billing (`license_sales`, audit, reminders, invoices) and Mandant SaaS license lifecycle (activate / extend).  
> **Not in scope:** Deployment / On-Premise license (`LicenseService`, `issued_licenses`) — see [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md).

**Last updated:** 2026-06-24

---

## Quick commands

```bash
# All billing-related backend tests
cd backend && dotnet test --filter "FullyQualifiedName~Billing|FullyQualifiedName~TenantLicenseService|FullyQualifiedName~AdminBilling|FullyQualifiedName~LicenseControllerManager|FullyQualifiedName~InvoicePdfGenerator"

# Core sale service only
cd backend && dotnet test --filter "FullyQualifiedName~KasseAPI_Final.Tests.Billing.BillingServiceTests"

# Frontend Admin — billing hooks, new sale page, routes
cd frontend-admin && npm run test -- src/features/billing/__tests__/
cd frontend-admin && npm run test -- src/shared/__tests__/billingRoutes.test.ts
```

---

## Test layout

| Area | File | Namespace | What it covers |
|------|------|-----------|----------------|
| **Sale service** | `KasseAPI_Final.Tests/Billing/BillingServiceTests.cs` | `KasseAPI_Final.Tests.Billing` | Preview, create, list, cancel, stats, PDF, key validation |
| **Sale harness** | `KasseAPI_Final.Tests/Billing/BillingServiceTestHarness.cs` | `KasseAPI_Final.Tests.Billing` | In-memory DB factory, tenant/user seeding, service wiring |
| **Test doubles** | `KasseAPI_Final.Tests/BillingTestDoubles.cs` | `KasseAPI_Final.Tests` | No-op reminders, audit service factory |
| **Mandant lifecycle** | `KasseAPI_Final.Tests/TenantLicenseServiceTests.cs` | — | Status, activate, expiring list |
| **Mandant lifecycle (extended)** | `KasseAPI_Final.Tests/BillingTenantLicenseServiceTests.cs` | — | Wrong tenant, extend metadata, history |
| **Manager API** | `KasseAPI_Final.Tests/LicenseControllerManagerTests.cs` | — | `POST /api/license/billing/*`, extend |
| **Legacy extend** | `KasseAPI_Final.Tests/AdminLicenseExtendTests.cs` | — | `POST /api/admin/license/extend` |
| **Activation routing** | `KasseAPI_Final.Tests/LicenseControllerActivateTests.cs` | — | Billing key branch on `POST /api/license/activate` |
| **Audit** | `KasseAPI_Final.Tests/BillingAuditServiceTests.cs` | — | Write, list filters, sale trail |
| **Reminders** | `KasseAPI_Final.Tests/BillingReminderServiceTests.cs` | — | Schedule anchors, cancel pending, send |
| **Admin controller** | `KasseAPI_Final.Tests/AdminBillingControllerTests.cs` | — | HTTP integration (sales, PDF, audit) |
| **Invoice PDF** | `KasseAPI_Final.Tests/InvoicePdfGeneratorTests.cs` | — | HTML template → PDF bytes |
| **Key format** | `KasseAPI_Final.Tests/LicenseKeyGeneratorTests.cs` | — | `REGK-{yyyyMMdd}-{slug}-{8chars}` |
| **FA routes** | `frontend-admin/src/shared/__tests__/billingRoutes.test.ts` | — | App Router pages, `SYSTEM_CRITICAL` guard |

---

## `BillingServiceTests` — core scenarios

These six tests are the minimum contract for Super Admin license sales:

| Test | Asserts |
|------|---------|
| `PreviewLicenseSale_ValidRequest_ReturnsPreview` | 12-month pricing (299 net → 358.80 gross), `REGK-` key, `RE` invoice number |
| `CreateLicenseSale_ValidRequest_CreatesSale` | Active sale persisted; tenant `license_key`, `license_valid_until_utc`, `current_license_sale_id` updated |
| `CreateLicenseSale_CustomPlan_CreatesWithCustomDate` | `custom` plan uses `CustomValidUntilUtc` |
| `CancelLicenseSale_ValidRequest_CancelsSale` | Status `cancelled`; tenant license fields cleared when sale was current |
| `ListLicenseSales_WithFilters_ReturnsFilteredList` | `TenantId` filter returns single tenant's sales |
| `GetStats_ReturnsCorrectStats` | Active revenue sum, sale count, average net price |

Extended tests in the same file cover validation errors, pagination, date filters, PDF generation, key uniqueness, and stats excluding cancelled sales.

### Test infrastructure

- **Database:** EF Core in-memory (`UseInMemoryDatabase`) with `NullCurrentTenantAccessor` (Super Admin ops bypass tenant filter via `IgnoreQueryFilters()` in service).
- **Harness:** `BillingServiceTestHarness` — one DB per test class instance; `CreateTestTenantAsync`, `CreateTestUserAsync`, `CreateTestSaleAsync`.
- **Reminders:** `BillingTestDoubles.CreateReminderScopeFactory()` registers `NoOpReminderService` (no hosted-service side effects).
- **Audit:** Real `BillingAuditService` with `NullCurrentUserService` (actor id optional in tests).
- **PDF:** `InvoicePdfGenerator` + `InvoicePdfTemplateService`; temp `ContentRootPath` for file persistence tests.

---

## Mandant license tests

`Billing.TenantLicenseService` validates:

- Key format (`REGK-{yyyyMMdd}-{slug}-{8chars}`)
- Sale exists and is `active`
- Sale not expired
- Tenant slug matches JWT tenant context
- German operator messages on failure

Extend = activate new sale key + `LICENSE_EXTENDED` audit row; does **not** stack days on existing expiry without a new sale.

---

## Controller integration

`AdminBillingControllerTests` uses `WebApplicationFactory` pattern with Super Admin JWT. Covers:

- Preview / create sale
- List + detail
- Cancel
- Stats
- PDF download headers

`LicenseControllerManagerTests` covers Manager-facing billing endpoints with tenant-scoped JWT.

---

## Frontend Admin

| Test file | Coverage |
|-----------|----------|
| `features/billing/__tests__/billingHooks.test.tsx` | `useBillingSalesList`, `useBillingCreate` (mocked `billingApi`) |
| `features/billing/__tests__/NewSalePage.test.tsx` | New sale page form render + preview flow |
| `shared/__tests__/billingRoutes.test.ts` | App Router pages, `SYSTEM_CRITICAL` guard |

Billing feature hooks (`features/billing/hooks/*`) rely on generated OpenAPI client — regenerate after swagger changes:

```bash
cd frontend-admin && npm run generate:api
```

---

## Adding new billing tests

1. **Service unit test:** Add to `Billing/BillingServiceTests.cs` or create sibling in `Billing/` namespace. Reuse `BillingServiceTestHarness` for happy-path flows; use `BillingServiceTestInfrastructure.CreateDb()` for error/edge cases needing direct DB mutation.
2. **Cross-service:** Use `BillingTestDoubles` for audit/reminder stubs; never mock `IDbContextFactory` unless testing pure logic.
3. **Controller:** Extend `AdminBillingControllerTests` or add focused test class with `WebApplicationFactory`.
4. **Update this doc** and [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) test table when adding new test files.

---

## Related documentation

| Document | Topic |
|----------|--------|
| [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) | API, services, DB, reminders |
| [`BILLING_E2E_TEST_PLAN.md`](BILLING_E2E_TEST_PLAN.md) | Manual E2E QA scenarios (7 flows) |
| [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md) | Three license layers |
| [`ai/modules/billing_license.md`](../ai/modules/billing_license.md) | AI agent guardrails |
| [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md) | Super Admin tenant license tab |
