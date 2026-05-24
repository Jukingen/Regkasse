# AGENTS.md

## Purpose
This repository is a POS monorepo. Prefer safe, incremental improvements over broad rewrites. Follow real package boundaries, preserve current architecture unless explicitly asked otherwise, and make the smallest safe change that satisfies the task.

## Language rules
Follow these language rules strictly:

- Code identifiers must be in English.
- Code comments must be in English.
- POS user interface texts must remain in German.
- Do not translate POS UI text into English or Turkish.
- When explaining plans, changes, or reviews in the IDE, respond in Turkish.

## Working style
- Prefer minimal, targeted changes over broad refactors.
- Preserve existing architecture and naming conventions unless explicitly asked for restructuring.
- Before editing, inspect nearby files and follow local patterns.
- Do not invent commands, package relationships, or framework conventions.
- State uncertainty explicitly when repo evidence is missing.
- Do not mix unrelated refactors into feature work.
- Prefer controlled evolution, small reversible steps, and behavior-preserving refactors.
- Prefer updating existing code paths over introducing parallel implementations.

## Repo map
- `backend/`
  - Main ASP.NET Core API.
  - Owns auth, authorization, domain logic, persistence, fiscal/TSE/RKSV behavior, reporting, and OpenAPI contract.
- `frontend/`
  - Mobile POS client.
  - Expo Router + React Native + TypeScript.
- `frontend-admin/`
  - Admin panel.
  - Next.js + TypeScript + Ant Design + TanStack Query + mixed generated/manual/legacy API boundaries.
- `localization/`
  - Shared i18n import/export/validation tooling.
- `scripts/`
  - Cross-repo validation and consistency scripts.
- `.github/workflows/`
  - CI source of executable truth.
- `docs/`
  - Human documentation and reference material.
- `ai/`
  - Internal implementation and guardrail docs that must be read before medium/high-risk work.

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `cafe.regkasse.at`, `bar.regkasse.at`, `market.regkasse.at`
- Super Admin accesses `admin.regkasse.at`

### Data Isolation

- Tenant-scoped tables use non-null `tenant_id` on entities implementing `ITenantEntity`
- Entity Framework global query filters in `AppDbContext` automatically filter by `ICurrentTenantAccessor.TenantId`
- Tenants can NEVER see other tenants' data
- Cross-tenant access attempts return HTTP 404

### Development Mode

- Localhost: use `X-Tenant-Id` header or `?tenant=` query parameter
- Or add dev tenant selector in FA (visible only in development)
- Or use localhost subdomains via hosts file (`*.regkasse.local`)

### Super Admin

Access: `admin.regkasse.at` (subdomain; host slug `admin` maps to legacy default tenant for operational APIs until impersonation).

**Permissions (implemented):**

- View all tenants via `GET /api/admin/tenants` (not all tenants’ business data without impersonation)
- Create, edit, soft-delete tenants (`POST` / `PUT` / `DELETE /api/admin/tenants`)
- Suspend/reactivate via `status` (`active` / `suspended`) and `isActive` on `PUT`
- Set tenant-level `licenseKey` / `licenseValidUntilUtc` on tenant row; issued licenses via `/admin/license` (tenant-scoped unless impersonating)
- Impersonate any active tenant for support (`POST /api/admin/tenants/{tenantId}/impersonate`)
- Platform-wide license dashboard metrics exist per tenant context; cross-tenant SaaS metrics API is not yet a separate surface

**Impersonation flow:**

1. Super Admin clicks “Login as” on `/admin/tenants` (FA).
2. Backend issues JWT with target `tenant_id` claim and `tenant_impersonation=true`.
3. **Current FA behavior:** same origin — token stored, `dev_tenant_id` set, page reload (dev). **Production target:** redirect to `https://{slug}.regkasse.at` with token handoff (not fully implemented).
4. Subsequent API calls use tenant-scoped JWT; EF filters apply to target tenant.
5. Structured server logs record actor + tenant; dedicated `impersonated_by` audit column is **not** yet on `AuditLog` (planned).

Requires `SuperAdmin` role (`[Authorize(Roles = SuperAdmin)]` on `AdminTenantsController`).

### Multi-Tenant Security

- **Isolation:** EF global query filters on `ITenantEntity`; clients cannot bypass via query params.
- **IDOR:** Cross-tenant resource access returns **HTTP 404** (not 403). See `TenantIsolationTests`.
- **Production resolution:** Subdomain/`Host` only; `X-Tenant-Id` and `?tenant=` disabled when `ASPNETCORE_ENVIRONMENT` is not Development.
- **JWT vs host:** After auth, `TenantContextMiddleware` may override accessor from JWT `tenant_id`; strict host↔JWT binding in production is **not** yet enforced (see `docs/MULTI_TENANT.md`).
- **Offline queue:** `offline_transactions.tenant_id` set on insert (from cash register / ambient tenant); preserved through replay.

### Migrating existing databases

Wave migrations (not a single `AddTenantIdToAllTables`): `AddTenantsAndSettingsTenantId`, Wave2/3A/3B, `AddTenantIdToFiscalAndAuditTables`. Legacy rows backfilled with `LegacyDefaultTenantIds.Primary` (Guid), not the string `legacy`. See `ai/02_DATABASE_CONTRACT.md` and `docs/MULTI_TENANT.md`.

When changing persistence, auth, or API handlers, read `REGKASSE_AI_ONBOARDING.md`, **`docs/MULTI_TENANT.md`**, and `ai/02_DATABASE_CONTRACT.md` / `ai/03_API_CONTRACT.md` before editing query filters or tenant middleware order.

Production deploys need wildcard DNS `*.regkasse.at`, wildcard TLS, and `ASPNETCORE_ENVIRONMENT=Production` (no dev tenant header overrides).

Local multi-tenant smoke: `curl -H "X-Tenant-Id: cafe" http://localhost:5184/api/health` or `?tenant=cafe` (Development only; slug not UUID). Legacy `test_cafe`/`test_bar` alias to `cafe`/`bar`. POS: `EXPO_PUBLIC_DEV_TENANT_ID=dev|cafe|bar`. FA: header tenant dropdown in dev.

### Tenant resolution in background services

- Startup and singleton hosted services have **no HTTP request**; `ICurrentTenantAccessor.TenantId` may be null (EF filters off for `ITenantEntity` only on intentional paths).
- Singletons that need EF must use **`IServiceScopeFactory.CreateScope()`** before `IDbContextFactory<AppDbContext>` / `AppDbContext` — see `LicenseService`.
- `activated_licenses` is deployment-local (machine fingerprint), not tenant-scoped.

### Scoped service resolution in singleton services

`AppDbContext` + `ICurrentTenantAccessor` are **scoped**. Do not create DbContext from the root provider inside a singleton.

```csharp
using var scope = _scopeFactory.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync(ct);
```

Prevents `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider`. Reference: `backend/Services/LicenseService.cs`.

### License service (singleton)

- `LicenseService` is always **`AddSingleton<LicenseService>()`**; `ILicenseService` → `ProductionLicenseService` (see `LicenseServiceRegistration.cs`).
- DB via **`IServiceScopeFactory`**; in-memory snapshot after `EvaluateOnStartup()` (not `IMemoryCache` in `LicenseService`).
- Startup DB failure → warning + trial/file fallback; does not block host start.

### Testing multi-tenant locally

1. **`X-Tenant-Id` header** (Development only, slug not UUID):

   ```bash
   curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health
   ```

2. **Query parameter:** `?tenant=test_cafe`

3. **Frontend Admin:** dev tenant dropdown in header (`HeaderDevTenantSwitch`); selection in `localStorage` (`dev_tenant_id`).

4. **Hosts file** (subdomain simulation):

   ```text
   127.0.0.1 cafe.regkasse.local
   127.0.0.1 bar.regkasse.local
   ```

   Then: `http://cafe.regkasse.local:5184` (slug = first host label per `TenantHostNames`).

### Troubleshooting

| Error | Cause | Fix |
|-------|--------|-----|
| `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider` | Singleton used root `IDbContextFactory` | `IServiceScopeFactory` + scoped factory |
| `Multiple constructors` on `AppDbContext` | Ambiguous DI constructors | Design-time ctor + `[ActivatorUtilitiesConstructor]` runtime ctor |

Details: `REGKASSE_AI_ONBOARDING.md` §16.1, `docs/MULTI_TENANT.md`.

## Source of truth
When deciding how to work, trust these in order:

1. Actual implementation in the nearest relevant files
2. Package-level config files
3. Root config files
4. CI workflows
5. README, `docs/`, and **`REGKASSE_AI_ONBOARDING.md`** (AI/project brief at repo root)
6. `ai/` guidance docs for domain-specific safety and implementation constraints

If they conflict, follow the most local and executable truth.

## Read before changing code
Before making changes:

1. For project-wide fiscal/RKSV/POS context, read **`REGKASSE_AI_ONBOARDING.md`**; then read the relevant docs under `/ai`
2. Respect compliance and fiscal/TSE/RKSV rules
3. Follow existing repo patterns
4. Preserve backward compatibility unless explicitly told otherwise

For medium or large changes, always provide:
- a short implementation plan
- affected files
- main risks
- backward compatibility impact
- a test strategy

## AI docs routing hints
Use `/ai` docs selectively based on the task:

- Backend/API/auth/contract work:
  - read the backend/API-related docs in `/ai`
- Database/entity/migration/persistence work:
  - read the database contract and persistence-related docs in `/ai`
- Compliance, fiscal, TSE, RKSV, audit, receipt, daily closing work:
  - read the compliance and protected-area docs in `/ai`
- Admin API integration work:
  - read API boundary and admin-related docs in `/ai`
- If unsure:
  - read the closest matching `/ai` docs first and avoid assumptions

## Directory hints
Before editing in each area, inspect these first:

- `backend/`
  - relevant controllers
  - services / use-cases
  - DTOs
  - validators
  - EF entities and mappings
  - migrations
  - tenancy: `Tenancy/`, `Middleware/Tenant*`, `ICurrentTenantAccessor`
  - impacted tests (include `TenantIsolationTests` when touching isolation)
- `frontend/`
  - relevant screens
  - hooks
  - contexts
  - navigation flow
  - API usage
  - impacted tests
- `frontend-admin/`
  - relevant routes/pages
  - feature components
  - hooks and queries
  - generated/manual API usage
  - auth gates
  - impacted tests
  - related i18n keys
- `localization/`
  - validation scripts
  - catalog ownership
  - missing/orphan key rules
  - CI budget or boundary checks

## Rule application model
Apply these rules in this order:

### Always-on baseline
These principles always apply:
- safe incremental changes over rewrites
- compliance and fiscal safety first
- backward compatibility first
- no speculative architecture changes
- explicit risk notes for sensitive flows

### Fiscal compliance (mandatory)
- Check NTP time sync status before treating online fiscal payments as allowed (`NtpSettings` / `NtpTimeSyncStatus`; block when sync failed or offset exceeds configured `MaxAllowedOffsetSeconds`).
- Never queue voucher (Gutschein) payments for offline non-fiscal replay—backend must reject; POS must not enqueue voucher payloads.
- Storno flows must supply **`OriginalReceiptId`** and a **`StornoReason`** where the contract requires them; do not conflate with partial refund.
- DEP-style fiscal export generation/download may require disclaimer acknowledgment: send **`X-Disclaimer-Acknowledged: true`** when `FiscalExportOptions.RequireDisclaimerAcknowledgment` is enabled.

### Context-driven rules
When touching backend or persistence, increase caution around:
- controllers, services, use-cases
- EF Core entities and mappings
- migrations and schema evolution
- auditability and compliance behavior
- API contracts and DTOs

### Path-specific rules
When touching `frontend/**`:
- use Expo / React Native patterns only
- avoid web-only abstractions and browser-specific assumptions
- avoid growing orchestration-heavy screens and overloaded contexts

When touching `frontend-admin/**`:
- respect generated/manual/legacy API boundaries
- preserve existing auth and route protection patterns
- avoid importing React Native / Expo patterns into admin web

## Do not
- Do not introduce a parallel architecture or broad rewrite.
- Do not casually change API contracts, auth behavior, role names, or payment flows.
- Do not mix unrelated refactors into feature work.
- Do not rename or reshape public APIs, DTOs, config keys, or role semantics without checking downstream consumers.
- Do not weaken validation, auditability, authorization, or fiscal guarantees.
- Do not commit secrets.

## High-risk flows
Treat these as high-risk and change them only with explicit scope and careful validation:

- Cart → Payment → Receipt → DailyClosing
- pricing and modifier behavior
- table cart switching and recovery
- inventory / payment / order synchronization
- TSE / RKSV signing and auditability
- auth / RBAC behavior

## Repository guidance
### Backend
- Keep controllers thin.
- Prefer service or use-case extraction over controller bloat.
- Preserve current response and error-shape conventions.
- Treat migrations, money logic, receipt lifecycle, daily closing, and fiscal integrations as sensitive.

### Frontend POS
- Avoid growing orchestration screens.
- Avoid overloaded contexts.
- Keep user flows clear, stable, and resilient.
- Preserve contract compatibility with backend POS endpoints.

### Frontend Admin
- Respect generated, manual, and legacy API boundaries.
- Reuse established query, auth, and route protection patterns.
- Prefer compact, clear operator-facing feedback over noisy UI warnings.

## Validation expectations
Choose the smallest safe validation set for the touched area, and expand it when risk increases.

### Root-level validation commands
Run from repository root when relevant:

```bash
node scripts/verify-api-client.mjs
node scripts/validate-critical-openapi-paths.mjs
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
node localization/scripts/check-translation-boundary.mjs --app frontend-admin
node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json