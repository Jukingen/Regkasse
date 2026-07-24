# Repository File Map (current)

## AI / proje özü
- Repo kökü: `REGKASSE_AI_ONBOARDING.md` (ana AI onboarding özeti).
- Kısa bağlam paketi: `ai/00_CONTEXT_README.md` ve bu dosyanın altındaki klasör yolları.

## Backend (`backend/`)
- Tenancy: `backend/Tenancy/` (`SubdomainTenantProvider`, `CurrentTenantService`, `TenantHostNames`, `ITenantDomainService` / TenantDomain)
- Tenant middleware: `backend/Middleware/TenantResolutionMiddleware.cs`, `TenantContextMiddleware.cs`
- Controllers: `backend/Controllers/`
- Super Admin tenants: `backend/Controllers/AdminTenantsController.cs`, `backend/Services/AdminTenants/`
- Digital / sites / online orders: public + sites controllers; `OnlineOrderIntakeService` — `docs/DIGITAL_SERVICES.md`, `docs/ONLINE_ORDERS.md`
- Billing / mandant license sales: `backend/Services/Billing/`, `backend/Services/Hosted/BillingReminderHostedService.cs`, `backend/Controllers/AdminBillingController.cs`, `AdminLicenseController.Extend.cs` — see `docs/BILLING_TENANT_LICENSE.md`, `docs/BILLING_TESTING.md`, `ai/modules/billing_license.md`
- Billing tests: `backend/KasseAPI_Final.Tests/Billing/` (`BillingServiceTests`, `BillingServiceTestHarness`)
- Services: `backend/Services/` (singleton `LicenseService` + `IServiceScopeFactory` DB pattern)
- TSE fiscal + Super Admin ops: `backend/Services/Tse/`, `backend/Controllers/AdminTse*.cs` — agent: `ai/modules/tse_finanzonline.md` (core), `ai/modules/tse_admin_ops.md` (ops); inventory: `docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`
- License registration: `backend/Services/LicenseServiceRegistration.cs`
- Authorization: `backend/Authorization/`
- EF context: `backend/Data/AppDbContext.cs`
- Migrations: `backend/Migrations/`
- OpenAPI contract: `backend/swagger.json`
- Backend tests: `backend/KasseAPI_Final.Tests/`

## POS Frontend (`frontend/`)
- Expo Router app: `frontend/app/`
- API services: `frontend/services/api/`
- Contexts: `frontend/contexts/`
- POS tests: `frontend/__tests__/`

## Admin Frontend (`frontend-admin/`)
- Next App Router pages: `frontend-admin/src/app/`
- Super Admin TSE ops UI: `frontend-admin/src/app/(protected)/admin/tse/*/`, features `frontend-admin/src/features/tse-*` — `ai/modules/tse_admin_ops.md`
- Auth gate: `frontend-admin/src/proxy.ts` (Next.js 16)
- Generated API client: `frontend-admin/src/api/generated/`
- Admin API boundary helpers: `frontend-admin/src/api/admin/`
- Axios mutator: `frontend-admin/src/lib/axios.ts`
- Orval config/transformer: `frontend-admin/orval.config.ts`, `frontend-admin/scripts/orval-strip-legacy-paths.cjs`
- Toasts: `frontend-admin/src/hooks/useNotify.ts`, `src/lib/notificationService.ts`

## Customer sites (`frontend-sites/`)
- Shared storefront: `frontend-sites/` — `/[slug]`; public catalog / order APIs. See `frontend-sites/README.md`.

## CI & verification
- Workflows: `.github/workflows/*.yml` (inventory: `.github/workflows/README.md`)
- OpenAPI/Orval checks: `scripts/verify-api-client.mjs`, `scripts/validate-critical-openapi-paths.mjs`
- Localization checks: `localization/scripts/*.mjs`
