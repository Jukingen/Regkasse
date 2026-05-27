# Regkasse — AI Onboarding / Project Brief

> Purpose: Use this document as the first context message for a new ChatGPT or Cursor session. It summarizes the project goal, architecture, domain rules, fiscal/RKSV constraints, development standards, and AI guardrails.

> **Primary AI brief:** Treat this file as the main onboarding source of truth for Regkasse; use `/ai/*.md` for deeper contracts and policies. This document is based on repository evidence. If a detail is unclear or not proven by code, it may still be marked as `UNKNOWN`—do not invent behavior beyond this document and the current repository.

> **Legal safety:** Nothing here is a legal compliance guarantee. Fiscal export output is diagnostic / internal analysis only, not an official RKSV proof.

---

## 1. Project Identity

### Project name

- Repository / working name: **Regkasse**
- POS package name: **cash-register**
- Admin package name: **registrierkasse-admin**

### Purpose

Regkasse is a full-stack Austrian POS / cash register system focused on:

- POS sales and payments
- RKSV / fiscal compliance
- TSE signing and signature chaining
- Receipts and invoices
- Admin reporting and audit views
- Gutschein / voucher management
- RKSV special receipts
- Offline replay and operational resilience

### Target users

- Cashiers / POS operators
- Store or restaurant managers
- Admin users
- Fiscal / compliance operators
- Business owners or staff responsible for RKSV operations

Repository evidence does not define a single commercial target segment.
The system supports table-service hospitality (Waiter/Kitchen roles, tables/orders)
as well as general Austrian RKSV retail operations (products, inventory, reporting).

Treat vertical positioning as implementation-driven, not a fixed product contract.

---

## 2. Product Goals

The project aims to provide a production-grade POS system that can:

1. Process sales through a mobile POS frontend.
2. Generate signed fiscal receipts.
3. Maintain a valid receipt number sequence and signature chain.
4. Support Austrian RKSV special receipts:
   - Nullbeleg
   - Startbeleg
   - Monatsbeleg
   - Jahresbeleg
   - Schlussbeleg / Endbeleg
5. Provide Gutschein / voucher creation, redemption, balance tracking, and auditability.
6. Prevent operator mistakes through backend and frontend guardrails.
7. Provide an admin panel for reporting, RKSV status, receipts, payments, vouchers, and special receipt operations.
8. Support fiscal export packages for diagnostics and internal analysis (explicitly not legal RKSV proof; see Fiscal Export section).
9. Keep all critical fiscal operations auditable.

---

## 3. Technical Stack

### Backend

- Language: **C#**
- Runtime / target framework: **.NET 10**
- Web framework: **ASP.NET Core Web API**
- ORM: **Entity Framework Core**
- Database provider: **Npgsql / PostgreSQL**
- Architecture: **monolithic layered API**
- Key backend areas:
  - Controllers
  - Services
  - EF `AppDbContext`
  - Models / DTOs
  - Hosted services
  - Fiscal / TSE signing services
  - FinanzOnline / outbox services

### Frontend POS

- Framework: **Expo / React Native**
- Router: **expo-router**
- Language: **TypeScript**
- State management: **Zustand**
- i18n: **i18next / react-i18next**
- Purpose:
  - Cashier-facing POS
  - Cart and payment flow
  - Receipt printing / display
  - Register readiness and guardrail messaging

### Frontend Admin

- Framework: **Next.js 14**
- Routing: **Next.js App Router**
- UI library: **Ant Design**
- Data fetching: **TanStack Query**
- API client generation: **Orval** from `backend/swagger.json`
- HTTP client: axios mutator pattern
- Purpose:
  - Backoffice/admin operations
  - Reports
  - Voucher management
  - Receipt/payment inspection
  - RKSV Sonderbelege UI
  - FinanzOnline/outbox/reconciliation views

### Database

- PostgreSQL
- EF Core migrations
- Important: migrations must be treated carefully because fiscal records and receipt sequences are legally sensitive.

## Database Schema

### Multi-Tenant Columns

All **tenant-scoped** tables (entities implementing `ITenantEntity`) include a non-nullable `tenant_id` column:

- PostgreSQL: `tenant_id uuid NOT NULL` (FK to `tenants.id`)
- Indexed for query performance (`HasIndex` on `TenantId` in `AppDbContext`)
- Value is the resolved tenant **Guid**, not the subdomain string
- Set per request: host subdomain (or dev `X-Tenant-Id` / `?tenant=` **slug**) → `CurrentTenantService` → `ICurrentTenantAccessor.TenantId`; JWT `tenant_id` claim may override after login

**Not tenant-scoped (examples):** `tenants` (root), ASP.NET Identity user tables, `auth_sessions` / `refresh_tokens` (see `AppDbContext` mappings).

**External tenant key (string):** `tenants.slug` (e.g. `cafe`) — used for subdomain/header resolution only; stored on the tenant row, not duplicated as `VARCHAR` on every child table.

### Global Query Filters

EF Core registers a global query filter on every `ITenantEntity` type:

```text
WHERE tenant_id = @currentTenantId   -- ambient ICurrentTenantAccessor.TenantId (Guid)
```

Implementation: `AppDbContext.CreateTenantQueryFilter<TEntity>()` → `e => _tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId`.

When the accessor has no tenant, the filter is effectively disabled (used only on intentional code paths). Normal API requests always set the accessor before data access.

### Scoped service resolution in singleton services

`AppDbContext` and `ICurrentTenantAccessor` are **scoped**. Singleton services (e.g. `LicenseService`) must **not** call `IDbContextFactory<AppDbContext>.CreateDbContext()` on the root provider — that causes:

`System.InvalidOperationException: Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider.`

**Pattern:** create a scope per database operation:

```csharp
using var scope = _scopeFactory.CreateScope();
var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
// use db ...
```

Reference: `backend/Services/LicenseService.cs` (`EvaluateOnStartup`, `GetCurrentStatusAsync`, `TryRestoreLicenseKeyFromActivatedLicenses`).

**`AppDbContext` constructors (EF Core):**

| Constructor | Use |
|-------------|-----|
| `AppDbContext(DbContextOptions<AppDbContext> options)` | Design-time / migrations (`DesignTimeDbContextFactory`); `NullCurrentTenantAccessor` — filters off |
| `AppDbContext(options, ICurrentTenantAccessor)` | Runtime DI — marked `[ActivatorUtilitiesConstructor]` |

`OnConfiguring` does not call `UseNpgsql` when options are already configured (`ApplicationHost.ConfigureAppDbContextOptions`).

### External services / integrations

- TSE signing service abstraction exists.
- FinanzOnline / RKSV submission/outbox infrastructure exists. FinanzOnline integration is partially implemented:
  - Session service uses SOAP transport with configurable BaseUrl and credentials
  - RKSV submission (Startbeleg/Jahresbeleg) client exists as a guarded skeleton and does NOT perform real outbound requests in this repository version
  - Default configuration uses Fake/Disabled mode. Do not assume production-ready BMF integration.
- BMF Belegcheck App manual verification is part of the operational workflow.

---

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `cafe.regkasse.at`, `bar.regkasse.at`, `market.regkasse.at`
- Super Admin accesses `admin.regkasse.at` (host slug `admin`; requires `SuperAdmin` role for cross-tenant APIs)

### Request pipeline (backend)

1. **`TenantResolutionMiddleware`** — resolves tenant from host via `SubdomainTenantProvider` / `TenantHostNames.GetTenantSlugFromHost`, loads `Tenants` row, sets `ICurrentTenantAccessor.TenantId` (`backend/Tenancy/CurrentTenantService.cs`).
2. **`TenantContextMiddleware`** (after auth) — may override accessor from JWT `tenant_id` claim.
3. **`AppDbContext`** — global query filters on all `ITenantEntity` types: `e.TenantId == ambient TenantId` when set.

### Data Isolation

- Tenant-scoped domain tables implement `ITenantEntity` with non-null `tenant_id uuid` (see **Database Schema** above).
- Entity Framework global query filters automatically scope reads/writes to the current tenant.
- Tenants must not see other tenants' data; cross-tenant resource access returns **HTTP 404** (not 403), so IDs do not leak existence.
- Global tables (e.g. `tenants`, identity users) are not filtered the same way; Super Admin tenant APIs use `SuperAdmin` role + `/api/admin/tenants/*`.

### Development Mode

- **Localhost:** `X-Tenant-Id` header (tenant **slug**) or `?tenant=` query parameter — see **§10 API Headers** and **Development Setup for Multi-Tenant Testing** below (`SubdomainTenantProvider`, Development only).
- **Frontend Admin:** dev tenant selector (development only); presets in `frontend-admin/src/features/auth/constants/devTenantPresets.ts`.
- **Hosts file:** `*.regkasse.local` or other `*.local` dev domains (e.g. `cafe.regkasse.local`) — see `TenantHostNames.IsLocalDevelopmentDomain`.

### Development Setup for Multi-Tenant Testing

**Prerequisites**

- Backend running with `ASPNETCORE_ENVIRONMENT=Development` (default for local `dotnet run` with `appsettings.Development.json`).
- API base URL typically `http://localhost:5184` (see `backend/appsettings.Development.json` / `.example`).
- A `tenants` row whose `slug` matches the value you send (e.g. `test_cafe`, `test_bar`, `dev` for POS presets; `cafe`, `bar`, `dev` for admin presets — see preset files below).

#### Option 1: Header-based (simplest)

Backend reads `X-Tenant-Id` in Development only (`SubdomainTenantProvider.DevTenantHeaderName`). Value is the tenant **slug**, not the UUID.

```bash
# Liveness (no auth); header still parsed in Development
curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health

# Tenant-scoped payments (requires JWT); canonical routes:
curl -H "X-Tenant-Id: test_cafe" -H "Authorization: Bearer <token>" \
  "http://localhost:5184/api/admin/payments?page=1&pageSize=5"
# POS: /api/pos/payment*  (legacy alias families may still answer on /api/Payment*)
```

#### Option 2: Query string

```bash
curl "http://localhost:5184/api/health?tenant=test_cafe"

curl -H "Authorization: Bearer <token>" \
  "http://localhost:5184/api/admin/payments?tenant=test_cafe&page=1&pageSize=5"
```

The POS axios client also appends `?tenant=<slug>` to the dev API base URL (`hydrateDevTenantApiBaseUrl` in `frontend/services/api/config.ts`).

#### Option 3: Localhost subdomains (hosts file)

Add to `C:\Windows\System32\drivers\etc\hosts` (or `/etc/hosts`):

```text
127.0.0.1 test-cafe.localhost
127.0.0.1 test-bar.localhost
```

Then call: `http://test-cafe.localhost:5184/api/health`

**Slug from host:** `TenantHostNames.GetTenantSlugFromHost` uses the **first label** of the hostname (`test-cafe` from `test-cafe.localhost`). That must match `tenants.slug` in the database (use slug `test-cafe` in DB, or prefer header/query with `test_cafe` to match POS presets).

**CORS note:** `*.localhost` is not listed in `IsLocalDevelopmentDomain` (only `*.local` / `*.regkasse.local`). Browser clients on `localhost:3000` calling `test-cafe.localhost:5184` may need extra CORS configuration; header/query on `localhost:5184` is simpler for API smoke tests.

**Production-like alternative:** `127.0.0.1 cafe.regkasse.local` → slug `cafe` (matches admin presets).

#### Option 4: FA tenant switcher

In **development** mode (`NODE_ENV=development`), the admin shell shows a **tenant selector dropdown in the header** (`HeaderDevTenantSwitch` in `frontend-admin/src/app/(protected)/layout.tsx`).

- Presets: `dev`, `cafe`, `bar` (`frontend-admin/src/features/auth/constants/devTenantPresets.ts`)
- Persists `dev_tenant_id` in `localStorage`, sets `X-Tenant-Id` via axios (`frontend-admin/src/lib/axios.ts`), reloads the page on change
- Tooltip documents hosts-file alternative (`dev` / `cafe` / `bar.regkasse.local`)

#### Option 5: POS dev tenant switcher

In **`__DEV__`**, POS shows `DevTenantSwitcher` in the tab layout (`frontend/src/components/dev/DevTenantSwitcher.tsx`).

- Presets: `dev`, `test_cafe`, `test_bar` (`frontend/constants/devTenantPresets.ts`)
- Persists slug and refreshes API base URL + headers

**Verify isolation:** `dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~TenantIsolation"`

### Super Admin

Access: **`admin.regkasse.at`** (host slug `admin`; operational business APIs use legacy default tenant until impersonation).

#### Super Admin capabilities

| Capability | API / UI | Notes |
|------------|----------|--------|
| List tenants | `GET /api/admin/tenants` | Optional `includeDeleted`; FA `/admin/tenants` |
| Create tenant | `POST /api/admin/tenants` | Unique `slug`; seeds `status=active` |
| Edit tenant | `PUT /api/admin/tenants/{id}` | Name, contact, license fields on tenant row |
| Suspend / reactivate | `PUT` with `status` | `suspended` sets `isActive=false`; `active` re-enables |
| Soft-delete | `DELETE /api/admin/tenants/{id}` | `status=deleted`; legacy default tenant cannot be deleted |
| Issue licenses | `/admin/license` + tenant `licenseKey` | Issued-license flows are tenant-scoped; use impersonation for another tenant’s context |
| Impersonate | `POST /api/admin/tenants/{tenantId}/impersonate` | JWT + `tenant_impersonation` claim |
| Create tenant users | `POST /api/admin/tenants/{tenantId}/users` | One-time `generatedPassword`; no invitation email |
| Create platform users | `POST /api/admin/users` (no `tenantId`) | Super Admin staff; same password handoff |
| Assign existing user | `POST /api/admin/tenants/{tenantId}/users/assign` | Membership only — no new Identity row |
| System-wide metrics | — | Per-tenant dashboard/reports only; no dedicated cross-tenant SaaS metrics API yet |

**Roles:** `SuperAdmin` on `AdminTenantsController`; tenant user endpoints also accept `[HasPermission(user.manage)]` (Managers on their tenant where policy allows).

#### User management (direct creation)

- **No email invitations** for mandant or platform users (removed 2026-05-22).
- FA: `CreateUserModal` + `useCreateUser` / `createUser` in `frontend-admin`.
- Backend: `TenantUserService.CreateAsync`, `TenantUserService.CreateQuickAsync`, `AdminUsersController.Create` → `PasswordGenerator.GenerateSecurePassword`, `UserCreationService` / `UniqueUsernameGenerator` for login names.
- **Add existing user:** `AddExistingUserModal` — assign membership only.
- Audit: `USER_CREATED` with `createdByUserId`, `tenantId`, `role`; password **not** in audit metadata.
- Optional `Email:Smtp` is for **onboarding welcome** only — see `docs/CUSTOMER_ONBOARDING.md`, `backend/CONFIGURATION.md`.

##### Creating users with username

When creating a user via Admin API or FA:

- If **`userName`** is provided, it must be unique across all users **case-insensitively** (`IUserUniquenessValidationService.IsUserNameTakenByOtherUserAsync` → `IdentityLoginLookup` / `NormalizedUserName`).
- If **`userName`** is omitted, the system generates one automatically (`{rolePrefix}{n}`, e.g. `manager1`, `cashier2`).
- **`email`** remains the contact/login email field and may differ from `userName` (especially Quick Create: auto email like `cashier_a3f9k2@cafe.regkasse.at`).
- API responses include **`userName`** and **`email`** for operator handoff (`CreateTenantUserResultDto`, `AdminCreateUserResponseDto`).
- FA success modals show username, email, and one-time password with copy actions (`QuickUserSuccessModal`, Schnell anlegen tab).

Request DTOs: `CreateTenantUserRequest`, `CreateQuickTenantUserRequest`, `AdminCreateUserRequest` (`userName` optional).

Full guide: **`docs/USER_MANAGEMENT.md`**.

**Role:** `SuperAdmin` only on `AdminTenantsController` for tenant CRUD (`[Authorize(Roles = SuperAdmin)]`).

#### Impersonation flow

1. Super Admin clicks **Login as** on a tenant row (FA).
2. Backend returns `TenantImpersonationResponseDto` (`token`, `tenantSlug`, `refreshToken`, …).
3. **Current FA:** `applyTenantImpersonationSession` stores token, sets `dev_tenant_id`, reloads same origin. **Target production UX:** redirect to `https://{slug}.regkasse.at` with token handoff (not fully implemented).
4. Authenticated calls use target tenant’s `tenant_id` in JWT; EF global filters scope data.
5. Server logs record actor user id + tenant; **`impersonated_by` on `AuditLog` is not yet implemented** (do not document as present).

Cannot impersonate deleted, suspended, or inactive tenants.

### Multi-Tenant Security

#### Tenant isolation guarantees

- **Database-level filtering:** EF Core global query filters on all `ITenantEntity` types (`AppDbContext.CreateTenantQueryFilter`). API clients cannot pass a filter to bypass this.
- **Cross-tenant IDOR:** Returns **HTTP 404** (not 403) so resource existence does not leak. Verified in `TenantIsolationTests`.
- **Production tenant resolution:** Subdomain from `Host` via `SubdomainTenantProvider` / `TenantHostNames.GetTenantSlugFromHost`.
- **Offline queue:** `offline_transactions.tenant_id` is NOT NULL; stamped on insert from cash register / ambient tenant; preserved on replay.

#### Tenant spoofing prevention

- **Production:** Only subdomain-based resolution; `X-Tenant-Id` and `?tenant=` are ignored unless `IsDevelopment()`.
- **Super Admin endpoints:** Additional `SuperAdmin` role requirement on `/api/admin/tenants/*`.

#### Known gap (document accurately)

- **JWT vs host:** `TenantContextMiddleware` may set `ICurrentTenantAccessor.TenantId` from JWT `tenant_id` after `TenantResolutionMiddleware` resolved host. Strict validation that JWT tenant matches host subdomain in production is **not** enforced yet. See `docs/MULTI_TENANT.md`.

### Tenant resolution in background / startup (no HTTP request)

During application startup or hosted background work there is often **no HTTP context** and no tenant slug on the accessor.

- Use **`IServiceScopeFactory.CreateScope()`** before resolving `AppDbContext` or `IDbContextFactory<AppDbContext>`.
- When `ICurrentTenantAccessor.TenantId` is null, EF global filters on `ITenantEntity` are **disabled** — intentional for cross-tenant admin paths only; normal requests must set the accessor first.
- **Deployment-local license data** (`activated_licenses`) is **not** `ITenantEntity`-scoped; startup license reads filter by **machine fingerprint**, not tenant.
- On DB failure at startup, `LicenseService` logs a warning and falls back to **trial / on-disk state**; it does **not** block Kestrel from starting.

### License service architecture

`LicenseService` is registered as a **singleton** (`LicenseServiceRegistration.AddLicenseServices`); `ILicenseService` is exposed via **`ProductionLicenseService`** (dev: singleton adapter; production: scoped adapter delegating to the inner singleton).

| Concern | Implementation |
|---------|----------------|
| In-process state | Thread-safe `_snapshot` / `_persisted` after `EvaluateOnStartup()` |
| Database access | **`IServiceScopeFactory`** + scoped `IDbContextFactory<AppDbContext>` (never root factory) |
| Persistence | Encrypted file (`ILicenseStorageService`) + `activated_licenses` rows |
| Startup | `EvaluateOnStartup()` reads DB inside a scope; failures → trial snapshot + warning log |
| HTTP status | `GetCurrentStatusAsync()` refreshes from DB in a scope when possible |

`IMemoryCache` is registered in `ApplicationHost` for other features; **`LicenseService` does not cache license status in `IMemoryCache`** (in-memory singleton snapshot only).

### Migrating existing databases

For existing single-tenant PostgreSQL databases, apply the **existing migration chain** (wave approach), not a single hypothetical `AddTenantIdToAllTables`:

| Phase | Example migration |
|-------|-------------------|
| Tenants table + default row | `20260403190133_AddTenantsAndSettingsTenantId` |
| Memberships / sessions | `UserTenantMemberships`, `AuthSessionTenantId` |
| Wave 2–3B domain | `Wave2TenantScoped…`, `Wave3A…`, `Wave3B…` |
| Fiscal / audit / offline | `20260516101549_AddTenantIdToFiscalAndAuditTables` |
| Super Admin tenant columns | `20260516104349_ExtendTenantsForSuperAdmin` |

**Backfill pattern in migrations:** add `tenant_id uuid NOT NULL` with `defaultValue: LegacyDefaultTenantIds.Primary` (Guid constant), then indexes. Do not use string `'legacy'` as the column default.

```bash
dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

Full guide: **`docs/MULTI_TENANT.md`**.

### Client expectations

- **POS (`frontend/`):** persist `tenant_id` / `tenant_slug` from login/license bootstrap; send `X-Tenant-Id` in Development when API base URL is loopback (`frontend/services/tenant/tenantStorage.ts`).
- **Admin (`frontend-admin/`):** same header pattern for dev; Super Admin UI under `/admin/tenants` when permitted.

### POS Tenant Configuration

#### Production

- POS receives `tenantId`, `tenantSlug`, and `apiBaseUrl` from **license activation** (`POST /api/license/activate` → `tenantStorage.persistBootstrap` in `frontend/api/license.ts`).
- Values are stored in secure/async storage (`tenant_id`, `tenant_slug`, `api_base_url` keys in `frontend/services/tenant/tenantStorage.ts`).
- Production API traffic should use the bootstrap URL, typically `https://{tenant}.regkasse.at/api` (or the exact `apiBaseUrl` returned by activation).
- Axios sends requests to that base URL; tenant slug from bootstrap is used when resolving headers.

#### Development

- Set in `frontend/.env` (see `.env.example`):

```env
EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api
EXPO_PUBLIC_DEV_TENANT_ID=test_cafe
```

- In `__DEV__`, effective slug order: **DevTenantSwitcher / `dev_tenant_id` storage override** → **`EXPO_PUBLIC_DEV_TENANT_ID`** (defaults to `dev` if unset) → login/license persisted slug.
- POS axios (`frontend/services/api/config.ts`) automatically:
  - adds **`X-Tenant-Id: <slug>`** on every request when a slug resolves, and
  - can append **`?tenant=<slug>`** to the dev base URL via `hydrateDevTenantApiBaseUrl()`.

Restart Metro after changing `.env`.

---

## 4. Repository Structure

Typical high-level structure:

```text
backend/                  ASP.NET Core API, EF Core, fiscal services, Tenancy/
frontend/                 Expo / React Native POS app
frontend-admin/           Next.js admin panel
docs/                     RKSV, operations, receipt, audit, workflow docs
localization/             Translation tooling and validation scripts
scripts/                  API client validation and helper scripts
.github/workflows/        CI workflows
ai/                       AI/project context files, if present
```

The backend OpenAPI contract is generated into:

```text
backend/swagger.json
```

The frontend-admin API client is generated into:

```text
frontend-admin/src/api/generated/**
```

Whenever backend API changes affect frontend-admin, update both `backend/swagger.json` and the generated Orval client.

---

## 5. Core Domain Model

### Payment / `PaymentDetails`

Represents the fiscal payment/sale record.

Important responsibilities:

- Stores total amount and tax amount
- Links to cash register
- Stores payment method
- Stores TSE signature data
- Stores receipt number
- Stores RKSV special receipt metadata
- Supports refund/storno references
- Supports offline replay metadata
- Supports voucher redemption linkage through related logic

Important fields/concepts:

- `TotalAmount`
- `TaxAmount`
- `PaymentMethodRaw`
- `CashRegisterId`
- `TseSignature`
- `PrevSignatureValueUsed`
- `TseTimestamp`
- `TaxDetails`
- `PaymentItems`
- `ReceiptNumber`
- `IdempotencyKey`
- `RksvSpecialReceiptKind`
- `RksvSpecialReceiptYear`
- `RksvSpecialReceiptMonth`
- `RksvNullbelegActsAsJahresbeleg`
- `IsRefund`
- `IsStorno`
- `StornoReason` (enum: e.g. wrong amount, customer cancel, technical, other)
- `OriginalPaymentId`
- `OriginalReceiptId` (expected for storno flows when enforcing linkage)

### Receipt

Represents the printed/displayable fiscal receipt.

Important responsibilities:

- Stores receipt number
- Stores issue date/time
- Stores cash register identity
- Stores items and tax lines
- Stores QR payload
- Stores signature value
- Stores previous signature value
- Stores parsed JWS signature parts

### Invoice

Represents the invoice document / accounting-facing document.

Important responsibilities:

- Links to source payment
- Stores company/customer snapshots
- Stores TSE signature
- Stores Kassen-ID
- Stores tax details
- Supports document type / credit note / storno fields

### Voucher / Gutschein

Represents a stored-value voucher.

Important rules:

- Raw voucher codes must not be stored in plaintext.
- Voucher codes are represented via hash and masked display code.
- Voucher balance must be tracked.
- Voucher redemption must be auditable through ledger entries.
- Voucher payments require online validation.
- Voucher codes must not be persisted in local offline queues or logs.

Core concepts:

- `Voucher`
- `VoucherLedgerEntry`
- `CodeHash`
- `MaskedCode`
- `InitialAmount`
- `RemainingAmount`
- `Status`
- `ValidFromUtc`
- `ExpiresAtUtc`
- Ledger transaction types such as Issue, Redeem, Cancel, Refund if implemented

### Cash Register / Kassa

Represents a physical or logical cash register.

Important fields/concepts:

- `RegisterNumber`
- `Location`
- `StartingBalance`
- `CurrentBalance`
- `Status`
- `CurrentUserId`

Important statuses:

- Closed
- Open
- Maintenance
- Disabled
- Decommissioned

Critical rule:

A **Decommissioned** register must not accept new sessions, payments, or receipts.

---

## 6. RKSV Special Receipts

RKSV special receipts are implemented as signed zero-value receipts using the normal receipt number sequence and TSE/signature chain.

Supported special receipt types:

- Nullbeleg
- Startbeleg
- Monatsbeleg
- Jahresbeleg
- Schlussbeleg / Endbeleg

They are represented through `PaymentDetails` with `RksvSpecialReceiptKind` and related metadata.

### Nullbeleg

Purpose:

- Zero-value RKSV receipt.
- Used for monthly/control purposes depending on workflow.
- Must not affect revenue.
- Must be signed and part of the signature chain.

### Startbeleg

Purpose:

- Initial RKSV receipt after RKSV setup / before legal fiscal operation.
- Must be unique per cash register.
- Used for initial verification / registration workflow.
- Integrated with RKSV/FinanzOnline tracking infrastructure where available.

Critical rule:

A register requiring Startbeleg must not allow normal sales before Startbeleg is created.

### Monatsbeleg

Purpose:

- Monthly zero-value control receipt.
- Must be unique per register/month.
- Must not affect revenue.

### Jahresbeleg

Purpose:

- Annual zero-value control receipt.
- December Monatsbeleg may act as Jahresbeleg depending on business rules.
- Must be unique per register/year.
- BMF Belegcheck manual workflow is relevant.

### Schlussbeleg / Endbeleg

Purpose:

- Permanent decommissioning receipt for a cash register.
- Must only be created when the register is closed and no active session exists.
- After Schlussbeleg, the register becomes Decommissioned.
- Decommissioned register must not allow new sessions or payments.

Critical rule:

Schlussbeleg is **not** for holidays, breaks, or seasonal pauses. It is for planned permanent decommissioning.

---

## 7. Fiscal / RKSV Logic

### TSE signing flow

Fiscal signing flow is centralized through the backend fiscal/TSE service layer.

Typical flow:

1. Payment or special receipt creation begins.
2. Receipt number is allocated.
3. TSE signing payload is built.
4. Previous signature is loaded from signature chain state.
5. Payload is signed as compact JWS.
6. TSE signature is stored.
7. Signature chain state is updated.
8. Payment / invoice / receipt are committed atomically.

### Signature chain

Important rule:

Each fiscal receipt must be chained to the previous receipt signature. The previous signature value must be preserved and used for integrity checks.

### Receipt numbers

Receipt numbers should be allocated through the existing receipt sequence service.

Critical rule:

RKSV special receipts must use the normal receipt number sequence. Do not invent a separate sequence unless explicitly required and reviewed.

### TSE unavailable behavior

- Fiscal payments must not silently succeed without a required signature.
- Special receipts must not be created when TSE signing is unavailable according to current TSE mode/policy.
- NON_FISCAL behavior must not occur accidentally in production flows.

### FinanzOnline / BMF

Current position (implementation-based):

- Manual BMF Belegcheck App workflow remains operationally important.
- Session webservice path uses configurable SOAP transport when credentials and `BaseUrl` are supplied (see backend FinanzOnline session integration); this is not the same as “full production RKSV submission” completeness.
- RKSV Startbeleg/Jahresbeleg **submission** client exists as a **guarded skeleton** in this repository revision: it does **not** perform real outbound SOAP for that path by default (`FinanzOnline:RksvSubmission` defaults to Fake/Disabled in examples). Do not describe that path as production-complete BMF integration.
- Startbeleg and Jahresbeleg are the special receipt types with explicit FinanzOnline **outbox/tracking** plumbing in code; other special receipt types may still rely on manual Belegcheck depending on operator workflow.

Do not claim legal completeness or guaranteed FinanzOnline acceptance.

### DEP / export

Fiscal export builds JSON (optional CSV) packages for diagnostics, accounting handoff, and internal analysis. Every profile carries an explicit **not legal proof** notice in the payload (`NotLegalProofNotice` / documented as `notLegalProofNotice` in onboarding lists). This is **not** an official DEP substitute or legal RKSV attestation—see `FiscalExportService` and admin fiscal-export endpoints.

**Disclaimer acknowledgment (when enabled):** `FiscalExportOptions.RequireDisclaimerAcknowledgment` gates generate/download actions until the client sends `X-Disclaimer-Acknowledged: true` (see `RequireDisclaimerAcknowledgmentFilter`, `FiscalExportDisclaimerHeaders`). Operators obtain disclaimer text via `GET /api/admin/fiscal-export/disclaimer` (`FiscalExportDisclaimerPaths`); failed attempts without the header may be logged.

### QR payload / receipt QR (voucher-heavy payloads)

RKSV QR image generation (`QrImageService`) tries **ECC levels M then L**, sweeping **QR versions 10–20 then 1–9** so long voucher-heavy RKSV strings still encode. If the string remains too large, it may **truncate UTF-8** to a safe byte budget (~2200) and retry—a last resort for printable QR reliability, not a semantic change to signing.

### RKSV compliance reminders (Monatsbeleg / Jahresbeleg / Startbeleg)

`RksvReminderService` exposes consolidated status for a register: **Startbeleg** missing vs present, **Monatsbeleg** for current/previous Vienna calendar months (ok / upcoming / overdue), and **Jahresbeleg** expectations respecting `CompanySettings.UseDecemberMonatsbelegAsJahresbeleg`. POS and admin consume this to warn before illegal gaps widen.

### NTP time synchronization

A background NTP sync loop (`NtpTimeSyncService`, `NtpSynchronizationCoordinator`) records drift. When `NtpSettings.Enabled` is true, **online fiscal payments** are rejected if the last sync failed, no offset is known, or **`|offset| > MaxAllowedOffsetSeconds`** (see `NtpTimeSyncStatus.ShouldAllowOnlineFiscalPayment`; example default in `appsettings.example.json` is **5** seconds). **`CriticalOffsetSeconds`** (example default **60**) drives warning severity in `/api/system/time/status` and operator-facing banners—not the same numeric threshold as the payment gate unless configuration aligns them.

### TSE health monitoring and offline replay

TSE availability is tracked with configurable health intervals and failure counts (`TseOptions`: e.g. `HealthCheckIntervalSeconds`, offline/degraded thresholds). **Voucher (Gutschein) payments** must not enter the server **NonFiscalPending** offline queue when TSE is unavailable—backend rejects that path (`PaymentService`); other methods may still queue per policy. Admin surfaces TSE health and offline queue summaries where implemented.

### Storno vs refund (classification)

**Full receipt cancellation (Storno)** is classified by **`StornoReason`** on `PaymentDetails` and must be tied to the original receipt via **`OriginalReceiptId`** (and related fields) when applicable—distinct from partial **refund** flows. APIs/contracts map these explicitly for audit (`PaymentDTOs`, admin payment audit views).

---


## 8. Payment Flow

### Normal POS payment

Typical flow:

1. Cashier adds products to cart.
2. POS opens payment flow.
3. POS submits payment to backend.
4. Backend validates register readiness.
5. Backend validates payment method.
6. Backend validates TSE requirements.
7. Backend signs fiscal payload.
8. Backend creates PaymentDetails, Invoice, Receipt.
9. Backend commits transaction.
10. POS receives receipt/payment result.
11. Receipt can be printed or inspected in admin.

### Voucher payment

Rules:

- Voucher payment requires a voucher code or voucher redemption payload.
- Voucher must be validated online.
- Offline voucher payments are blocked.
- Voucher balance must not go below zero.
- Voucher redemption must be recorded in ledger.
- Voucher code must not be logged, stored in offline queue, or exposed after creation.
- Admin should see masked voucher code and ledger history, not raw code.

### Offline behavior

Known backend concept:

- Offline transaction replay exists.

Critical rule:

Do not queue voucher payments offline with raw voucher code.

POS stores NON_FISCAL_PENDING transactions in a local queue
(@regkasse/offline_transactions_v1 via AsyncStorage/localStorage).

Voucher codes are explicitly excluded from offline storage.

Replay is triggered via POST /api/offline-transactions/replay after connectivity recovery.

Risk: duplicate payments if operator retries before queue reconciliation.

---

## 9. Operator Guardrails

Guardrails prevent operator mistakes and fiscal invalid states.

### Backend hard guards

These are mandatory and must not be bypassed:

- Decommissioned register cannot take payments.
- Decommissioned register cannot create new receipts.
- Startbeleg duplicate is blocked.
- Monatsbeleg duplicate is blocked.
- Jahresbeleg duplicate is blocked.
- Schlussbeleg invalid state is blocked.
- Schlussbeleg requires closed register and no open session.
- Voucher payment without code is blocked.
- Voucher payment cannot bypass TSE signing through client flags.

### Frontend POS guards

POS should block or warn operators before invalid actions:

- Decommissioned register: block payment.
- Startbeleg required: block payment/session start until resolved.
- Monatsbeleg required: show blocking modal/session gate where implemented.
- Offline + Gutschein: block immediately.
- TSE unavailable: block or fail fiscal payment according to policy.

### Soft warnings

Soft warnings reduce human error but do not necessarily block the action:

- Large cash payment confirmation.
- Full Gutschein balance usage confirmation.
- Night/month-end warning for Monatsbeleg/Tagesabschluss context.
- Repeated TSE failure warning.

### Audit logging

Critical POS actions should be auditable:

- Successful payments
- Failed payments
- Blocked payments
- Gutschein usage
- Special receipt creation
- Register readiness checks
- Decommissioned register payment attempts

Rules:

- Do not log voucher codes.
- Do not log secrets or raw credentials.
- Prefer error codes and masked references.

---

## Authentication

### Login with Username or Email

Users can sign in with either:

- **Email address** (e.g. `manager@cafe.regkasse.at`)
- **Username** (e.g. `manager1`, `cashier2`, `user3`)

**Username generation (Quick Create / admin user create):**

- FA **Schnell anlegen** and admin user APIs generate a unique login name when `userName` is omitted.
- Pattern: `{rolePrefix}{incrementalNumber}` (collision retry adds a random suffix).
- Role prefixes: `admin` (SuperAdmin), `manager`, `cashier`, default `user` (includes Accountant and other roles).

**Login API:** `POST /api/Auth/login`

- Preferred body field: **`loginIdentifier`** (email or username).
- Legacy field **`email`** is still accepted with the same value for older clients.
- **`clientApp`:** `pos` (POS) or `admin` (Frontend Admin); required when `AllowLegacyLoginWithoutClientApp` is false.

```json
// Old (still supported):
{ "email": "user@example.com", "password": "...", "clientApp": "pos" }

// New:
{ "loginIdentifier": "manager1", "password": "...", "clientApp": "pos" }
```

```json
{ "loginIdentifier": "user@example.com", "password": "...", "clientApp": "admin" }
```

Backend resolves the user with `IdentityLoginLookup.FindByLoginIdentifierAsync`: `FindByEmailAsync` first, then case-insensitive username match on `NormalizedUserName` (`backend/Helpers/IdentityLoginLookup.cs`, `AuthController`).

**Clients:**

- **Frontend Admin:** `LoginForm` sends `loginIdentifier` (+ legacy `email`) — `frontend-admin/src/features/auth/components/LoginForm.tsx`
- **POS:** `authService.buildLoginPayload` — `frontend/services/api/loginPayload.ts`, `frontend/app/(auth)/login.tsx`

### Username case-insensitive login

Usernames are **case-insensitive** for login and uniqueness:

- `Manager1`, `manager1`, and `MANAGER1` refer to the same user at sign-in.
- Email addresses remain case-insensitive (standard ASP.NET Identity behavior).

**Technical implementation:**

- `UserName` is stored as entered; `NormalizedUserName` is **UPPER** invariant (Identity).
- Login and duplicate checks use `NormalizedUserName` (`IdentityLoginLookup`).
- Creating or renaming a user rejects names that collide case-insensitively (`[UsernameUnique]` on `UpdateUsernameRequest`, `UserUniquenessValidationService`).
- Legacy DB rows: migration `NormalizeUsernamesCaseInsensitive` renames case-insensitive duplicates and backfills `NormalizedUserName`.

**Example:**

- User created with username `Mustafa`
- Can log in as: `mustafa`, `MUSTAFA`, `MuStAfA`
- Cannot create another user with username `mustafa` (or `MUSTAFA`)

Contract supplements: `docs/API_CONTRACTS.md` (Authentication).

---

## 10. API Surface

The authoritative API contract is `backend/swagger.json`.

### API Headers

#### Tenant Identification

- **Production:** Tenant derived from request `Host` subdomain automatically (`{slug}.regkasse.at` → `tenants.slug`).
- **Development:** `X-Tenant-Id: {slug}` header — tenant **slug** (e.g. `cafe`), not the UUID; see `SubdomainTenantProvider.DevTenantHeaderName`.
- **Development:** `?tenant={slug}` query parameter (same slug semantics as the header).

After resolution, the backend sets `ICurrentTenantAccessor.TenantId` (Guid). Authenticated requests may also carry JWT claim `tenant_id` (Guid), applied by `TenantContextMiddleware`.

Clients (POS/admin): in Development on loopback, send `X-Tenant-Id` from `tenantStorage` / dev tenant selector (`frontend/services/tenant/tenantStorage.ts`, `frontend-admin` dev presets).

#### Super Admin Endpoints

- All endpoints under `/api/admin/tenants` require **`SuperAdmin`** role (`AdminTenantsController`).
- These endpoints manage the global **`tenants`** table (not `ITenantEntity`-scoped); they are not limited by per-tenant row filters on business tables.
- To access a specific tenant’s operational data (products, receipts, payments, etc.), use **`POST /api/admin/tenants/{tenantId}/impersonate`** and call APIs with the returned tenant-scoped JWT.

Key endpoint groups:

### POS payments

- `/api/pos/payment`
- `/api/pos/payment/methods`
- `/api/pos/payment/{id}`

Legacy `/api/Payment` may exist, but prefer `/api/pos/*` for POS behavior.

### Receipts

- Receipt listing/detail endpoints are exposed through `ReceiptsController`.
- Receipt details should include fiscal data where supported.

### RKSV special receipts

- `POST /api/rksv/special-receipts/nullbeleg`
- `POST /api/rksv/special-receipts/startbeleg`
- `POST /api/rksv/special-receipts/monatsbeleg`
- `POST /api/rksv/special-receipts/jahresbeleg`
- `POST /api/rksv/special-receipts/schlussbeleg`

### Vouchers

- Admin voucher APIs under `/api/admin/vouchers`
- POS voucher validation under `/api/pos/vouchers/validate`

### Offline replay

- `/api/offline-transactions/replay`

### Fiscal export / RKSV admin

- `/api/admin/fiscal-export`
- FinanzOnline/outbox/reconciliation endpoints exist; check swagger and current controllers.

### API contract rule

When changing backend endpoints or DTOs:

1. Regenerate `backend/swagger.json`.
2. Regenerate frontend-admin Orval client.
3. Commit generated client changes with swagger changes.
4. Run `node scripts/verify-api-client.mjs` from repository root.

---

## 11. Database Overview

Core tables / concepts include:

- `payment_details`
- `invoices`
- `receipts`
- `receipt_items`
- `receipt_tax_lines`
- `receipt_sequences`
- `signature_chain_state`
- `cash_registers`
- `cash_register_transactions`
- `daily_closings`
- `tse_devices`
- `tse_signatures`
- `offline_transactions`
- `vouchers`
- `voucher_ledger_entries`
- `finanz_online_submissions`
- `finanz_online_outbox_messages`
- `rksv_special_receipt_finanz_online_submissions`
- auth/session/refresh-token tables

Important constraints:

- Receipt sequence must remain consistent.
- Signature chain state is per cash register.
- RKSV special receipts have uniqueness rules, such as one Startbeleg per register, one Monatsbeleg per register/month, one Jahresbeleg per register/year, one Schlussbeleg per register.
- Voucher code hashes should be unique per tenant.
- Voucher ledger should preserve auditability.

Migration rule:

Never casually rewrite fiscal migrations. For fiscal/RKSV changes, prefer additive nullable fields and carefully tested migrations.

---

## 12. Testing Strategy

Important backend test areas:

- RKSV special receipts
- Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg service tests
- Voucher redemption tests
- Payment/TSE signature integration tests
- Signature chain tests
- Offline replay integration tests
- FinanzOnline outbox tests
- Cash register resolution / readiness tests
- Role/permission matrix tests

Important frontend/admin checks:

- `npm run typecheck`
- translation validation
- menu/route coverage tests where present
- POS guardrail tests where present

Useful commands:

```bash
cd backend
dotnet build KasseAPI_Final.csproj -c Debug
```

```bash
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~RksvStartbelegServiceTests|FullyQualifiedName~RksvMonatsbelegServiceTests|FullyQualifiedName~RksvJahresbelegServiceTests|FullyQualifiedName~RksvSchlussbelegServiceTests|FullyQualifiedName~RksvNullbelegServiceTests"
```

```bash
cd frontend-admin
npm run typecheck
node ../localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
```

```bash
node scripts/verify-api-client.mjs
```

---

## 13. Internationalization / UI Language Rules

### POS

- POS UI should be German for operator-facing fiscal/payment messages.
- Avoid Turkish or English fallback labels in production POS UI.
- Existing German labels such as `Gutschein`, `Startbeleg`, `Monatsbeleg`, `Jahresbeleg`, `Endbeleg`, `Fiskalisierung abgeschlossen` should be preserved or translated through i18n.

### Frontend Admin

- Admin supports multiple languages, including German, English, and Turkish.
- Visible labels must use i18n.
- Permission codes and backend constants must not be translated.

### Backend

- Backend logs and technical errors may be English.
- User-facing frontend messages should be localized.

---

## 14. Coding Standards and AI Development Rules

### General

- Keep changes minimal and scoped.
- Do not refactor unrelated modules.
- Do not alter fiscal flows unless the task explicitly requires it.
- Prefer additive changes over destructive changes.
- Preserve existing architecture and naming patterns.

### Backend

- Use service-layer validations for fiscal hard rules.
- Do not trust client flags for fiscal signing decisions.
- Use domain-specific error codes for guardrail failures.
- Keep payment/receipt/signature operations atomic.
- Do not create fiscal records outside intended transaction boundaries.
- Do not log secrets.
- Do not log voucher codes.
- Use EF migrations carefully.

### Frontend POS

- Keep operator UX simple and German.
- Block illegal actions early in UI, but always rely on backend as final authority.
- Do not store voucher secrets locally.
- Do not queue voucher payments offline.
- Avoid broad layout or navigation refactors during fiscal work.

### Frontend Admin

- Use generated API clients where appropriate.
- Keep route permissions consistent.
- Use Ant Design patterns already present in the codebase.
- Update i18n keys in all supported languages.
- Do not expose raw voucher codes.

### API contract

Whenever backend API changes:

1. Update swagger.
2. Regenerate Orval client.
3. Run API client verification.
4. Commit generated files together.

---

## 15. AI Guardrails

When using ChatGPT/Cursor on this project:

### Always do

- Ask for or inspect current code before modifying fiscal logic.
- Prefer small, independent tasks.
- Use separate chat contexts for separate features.
- For RKSV/fiscal work, first request analysis, then implementation.
- List modified files after changes.
- Run targeted tests.
- Explain risk and acceptance criteria.

### Never do

- Do not claim legal compliance guarantees.
- Do not invent BMF/FinanzOnline behavior.
- Do not store or log raw voucher codes.
- Do not bypass TSE signing with client flags.
- Do not loosen `CreatePaymentRequest` validation to allow arbitrary zero-amount normal payments.
- Do not implement Schlussbeleg as a normal daily closing.
- Do not allow payments on Decommissioned registers.
- Do not change receipt number sequence casually.
- Do not regenerate huge clients unless API changed.
- Do not refactor unrelated code while fixing fiscal bugs.

### Recommended Cursor workflow

For large changes:

1. New chat context.
2. Start with: `Context reset. Focus only on ...`
3. Ask for analysis first.
4. Ask for minimal implementation second.
5. Ask for final audit third.
6. Run tests.
7. Commit.

---

## 16. Deployment Requirements

### DNS Configuration

- **Wildcard A record:** `*.regkasse.at` → backend (and admin/POS frontends if served on tenant subdomains) server IP address.
- Required so each tenant host (`cafe.regkasse.at`, `bar.regkasse.at`, …) and `admin.regkasse.at` resolve to the application stack.
- Reverse proxy / load balancer must **preserve the original `Host` header** for tenant slug extraction (`TenantHostNames.GetTenantSlugFromHost`).
- **SSL/TLS:** Certificate must cover the wildcard (`*.regkasse.at`) and typically the apex (`regkasse.at`) if used.

Local development alternative: hosts-file entries such as `cafe.regkasse.local` (see `TenantHostNames.IsLocalDevelopmentDomain`).

### Environment Variables

| Variable | Role |
|----------|------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET host environment; drives `IWebHostEnvironment.IsDevelopment()` |

**Tenant resolution mode (backend):**

- **`Development`:** `X-Tenant-Id` header and `?tenant=` query overrides are allowed (`SubdomainTenantProvider`).
- **`Production`** (and other non-Development values such as `Staging`): **only** subdomain / `Host`-based resolution; header and query overrides are ignored.

Ensure production deployments set `ASPNETCORE_ENVIRONMENT=Production` (not `Development`) so tenant cannot be spoofed via headers.

### License service startup behavior

On process start (`LicenseComplianceHostedService` → `EvaluateOnStartup()`):

1. Opens an **`IServiceScopeFactory`** scope and reads `activated_licenses` for this host’s machine fingerprint.
2. Merges result with encrypted on-disk license state and builds the in-memory snapshot.
3. If the database read fails (e.g. scoped DI misuse or DB down), logs a warning and continues with **trial / file state**.
4. Does **not** block application startup or HTTP listener binding.

### Related client build vars (not ASP.NET)

- Admin: `NEXT_PUBLIC_API_BASE_URL` should target the correct tenant or shared API host per deployment layout — see `frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`.
- POS: configure API base URL per tenant build or runtime bootstrap (`EXPO_PUBLIC_*` / license bootstrap) so requests hit the matching subdomain in production.

---

## 16.1 Troubleshooting (common errors)

### `Cannot resolve scoped service from root provider`

**Error:**

```text
System.InvalidOperationException: Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider.
```

**Cause:** A **singleton** resolved `IDbContextFactory<AppDbContext>` (or `AppDbContext`) from the **root** `IServiceProvider` while `AppDbContext` requires scoped `ICurrentTenantAccessor`.

**Fix:** Use `IServiceScopeFactory`:

```csharp
using var scope = _scopeFactory.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync(cancellationToken);
```

**Reference:** `LicenseService.cs`. Same pattern applies to other singletons that touch EF (e.g. audit hosted services already use `IServiceScopeFactory`).

### `Multiple constructors accepting all given argument types` (`AppDbContext`)

**Cause:** Two public runtime constructors both satisfiable by DI.

**Fix:** Keep a single `[ActivatorUtilitiesConstructor]` runtime ctor (`options` + `ICurrentTenantAccessor`) and a separate design-time ctor (`options` only) for `dotnet ef` — see `AppDbContext.cs`, `DesignTimeDbContextFactory.cs`.

---

## 17. Known Risks / Limitations

Known or suspected risks:

- Real BMF / FinanzOnline webservice integration requires official credentials and documentation.
- Manual BMF Belegcheck workflow remains important.
- Voucher refund / partial refund balance restoration may need further implementation.
- Fiscal export is NOT a legal RKSV proof. Payloads include explicit disclaimer text; when enabled, API calls may also require **`X-Disclaimer-Acknowledged: true`**. Exports are for diagnostics and internal analysis only.
- The codebase references Epson-style TSE devices and generic providers (e.g., Epson-TSE, fiskaly) through model comments and simulation logic. Runtime signing is provider-based (Fake vs Real) and not tied to a single OEM SDK. Supported vendors are configuration-driven, not defined as a fixed compatibility list.
- This repository does not define a production deployment architecture. CI uses GitHub Actions with PostgreSQL containers for testing, but production hosting (cloud, on-prem, containers, etc.) is deployment-specific.
- Canonical roles are defined in backend/Authorization/Roles.cs: SuperAdmin → full access; Manager → admin + RKSV + voucher + reporting; Cashier → POS + payment + TSE signing; Waiter → limited POS usage; Accountant → reporting + FinanzOnline + RKSV (no Schlussbeleg); Kitchen / ReportViewer → specialized roles. Always treat RolePermissionMatrix as the source of truth.
- The system does NOT enforce sales blocking after early Jahresbeleg. This is explicitly treated as a legal/process responsibility, not a hard backend restriction. Duplicate Jahresbeleg creation is prevented, but continued operation is not blocked in code.

---

## 18. Current High-Value Next Tasks

Possible next tasks, in priority order:

1. Keep RKSV documentation synchronized with implemented special receipts.
2. Add or refine manual BMF Belegcheck workflow docs.
3. Strengthen POS offline/fail-safe behavior.
4. Add audit-friendly admin shortcuts for critical POS action logs.
5. Implement voucher refund ledger restoration if business requires refunds.
6. Add verification URL / QR visibility improvements if required by receipt UX.
7. Perform real audit simulation using RKSV audit checklist.
8. Only later: evaluate real FinanzOnline/BMF webservice integration.

---

## 19. Quick Prompt for New AI Sessions

Use this at the top of a new AI/Cursor chat:

```text
You are working on Regkasse, a full-stack Austrian RKSV-compliant POS cash register system.

Stack:
- Backend: ASP.NET Core / C# / EF Core / PostgreSQL
- POS frontend: Expo React Native / TypeScript / Zustand / i18n
- Admin frontend: Next.js 14 / Ant Design / TanStack Query / Orval generated API client

Critical domain rules:
- Fiscal payments require TSE/RKSV signing and signature chaining.
- Receipt numbers must stay sequential and consistent.
- Decommissioned cash registers must not accept sessions or payments.
- RKSV special receipts exist: Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg.
- Gutschein/voucher codes must never be stored or logged in plaintext.
- Offline voucher payments are forbidden.
- Backend is final authority; frontend guardrails are UX only.
- Do not claim legal compliance guarantees.

Development rules:
- Keep changes minimal and scoped.
- Do not refactor unrelated code.
- Do analysis before implementation for fiscal changes.
- Update swagger + Orval client if backend API changes.
- Run targeted tests.
```

---

## 20. Definition of Done for Fiscal/RKSV Changes

A fiscal/RKSV change is not done until:

- Backend guardrails are in place.
- Frontend guardrails are consistent where applicable.
- Receipt/payment/signature behavior remains atomic.
- Voucher secrets are not logged or persisted.
- Tests pass for affected flows.
- Swagger and generated client are synchronized if API changed.
- Admin/POS i18n is updated if UI changed.
- Documentation is updated if behavior changed.
- Known legal uncertainty is documented rather than hidden.

