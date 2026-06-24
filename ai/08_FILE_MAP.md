# Repository File Map (current)

## AI / proje özü
- Repo kökü: `REGKASSE_AI_ONBOARDING.md` (ana AI onboarding özeti).
- Kısa bağlam paketi: `ai/00_CONTEXT_README.md` ve bu dosyanın altındaki klasör yolları.

## Backend (`backend/`)
- Tenancy: `backend/Tenancy/` (`SubdomainTenantProvider`, `CurrentTenantService`, `TenantHostNames`)
- Tenant middleware: `backend/Middleware/TenantResolutionMiddleware.cs`, `TenantContextMiddleware.cs`
- Controllers: `backend/Controllers/`
- Super Admin tenants: `backend/Controllers/AdminTenantsController.cs`, `backend/Services/AdminTenants/`
- Billing / mandant license sales: `backend/Services/Billing/`, `backend/Services/Hosted/BillingReminderHostedService.cs`, `backend/Controllers/AdminBillingController.cs`, `AdminLicenseController.Extend.cs` — see `docs/BILLING_TENANT_LICENSE.md`, `docs/BILLING_TESTING.md`, `ai/modules/billing_license.md`
- Billing tests: `backend/KasseAPI_Final.Tests/Billing/` (`BillingServiceTests`, `BillingServiceTestHarness`)
- Services: `backend/Services/` (singleton `LicenseService` + `IServiceScopeFactory` DB pattern)
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
- Generated API client: `frontend-admin/src/api/generated/`
- Admin API boundary helpers: `frontend-admin/src/api/admin/`
- Axios mutator: `frontend-admin/src/lib/axios.ts`
- Orval config/transformer: `frontend-admin/orval.config.ts`, `frontend-admin/scripts/orval-strip-legacy-paths.cjs`

## CI & verification
- Workflows: `.github/workflows/*.yml`
- OpenAPI/Orval checks: `scripts/verify-api-client.mjs`, `scripts/validate-critical-openapi-paths.mjs`
- Localization checks: `localization/scripts/*.mjs`
