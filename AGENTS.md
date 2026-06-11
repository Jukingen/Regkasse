# AGENTS.md - Regkasse POS System Development Rules

## Purpose
This repository is a POS monorepo. Prefer safe, incremental improvements over broad rewrites. Follow real package boundaries, preserve current architecture unless explicitly asked otherwise, and make the smallest safe change that satisfies the task.

## Cursor / AI Agent Integration
- Cursor loads **`AGENTS.md`** as an **always-applied workspace rule** — agents see it on every task.
- **`.cursorrules`** is a thin redirect stub only; do not duplicate rules there.
- For medium or large tasks, also read **`REGKASSE_AI_ONBOARDING.md`** and relevant docs under `ai/`.
- Keep this file valid Markdown (closed code fences, proper headings); broken formatting reduces what agents can parse reliably.

**Last updated:** 2026-06-11

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

### Updated Stack Versions

| Component | Version |
|-----------|---------|
| Backend (.NET) | 10.0.8 |
| EF Core | 10.0.8 |
| Next.js | 16.2.6 |
| React | 19.2.6 |
| Ant Design | 6.4.3 |
| Expo | SDK 56 |
| React Native | 0.85.3 |

- Admin auth boundary: `frontend-admin/src/proxy.ts` (Next.js 16; replaces deprecated `middleware.ts`).
- Ant Design 6: prefer `destroyOnHidden` (Modal/Drawer/Tabs), `popupRender` (Dropdown/Select), `titlePlacement` (Divider); `Card bordered={false}` → `variant="borderless"`; `Tag bordered={false}` → `variant="filled"`. **Never** use static `message` / `notification` / `Modal.confirm` from `antd` imports — use `useAntdApp()` (see Frontend-Admin conventions).

### Packages
- `backend/` - ASP.NET Core 10 API (auth, domain logic, fiscal/TSE/RKSV, reporting, OpenAPI)
- `frontend/` - Mobile POS (Expo SDK 56 + React Native + TypeScript)
- `frontend-admin/` - Admin panel (Next.js 16 + Ant Design 6 + TanStack Query)
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

// Deprecated (do not use in new code):
{ "email": "user@example.com", "password": "..." }
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
- User data: keep until account deletion; preserve when legal hold applies

## Fiscal Rules (RKSV/TSE)

### RKSV Special Receipts
- `Nullbeleg`, `Startbeleg`, `Monatsbeleg`, `Jahresbeleg`, `Schlussbeleg` MUST each include TSE signature
- `Monatsbeleg` MUST be created within 7 days of month end
- `Jahresbeleg` MUST be created by January 31st of the following year
- Show admin warning 14 days before RKSV deadline; email alert 7 days before deadline

### FinanzOnline Rules
- Production mode MUST use real SOAP submission
- Test mode MUST use simulation (`FinanzOnline:Mode=Simulation`)
- Failed submissions MUST retry with exponential backoff (maximum 5 attempts)
- Submission status MUST be visible in admin panel

### Signature Chain Validation
- Receipt numbers MUST be sequential per register
- Duplicate receipt numbers are forbidden
- Any detected gap MUST be logged and trigger an alert

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

### DEP §7 Export (Datenerfassungsprotokoll / Signaturjournal)

**Status:** ✅ Implemented (F1–F5 complete)

**Features:**
- BMF-compliant JSON export with `Belege-Gruppe` structure
- Certificate grouping by thumbprint
- Includes all receipt types (normal, special, daily closings)
- Compact JWS signatures (not QR payload)
- RKSV §9 `BelegdatenPayload` machine code (`_R1-AT1_…`) for Prüftool receipt verification
- Chronological ordering

**Purpose:** Betriebsprüfung (tax audit) compliant export of signed receipt data per cash register and period.

**Technical rules:**

1. **Format:** BMF JSON schema with `Belege-Gruppe` structure (`RksvDepExportRootDto`; property names via `[JsonPropertyName]` — e.g. `Belege-Gruppe`, `Belege-kompakt`)
2. **Signature format:** Compact JWS (`header.payload.signature`), **not** QR payload
3. **Grouping:** All receipts signed with the same certificate thumbprint in one `Belege-Gruppe`
4. **Order:** Chronological by issue date/time (`IssuedAt`), then `SequenceNumber` (BelegNr seq or closing date)
5. **Included items:**
   - Normal payment receipts (`payment_details`, `RksvSpecialReceiptKind` null)
   - Special receipts: `Startbeleg`, `Monatsbeleg`, `Jahresbeleg`, `Schlussbeleg`, `Nullbeleg` (when `includeSpecialReceipts=true`)
   - Daily closings (`DailyClosings.TseSignature`, when `includeDailyClosings=true`; filter by `ClosingDate`)
6. **Certificate chain:** Leaf signing cert + issuer CA chain (`Zertifizierungsstellen`), DER Base64 per group
7. **Excluded:** Rows without TSE signature or invalid compact JWS (not exactly three segments)
8. **Thumbprint storage:** `payment_details.certificate_thumbprint`, `DailyClosings.certificate_thumbprint` (stamped at sign time; legacy rows fall back to active TSE cert)

**Implementation status:**

| Phase | Scope | Status |
|-------|--------|--------|
| F1 | Controller + service + DTO skeleton | Done |
| F2 | Certificate grouping + CA chain | Done |
| F3 | Special receipts + daily closings | Done |
| F4 | Prüftool test script (`scripts/verify-rksv-dep-export.ps1`) | Done |
| F5 | Full RKSV §9 `BelegdatenPayload` Prüftool compliance | Done |

**Key files:** `AdminRksvDepExportController`, `RksvDepExportService`, `RksvDepExportDtos`, `ITseKeyProvider`, `TseCertificateChainBuilder`, `BelegdatenPayload`, `BelegdatenPayloadBuilder`, `RksvMachineCodeBuilder`, `SignaturePipeline`, `TseService`

**API:** `GET /api/admin/rksv/dep-export` (Admin only)

| Parameter | Required | Default | Notes |
|-----------|----------|---------|-------|
| `cashRegisterId` | Yes | — | Target register UUID |
| `fromUtc` | Yes | — | Period start (UTC) |
| `toUtc` | Yes | — | Period end (UTC); max 366 days |
| `includeSpecialReceipts` | No | `true` | Sonderbelege |
| `includeDailyClosings` | No | `true` | Tagesabschluss signatures |

**Permission required:** `ReportExport` + `AuditView` (`AppPermissions.ReportExport` / `report.export`, `AppPermissions.AuditView` / `audit.view`).

**Auth:** JWT + tenant context. Cross-tenant / missing tenant → HTTP 404. All DEP exports MUST be logged in audit log (`RksvDepExportJson`).

**Testing:**

```bash
cd backend && dotnet test --filter "RksvDepExportServiceTests"
```

```powershell
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"
```

Requires JDK 17+ on PATH; uses `backend/Tests/regkassen-verification-depformat-1.1.1.jar`.

**Developer guide:** `docs/DEP_EXPORT_DEVELOPMENT.md`

## Backup & Restore Rules

### Backup Policy
- Daily automated backup is mandatory per tenant
- Manual backup can be triggered by authorized roles only
- Backup metadata MUST include `tenant_id`, `triggered_by`, `started_at_utc`, `finished_at_utc`, `status`
- Sensitive data in backup manifests/logs MUST be masked

### Backup Permissions
| Action | Tenant Admin | Super Admin |
|--------|--------------|-------------|
| View own tenant backups | Yes | Yes |
| View all tenant backups | No | Yes |
| Trigger tenant backup | Yes | Yes |
| Trigger full system backup | No | Yes |
| Modify backup schedule | Yes (own tenant) | Yes (global) |
| Delete backup | No | Yes |
| Request restore | No | Yes (requires second approval) |
| Approve restore | No | Yes (second Super Admin) |

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
- Restore operations MUST use correlation IDs for end-to-end tracing
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

### Activity Feed
- Important business and security events MUST be written to activity feed
- Feed entries MUST be tenant-scoped and permission-filtered
- Feed payloads must avoid raw secrets and sensitive payment data

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
- Optional outbound webhooks (Slack/Discord/Teams) when configured

### Permissions UI (Admin)
- Show/hide frontend elements based on `hasPermission()`
- Redirect unauthorized users to 403 page
- Denied access attempts MUST be written to audit log

## Security Rules

### Input Validation
- ALL user inputs MUST be validated on backend
- Client-side validation is UX-only and is NOT a security boundary
- Username regex: `^[a-zA-Z0-9_-]{3,50}$`
- Email format: RFC 5322 compliant
- Tax number regex: `^ATU\d{8}$`

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
- `Die TSE-Signatur konnte nicht erstellt werden`

Technical logs (English):
- `User login failed: invalid credentials`
- `Permission denied for user {id} on resource {resource}`
- `Backup failed: pg_dump exited with code {code}`
- `TSE signature creation timeout after 30s`

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
- Decommissioned register lifecycle (no new sessions/payments)
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
- Use additive schema changes only (new columns, new tables)
- Perform data backfill in separate migration/step from schema introduction

## Database Baseline Rules
- `id uuid PRIMARY KEY`, `created_at timestamptz NOT NULL DEFAULT now()`, `updated_at timestamptz`
- `is_active boolean NOT NULL DEFAULT true`
- `tenant_id uuid REFERENCES tenants(id)` on tenant-scoped tables (indexed)
- All foreign keys MUST have indexes

## Frontend-Admin (FA) Conventions
- Routes: `frontend-admin/src/app/(protected)/`
- Features: `frontend-admin/src/features/{domain}/` (components, api hooks)
- TanStack Query for server state; Zustand for UI prefs only — never store secrets in Zustand
- Generate API client: `cd frontend-admin && npm run generate:api`
- UI: Ant Design 6 + `@ant-design/nextjs-registry` for SSR; no official `@ant-design/codemod-v6` — migrate deprecated props manually (see stack table above)
- **Ant Design 6 feedback APIs (message / notification / modal):** `<App>` wraps the tree in `ThemeProvider`; components/hooks use `useAntdApp()` from `frontend-admin/src/hooks/useAntdApp.ts` (thin wrapper over `App.useApp()`). Do **not** import `message`, `notification`, or call `Modal.confirm` statically from `antd`.

### Ant Design 6 — batch fix pattern (message / notification / modal)

Static APIs cannot consume theme context. Replace as follows (FA standard: `useAntdApp()`; equivalent to inline `App.useApp()`).

**Message**

```tsx
// Before (wrong)
import { message } from 'antd';
message.success(t('common.auth.loginSuccess'));

// After (correct)
import { useAntdApp } from '@/hooks/useAntdApp';

const { message } = useAntdApp();
message.success(t('common.auth.loginSuccess'));
```

**Notification**

```tsx
// Before (wrong)
import { notification } from 'antd';
notification.error({ message: 'Error', description: 'Something went wrong' });

// After (correct)
import { useAntdApp } from '@/hooks/useAntdApp';

const { notification } = useAntdApp();
notification.error({ message: 'Error', description: 'Something went wrong' });
```

**Modal static methods** (`confirm`, `success`, `warning`, `error`, `info`): use `modal` from `App.useApp()` (or `useAntdApp()`), not `Modal.confirm`. Keep `import { Modal } from 'antd'` only for JSX `<Modal>`.

```tsx
// Before (wrong)
import { Modal } from 'antd';
Modal.confirm({ title: 'Confirm', content: 'Are you sure?', onOk: () => {} });

// After (correct)
import { App } from 'antd';
const { modal } = App.useApp();
modal.confirm({ title: 'Confirm', content: 'Are you sure?', onOk: () => {} });
```

**Non-React callers** (axios interceptors, query client defaults): `showAntdError` from `@/lib/antdAppBridge` (registered by `AntdAppBridgeRegistrar`).

**Helpers** that accept `message.open`: pass `message.open` from `useAntdApp()` in the caller (e.g. `openApiErrorMessage(message.open, t, err, options)`).

## Frontend-POS (FE) Conventions
- Routes: `frontend/app/(tabs)/`
- Offline queue: persist across restarts; never store plain-text voucher codes offline
- TSE via fiskaly API; offline sync when connectivity restored

## Quick Reference

### Useful Commands
```bash
# Backend
dotnet run --project backend/KasseAPI_Final.csproj
dotnet ef migrations add <Name> --project backend
dotnet test

# Frontend Admin
cd frontend-admin && npm run dev
cd frontend-admin && npm run generate:api
cd frontend-admin && npm run test

# Frontend POS
cd frontend && npm start
cd frontend && npm run test
```

### Useful API Endpoints (dev)
```bash
curl http://localhost:5184/api/health
curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/tenants/switcher
curl -X POST http://localhost:5184/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"loginIdentifier":"admin","password":"***"}'
curl -X POST http://localhost:5184/api/admin/backup/trigger \
  -H "Authorization: Bearer ***"
curl http://localhost:5184/api/admin/activities/unread-count \
  -H "Authorization: Bearer ***"
```

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