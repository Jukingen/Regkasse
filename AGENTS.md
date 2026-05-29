# AGENTS.md - Regkasse POS System Development Rules

## Purpose
This repository is a POS monorepo. Prefer safe, incremental improvements over broad rewrites. Follow real package boundaries, preserve current architecture unless explicitly asked otherwise, and make the smallest safe change that satisfies the task.

## Cursor / AI Agent Integration
- Cursor loads this file as an **always-applied workspace rule** — agents see it on every task.
- For medium or large tasks, also read **`REGKASSE_AI_ONBOARDING.md`** and relevant docs under `ai/`.
- `.cursorrules` is a supplementary legacy rules file; prefer **AGENTS.md** when they conflict on agent workflow.
- Keep this file valid Markdown (closed code fences, proper headings); broken formatting reduces what agents can parse reliably.

## Language Rules
Follow these language rules strictly:

| Context | Language | Example |
|---------|----------|---------|
| Code identifiers | English | `GetUserByIdAsync` |
| Code comments | English/Turkish | `// User validation` or `// Kullanıcı doğrulama` |
| POS UI texts | German (de-DE) | "Benutzername", "Passwort zurücksetzen" |
| Admin UI texts | i18n (de/en/tr) | Use locale files under `frontend-admin/src/i18n/` |
| Backend logs | English | "User created successfully" |
| API error messages | English | "Invalid login credentials" |
| Database schema | English | users, tenants, payment_details |
| Git commits | English | "feat: add username login support" |
| **Explanations in IDE** | **Turkish** | When explaining plans, changes, or reviews |

**Do not** translate POS UI text into English or Turkish.

## Working Style
- Prefer minimal, targeted changes over broad refactors.
- Preserve existing architecture and naming conventions unless explicitly asked for restructuring.
- Before editing, inspect nearby files and follow local patterns.
- Do not invent commands, package relationships, or framework conventions.
- State uncertainty explicitly when repo evidence is missing.
- Do not mix unrelated refactors into feature work.
- Prefer controlled evolution, small reversible steps, and behavior-preserving refactors.
- Prefer updating existing code paths over introducing parallel implementations.

## Source of Truth
When deciding how to work, trust these in order:

1. Actual implementation in the nearest relevant files
2. Package-level config files
3. Root config files
4. CI workflows
5. README, `docs/`, and **`REGKASSE_AI_ONBOARDING.md`**
6. `ai/` guidance docs for domain-specific safety

If they conflict, follow the most local and executable truth.

## Read Before Changing Code
Before making changes:

1. For project-wide fiscal/RKSV/POS context, read **`REGKASSE_AI_ONBOARDING.md`**; then read relevant docs under `/ai`
2. Respect compliance and fiscal/TSE/RKSV rules
3. Follow existing repo patterns
4. Preserve backward compatibility unless explicitly told otherwise

For medium or large changes, always provide:
- a short implementation plan
- affected files
- main risks
- backward compatibility impact
- a test strategy

## Repo Map
- `backend/` - ASP.NET Core 10 API (auth, domain logic, fiscal/TSE/RKSV, reporting, OpenAPI)
- `frontend/` - Mobile POS (Expo Router + React Native + TypeScript)
- `frontend-admin/` - Admin panel (Next.js 14 + Ant Design + TanStack Query)
- `localization/` - Shared i18n import/export/validation tooling
- `scripts/` - Cross-repo validation and consistency scripts
- `tools/` - License generator and i18n utility tools
- `testsprite/` - API/E2E test definitions and TestSprite CI integration
- `.github/workflows/` - CI source of executable truth
- `docs/` - Human documentation and reference material
- `ai/` - Internal implementation and guardrail docs

## Multi-Tenant Architecture (CRITICAL)

### Tenant Identification
- **Production:** Subdomain only (`{slug}.regkasse.at`)
- **Development:** `X-Tenant-Id` header or `?tenant={slug}` query
- JWT contains `tenant_id` claim after authentication
- Super Admin: `admin.regkasse.at`

### Tenant Isolation
- Tenant-scoped tables MUST have non-null `tenant_id` on entities implementing `ITenantEntity`
- EF Core global query filters in `AppDbContext` automatically filter by `ICurrentTenantAccessor.TenantId`
- Tenants can NEVER see other tenants' data
- Cross-tenant access MUST return HTTP 404 (NOT 403)
- NEVER use `IgnoreQueryFilters()` except for Super Admin operations

### Super Admin
- Role: `SuperAdmin` (system.critical permission)
- Access via `admin.regkasse.at`
- Can impersonate any tenant via `POST /api/admin/tenants/{id}/impersonate`
- Tenant CRUD via `/api/admin/tenants/*`
- Cross-tenant SaaS metrics API is not yet a separate surface

### Impersonation Flow
1. Super Admin clicks "Login as" on `/admin/tenants` (FA)
2. Backend issues JWT with target `tenant_id` claim and `tenant_impersonation=true`
3. Production target: redirect to `https://{slug}.regkasse.at` with token handoff
4. Subsequent API calls use tenant-scoped JWT; EF filters apply to target tenant
5. Structured server logs record actor + tenant

### Tenant Resolution in Background Services
- Startup and singleton hosted services have **no HTTP request**; `ICurrentTenantAccessor.TenantId` may be null
- Singletons that need EF must use **`IServiceScopeFactory.CreateScope()`** before `IDbContextFactory<AppDbContext>`
- `AppDbContext` + `ICurrentTenantAccessor` are **scoped** - do not create DbContext from root provider inside a singleton

```csharp
using var scope = _scopeFactory.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync(ct);
```

### Testing Multi-Tenant Locally

```bash
# Header (Development only)
curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health

# Query parameter
curl http://localhost:5184/api/health?tenant=test_cafe

# Hosts file simulation
127.0.0.1 cafe.regkasse.local
# Then: http://cafe.regkasse.local:5184
```

## API Boundaries (DO NOT CROSS)

| Client | Allowed Prefix | Forbidden |
|--------|---------------|-----------|
| POS (frontend) | `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` | `/api/admin/*` |
| Admin (frontend-admin) | `/api/admin/*`, `/api/Auth/*` | `/api/pos/*` |
| Shared | `/api/Auth/*`, `/api/user/*`, `/api/rksv/*` | - |

### Legacy Endpoints (DO NOT EXTEND)
- `/api/Payment`, `/api/Cart`, `/api/Product` - existing only, no new features
- New features MUST use canonical boundaries

## Authentication & User Management

### Login Methods
- **Email:** `user@example.com`
- **Username:** `manager1`, `cashier2` (case-insensitive)
- Login endpoint: `POST /api/Auth/login` with `loginIdentifier` field

```typescript
// Use this:
{ "loginIdentifier": "manager1", "password": "..." }
// loginIdentifier can be email OR username (case-insensitive)
```

### Username Rules
- Min 3, max 50 characters
- Allowed: `a-z`, `0-9`, `_`, `-`
- Case-insensitive (`Mustafa = mustafa = MUSTAFA`)
- Auto-generated on Quick Create: `{rolePrefix}{nextNumber}`

### Password Rules
- Min 8 characters
- Generated on user creation (displayed once, never stored in plain text)
- Force change on first login when configured

### Username Change Requirements
- MUST log `AuditEventType.UserNameChanged`
- MUST store both `old_username` and `new_username` in audit
- MUST require reason field (optional but recommended)
- MUST invalidate all active sessions for the user
- MUST check username uniqueness (case-insensitive)

### User Permissions
- `users.view` - View user list
- `users.manage` - Create/edit/delete users
- `settings.view` - View system settings
- `settings.manage` - Modify system settings
- `reports.view` - View reports
- `reports.export` - Export reports

## Audit & Compliance

### Mandatory Audit Fields
Every fiscal or security-sensitive operation MUST log:
- `actor_user_id` - Who performed the action
- `actor_role` - Their role (`SuperAdmin`, `Manager`, `Cashier`)
- `tenant_id` - Which tenant (`NULL` only for system operations)
- `action_type` - From `AuditEventType` enum (see `backend/Models/AuditEventType.cs`)
- `old_values` / `new_values` - For sensitive changes
- `correlation_id` - For tracking related events
- `ip_address` - Client IP
- `timestamp_utc` - UTC timestamp

### Audit Event Types (`AuditEventType`)
Security and lifecycle audit log — source of truth: `backend/Models/AuditEventType.cs`.

Key values include: `UserCreated`, `UserUpdated`, `UserRoleChanged`, `UserDeactivated`, `UserReactivated`, `UserDeleted`, `UserNameChanged`, `PasswordResetForced`, `UserTenantMembershipChanged`, `RestoreRequested`, `RestoreApproved`, `RestoreRejected`, `RestoreCompleted`, `RestoreFailed`.

Do **not** confuse with `ActivityEventType` (admin activity feed / notifications).

### Activity Feed Event Types (`ActivityEventType`)
Admin bell / SSE / email / webhook — source of truth: `backend/Models/ActivityEventType.cs`.

Includes: `UserCreated`, `UserUpdated`, `UserDeleted`, `CashRegisterOpened`, `CashRegisterClosed`, `CashRegisterDecommissioned`, `LicenseExpiringSoon`, `LicenseExpired`, `OfflineQueueGrowing`, `FinanzOnlineSubmissionFailed`, `BackupFailed`, `BackupSucceeded`, `RestoreDrillFailed`, `RestoreDrillSucceeded`.

### Data Retention
- Audit logs: minimum 7 years
- Payment data: minimum 7 years

## Fiscal Rules (RKSV/TSE)

### RKSV Special Receipts
- `Nullbeleg`, `Startbeleg`, `Monatsbeleg`, `Jahresbeleg`, `Schlussbeleg` MUST each include TSE signature
- `Monatsbeleg` MUST be created within 7 days of month end
- `Jahresbeleg` MUST be created by January 31st

### TSE Requirements
- Every fiscal receipt MUST have TSE signature
- Signature chain MUST be continuous (no gaps)
- TSE health status MUST be monitored continuously

### TSE Offline Mode
- Max **50** offline transactions per cash register (`TseOptions.MaxOfflineTransactionsPerCashRegister`)
- When limit is reached, POS must block new offline transactions
- Show warning at 40 transactions (80% capacity)
- Offline queue must persist across app restarts

### NTP Time Sync
- Check NTP time sync status before treating online fiscal payments as allowed
- Block when sync failed or offset exceeds configured `MaxAllowedOffsetSeconds`

### Voucher (Gutschein) Rules
- Never queue voucher payments for offline non-fiscal replay
- Backend must reject; POS must not enqueue voucher payloads

### Storno Flows
- Must supply `OriginalReceiptId` and a `StornoReason` where contract requires them
- Do not conflate with partial refund

## Backup & Restore Rules

### Backup Policy
- Daily automated backup is mandatory per tenant
- Manual backup can be triggered by authorized roles only
- Backup metadata MUST include `tenant_id`, `triggered_by`, `started_at_utc`, `finished_at_utc`, `status`

### Backup Types
| Type | Scope | Who Can Trigger |
|------|-------|-----------------|
| Tenant Backup | Single tenant | Tenant Admin, Super Admin |
| Full Backup | All tenants | Super Admin only |
| Scheduled Backup | Per configuration | Automated |

### Restore Policy (HIGH RISK)
- NEVER allow automatic restore to production
- Restore requires two SuperAdmin approvals
- Restore target must be isolated/test database only
- Every restore request/approval/execution step MUST be written to full audit trail
- Cross-tenant restore is forbidden unless explicit Super Admin recovery workflow

### Backup Status (`BackupRunStatus`)
`Queued`, `Running`, `AwaitingVerification`, `Succeeded`, `Failed`, `VerificationFailed`, `Cancelled`

### Backup Configuration
```yaml
enabled: true
scheduleCron: "0 2 * * *"  # Daily at 2 AM
retentionDays: 30
executionMode: "PgDump"  # Fake, PgDump, ProductionStub
externalArchiveRoot: "/backup/archive"  # Super Admin only
```

## Activity Feed & Notifications

### Event Types to Track
- User actions (create, update, delete, username change)
- Cash register actions (open, close, decommission)
- Backup events (start, success, failure)
- License events (expiring, expired)
- FinanzOnline events (submission success, submission failure)

### Notification Rules
- Critical failures (TSE/RKSV, backup, restore, auth anomalies) MUST generate notifications
- Use Server-Sent Events (SSE) for in-app notifications
- Optional email channel for critical events

## Security Rules

### Input Validation
- ALL user inputs MUST be validated on backend
- Client-side validation is UX-only and is NOT a security boundary
- Username regex: `^[a-zA-Z0-9_-]{3,50}$`
- Email format: RFC 5322 compliant

### Session Management
- Use JWT with 24h expiry
- Enforce refresh token rotation
- Auto-logout after 30 minutes inactivity
- Username change MUST invalidate all active sessions:
  1. Update `SecurityStamp`
  2. Invalidate all existing JWTs
  3. Force re-login on all devices

### Sensitive Data
- NEVER log passwords (including hashed form)
- NEVER log voucher codes
- Mask payment card numbers as `**** **** **** 1234`
- Encrypt secrets in `appsettings.Production.json`

### Error Handling
User-facing errors (German):
- `Benutzername oder Passwort ist falsch`
- `Sie haben keine Berechtigung für diese Aktion`
- `Das Backup ist fehlgeschlagen. Bitte kontaktieren Sie den Support.`

Technical logs (English):
- `User login failed: invalid credentials`
- `Permission denied for user {id} on resource {resource}`

## Directory Hints
Before editing in each area, inspect these first:
- `backend/` - controllers, services, DTOs, EF entities, migrations, tenancy files, impacted tests
- `frontend/` - screens, hooks, contexts, navigation, API usage, impacted tests
- `frontend-admin/` - routes, feature components, hooks, queries, auth gates, impacted tests
- `localization/` - validation scripts, catalog ownership, CI budget checks

## AI Docs Routing Hints
Use `/ai` docs selectively based on the task:
- Backend/API/auth/contract work → `ai/01_BACKEND_CONTRACT.md`, `ai/03_API_CONTRACT.md`
- Database/entity/migration work → `ai/02_DATABASE_CONTRACT.md`
- Compliance/fiscal/TSE/RKSV work → `ai/05_SECURITY_COMPLIANCE.md`, `ai/modules/tse_finanzonline.md`
- Admin API integration work → `ai/10_API_BOUNDARY_POLICY.md`
- High-risk areas → `ai/07_DO_NOT_TOUCH.md`

## Code Quality Rules

### Required for Every PR
- Unit tests pass
- Integration tests pass (if applicable)
- No `console.log` in production code
- No TypeScript `any` type (use `unknown` instead)
- No hardcoded user-facing strings (use i18n)
- Audit events exist for sensitive operations

## High-Risk Flows (DO NOT TOUCH WITHOUT CAREFUL VALIDATION)
- Cart → Payment → Receipt → DailyClosing
- Pricing and modifier behavior
- Table cart switching and recovery
- Inventory / payment / order synchronization
- TSE / RKSV signing and auditability
- Auth / RBAC behavior
- Payment processing (money, rounding, idempotency)
- TSE signature chain (fiscal compliance)
- RKSV special receipts
- Voucher ledger balance integrity
- FinanzOnline outbox SOAP submission flow
- Tenant isolation (query filters, 404 semantics)
- Singleton + EF pattern (always use `IServiceScopeFactory`)

## Do NOT
- Do not introduce a parallel architecture or broad rewrite
- Do not casually change API contracts, auth behavior, role names, or payment flows
- Do not mix unrelated refactors into feature work
- Do not rename or reshape public APIs, DTOs, config keys, or role semantics without checking downstream consumers
- Do not weaken validation, auditability, authorization, or fiscal guarantees
- Do not commit secrets
- Do not delete columns directly; mark as `is_deleted` or deprecated
- Do not modify existing migration files after they are committed

## Validation Commands
Run from repository root when relevant:

```bash
node scripts/verify-api-client.mjs
node scripts/validate-critical-openapi-paths.mjs
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
node localization/scripts/check-translation-boundary.mjs --app frontend-admin
node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json

# Backend
dotnet test

# Frontend Admin
cd frontend-admin && npm run test

# Frontend POS
cd frontend && npm run test
```

## Development Workflow

### Local Setup
1. Start PostgreSQL (Docker or local)
2. Run backend: `cd backend && dotnet run`
3. Run FA: `cd frontend-admin && npm run dev`
4. Run FE: `cd frontend && npm start`

### Environment Variables

Backend:
```env
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Database=kasse_db;Username=postgres;Password=***
JwtSettings__SecretKey=*** (min 32 chars)
```

Frontend Admin:
```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
```

Frontend POS:
```env
EXPO_PUBLIC_API_BASE_URL=http://192.168.1.100:5184/api
EXPO_PUBLIC_DEV_TENANT_ID=test_cafe
```