# API contract supplements

> **Authoritative OpenAPI:** `backend/swagger.json`  
> **AI / governance:** `ai/03_API_CONTRACT.md`, `ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`  
> **User lifecycle (operator):** [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md)

This document records **intentional contract deltas** that may lag swagger until the next regeneration cycle. When code and swagger disagree, **implementation + tests** win until swagger is updated and Orval is re-run.

---

## Authentication API changes

### `POST /api/Auth/login`

Resolves the user by **email first**, then **username** (case-insensitive via `NormalizedUserName`; see `IdentityLoginLookup`).

#### Request (legacy)

```json
{
  "email": "manager@cafe.regkasse.at",
  "password": "string",
  "clientApp": "pos"
}
```

#### Request (preferred)

```json
{
  "loginIdentifier": "manager1",
  "password": "string",
  "clientApp": "admin"
}
```

`loginIdentifier` may be a full **email** or a **username** (e.g. `cashier2`, `manager@cafe.regkasse.at`). **Username lookup is case-insensitive** (`Mustafa`, `mustafa`, `MUSTAFA` resolve to the same account via `NormalizedUserName`).

| Field | Required | Notes |
|-------|----------|--------|
| `loginIdentifier` | Preferred | Email or username |
| `email` | Legacy | Used when `loginIdentifier` is empty; same semantics |
| `password` | Yes | |
| `clientApp` | Config-dependent | `"pos"` \| `"admin"`; required when `AllowLegacyLoginWithoutClientApp` is false |

**Backward compatibility**

- The `email` field is **deprecated** for new clients but still supported (`LoginModel.ResolveLoginIdentifier()`).
- New implementations (FA, POS) should send **`loginIdentifier`**; FA also mirrors the value in `email` for older gateways.
- **Response shape is unchanged** (access token, refresh token, `user` object, permissions).

**Example — username login**

```json
{
  "loginIdentifier": "cashier1",
  "password": "***",
  "clientApp": "pos"
}
```

**Username uniqueness (create / rename):** case-insensitive — cannot register `admin` when `Admin` exists (`UserUniquenessValidationService` + `IdentityLoginLookup`).

**Implementation:** `backend/Models/DTOs/LoginModel.cs`, `backend/Controllers/AuthController.cs`, `backend/Helpers/IdentityLoginLookup.cs`.

**POS client:** `frontend/services/api/authService.ts` (`buildLoginPayload`), `frontend/contexts/AuthContext.tsx` (always `clientApp: "pos"`), UI `frontend/app/(auth)/login.tsx`. Automated checks: `frontend/__tests__/authService.buildLoginPayload.test.ts`, `AuthControllerTests.Login_WithLoginIdentifierUsername_*`.

---

## User management API changes

### `GET /api/admin/users?search=…`

Unified/platform/tenant list (`type=platform` | `type=tenant`). **`search`** matches (case-insensitive, substring):

- `UserName` (login name)
- `Email`
- Display name (`FirstName` + `LastName`)
- `EmployeeNumber`

Example: `?search=mustafa` finds `mustafa` and `mustafa@cafe.regkasse.at`.

---

### `POST /api/admin/users`

Creates a **platform** user when `tenantId` is omitted, or a **tenant** user when `tenantId` is set (delegates to tenant user creation).

#### Request additions

```json
{
  "userName": "manager1",
  "email": "manager@cafe.regkasse.at",
  "role": "Manager",
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "firstName": "Anna",
  "lastName": "Muster",
  "password": null,
  "isOwner": false,
  "employeeNumber": null,
  "taxNumber": null,
  "notes": null
}
```

| Field | Required | Notes |
|-------|----------|--------|
| `userName` | No | Globally unique login name; auto-generated from role when omitted (`manager1`, `user2`, …) |
| `email` | Yes | Contact / email login; distinct from `userName` |
| `role` | Yes | Canonical role name |
| `tenantId` | No | When set → tenant-scoped create |
| `firstName`, `lastName` | No | |
| `password` | No | Secure 12-char password generated when omitted |

#### Response additions (platform — `AdminCreateUserResponseDto`)

```json
{
  "id": "string",
  "userName": "manager1",
  "email": "manager@cafe.regkasse.at",
  "firstName": "Anna",
  "lastName": "Muster",
  "role": "Manager",
  "generatedPassword": "string",
  "forcePasswordChangeOnNextLogin": true,
  "tenantId": "uuid",
  "tenantSlug": "cafe"
}
```

When `tenantId` is set, HTTP **201** body may be **`CreateTenantUserResultDto`** instead (see below).

**Implementation:** `AdminCreateUserRequest`, `AdminCreateUserResponseDto` — `backend/Services/AdminTenants/AdminUserCreateDtos.cs`.

---

### `PATCH /api/admin/users/{id}/username`

Changes the user's **login username** (`ApplicationUser.UserName`). Email is unchanged. Requires `user.manage`. Writes audit `USER_NAME_CHANGE` / `AuditEventType.UserNameChanged`, rotates the Identity **security stamp**, and revokes all active `auth_sessions` / refresh tokens (JWTs with `sid` fail on next request).

#### Request (`UpdateUsernameRequest`)

```json
{
  "newUsername": "cashier2",
  "reason": "Operator requested rename"
}
```

| Field | Required | Notes |
|-------|----------|--------|
| `newUsername` | Yes | 3–50 chars; `[a-zA-Z0-9_-]+`; globally unique; not a [reserved name](#reserved-login-names) |
| `reason` | No | Recommended; stored in audit metadata |

**Rate limit:** At most one successful username change per user every **7 days** (tracked via Identity claim `LastUsernameChange`). Returns **400** business rule when exceeded. No-op (same username) does not count.

**New account guard:** Username cannot be changed within **24 hours** of `ApplicationUser.CreatedAt`. Returns **400** business rule when the account is too new.

#### Reserved login names

Exact match (case-insensitive), blocked on create and rename: `admin`, `root`, `system`, `support`, `helpdesk`, `superadmin`, `superuser`, `administrator`, `moderator`, `owner`.

#### Response (`AdminUpdateUsernameResponse`)

```json
{
  "oldUsername": "cashier1",
  "newUsername": "cashier2"
}
```

**Implementation:** `backend/Models/DTOs/UpdateUsernameRequest.cs`, `AdminUsersController.UpdateUsername`.

After a successful change (not a no-op), the backend sends a **German** transactional email to the user's `email` when SMTP is configured (`Email:Smtp`, optional `SupportContact`). Includes old/new username, admin actor email, UTC timestamp, and support contact. Skipped when SMTP is off or send fails (username update still succeeds).

Each successful change is also appended to **`user_username_history`** (retention: operational DB policy; intended for multi-year audit). Admin read: `GET /api/admin/users/{id}/username-history` → `UserUsernameHistoryDto[]`.

---

### `POST /api/Auth/forgot-username` (anonymous, admin app)

Recover login usernames for an email address.

#### Request

```json
{
  "email": "cashier@cafe.regkasse.at",
  "clientApp": "admin"
}
```

Always returns **200** with a generic message (no account enumeration). When an active user exists and SMTP is configured, sends a **German** email with the **current** login username only (not `user_username_history`).

**FA:** `/login/forgot-username` — `requestForgotUsername` in `frontend-admin/src/features/auth/api/forgotUsername.ts`.

---

### `GET /api/admin/users/username-suggestions?role=Manager`

Preview for **Schnell anlegen** / Quick Create: next auto-generated login username for a role.

#### Response (`UsernameSuggestionResponse`)

```json
{
  "suggestedUsername": "manager3",
  "availableNumbers": [3, 4, 5]
}
```

| Field | Notes |
|-------|--------|
| `suggestedUsername` | Same rule as create: `{rolePrefix}{max+1}` (e.g. `manager1`, `manager2` → `manager3`) |
| `availableNumbers` | Next three free numeric suffixes from the suggestion upward (collision-aware) |

Requires `user.manage`. `role` must exist in Identity roles.

**FA client:** `fetchUsernameSuggestion` — `frontend-admin/src/features/users/api/users.ts` (Quick tab in `CreateUserModal`).

---

### `POST /api/admin/tenants/{tenantId}/users`

Manual tenant user create (non-quick). Same username rules as platform create.

#### Request

```json
{
  "email": "new.user@cafe.regkasse.at",
  "userName": "custom_user",
  "role": "Cashier",
  "firstName": "Max",
  "lastName": "Mustermann",
  "isOwner": false
}
```

#### Response (`CreateTenantUserResultDto`)

```json
{
  "userId": "string",
  "email": "new.user@cafe.regkasse.at",
  "userName": "cashier3",
  "generatedPassword": "string",
  "forcePasswordChangeOnNextLogin": true,
  "success": true,
  "tenantPortalUrl": "https://cafe.regkasse.at",
  "role": "Cashier"
}
```

**Implementation:** `CreateTenantUserRequest`, `AdminTenantUsersController.Create`.

---

### `POST /api/admin/tenants/{tenantId}/users/quick`

**Quick Create** — one-step user creation with auto-generated **username**, **email**, and **password**. No manual credential entry.

#### Request

```json
{
  "role": "Manager"
}
```

Optional custom login name:

```json
{
  "role": "Cashier",
  "userName": "cashier_front"
}
```

| Field | Required | Notes |
|-------|----------|--------|
| `role` | Yes | `Manager`, `Cashier`, or `Accountant` only |
| `userName` | No | Must be unique if provided |

**Generated values**

| Field | Pattern |
|-------|---------|
| `userName` | `{rolePrefix}{n}` — prefixes: `manager`, `cashier`, `user` (Accountant), collision → `_` + random suffix |
| `email` | `{role}_{random6}@{tenantSlug}.regkasse.at` |
| `generatedPassword` | 12-character secure random |

**Rate limit:** max **10** quick users per tenant per hour (audit-based).

#### Response

Same shape as manual tenant create — `CreateTenantUserResultDto` (201 Created).

```json
{
  "userId": "...",
  "email": "manager_a3f9k2@cafe.regkasse.at",
  "userName": "manager1",
  "generatedPassword": "...",
  "forcePasswordChangeOnNextLogin": true,
  "success": true,
  "tenantPortalUrl": "https://cafe.regkasse.at",
  "role": "Manager"
}
```

**Implementation:** `CreateQuickTenantUserRequest`, `QuickUserGeneratorService`, `AdminTenantUsersController.CreateQuick`.

**FA client:** `createQuickUser` — `frontend-admin/src/features/super-admin/api/quickUser.ts`.

---

## Billing / License Management

> **Full operator guide:** [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md)  
> **OpenAPI:** `backend/swagger.json` — regenerate FA client: `cd frontend-admin && npm run generate:api`

Billing-format license keys: `REGK-{yyyyMMdd}-{tenantSlug}-{8chars}`.  
**Not** deployment / On-Premise `LicenseService` (`issued_licenses`) — see [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md).

### Super Admin Endpoints

Controller: `AdminBillingController` — `[Authorize(Roles = SuperAdmin)]` on all routes below.

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/admin/billing/license-sales/preview` | Preview a license sale (pricing, key, invoice number) | SuperAdmin |
| POST | `/api/admin/billing/license-sales` | Create a license sale; updates tenant `license_*` fields | SuperAdmin |
| GET | `/api/admin/billing/license-sales` | List sales (pagination, `tenantId`, `status`, `search`, date range) | SuperAdmin |
| GET | `/api/admin/billing/license-sales/{id}` | Get sale detail | SuperAdmin |
| GET | `/api/admin/billing/license-sales/by-key/{licenseKey}` | Lookup sale by billing license key | SuperAdmin |
| GET | `/api/admin/billing/license-sales/{id}/pdf` | Download invoice PDF | SuperAdmin |
| POST | `/api/admin/billing/license-sales/preview-pdf` | Generate preview PDF without persisting sale | SuperAdmin |
| POST | `/api/admin/billing/license-sales/{id}/cancel` | Cancel a sale; clears tenant license when current | SuperAdmin |
| GET | `/api/admin/billing/stats` | Sales statistics (revenue, active/expiring counts) | SuperAdmin |
| GET | `/api/admin/billing/license-sales/expiring` | Licenses expiring within threshold (default 30 days) | SuperAdmin |
| GET | `/api/admin/billing/tenants/{tenantId}/license` | Mandant license info for a tenant | SuperAdmin |
| GET | `/api/admin/billing/audit` | Paginated billing audit log | SuperAdmin |
| GET | `/api/admin/billing/license-sales/{id}/audit` | Audit trail for one sale | SuperAdmin |
| GET | `/api/admin/billing/tenants/{tenantId}/reminders` | License expiry reminders for tenant | SuperAdmin |
| POST | `/api/admin/billing/reminders/check` | Manual: create missing reminders | SuperAdmin |
| POST | `/api/admin/billing/reminders/send` | Manual: process pending reminders | SuperAdmin |

**Create sale body (`CreateLicenseSaleRequest`):**

| Field | Required | Notes |
|-------|----------|--------|
| `tenantId` | Yes | Target mandant UUID |
| `licensePlan` | Yes | `6_months` \| `12_months` \| `custom` |
| `priceNet` | Yes | > 0 |
| `vatRate` | No | Default 20 |
| `customValidUntilUtc` | If `custom` | Must be after license start |
| `notes` | No | Free text |

**List sales query (`GET /api/admin/billing/license-sales`):** `page`, `pageSize`, `tenantId`, `status` (`active` \| `cancelled` \| `refunded` \| `all`), `search`, `fromDate`, `toDate`.

### Manager Endpoints

Mandant SaaS license lifecycle — JWT with **tenant context** required (`ICurrentTenantAccessor.TenantId`). Cross-tenant key → HTTP 400 (German message in body).

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/license/status` | License snapshot (deployment + optional mandant overlay when tenant resolved) | Anonymous / JWT |
| GET | `/api/license/billing/status` | Current mandant billing license status (`TenantLicenseStatus`) | JWT + tenant |
| POST | `/api/license/activate` | Unified activate: billing-format key → mandant branch; else deployment | JWT + tenant (billing keys) |
| POST | `/api/license/billing/activate` | Activate billing-format key for current tenant | JWT + tenant |
| POST | `/api/license/extend` | Extend mandant license with a new billing sale key | JWT + tenant |

**Extend / activate body:**

```json
{ "licenseKey": "REGK-20270101-cafe-A7F3K2D9" }
```

**Responses:** `ActivationResult` / `ExtendResult` — `{ success, message, licenseKey, validUntil, plan }` (field names per OpenAPI).

> **Note:** `GET /api/license/status` and `POST /api/license/activate` are shared with POS (anonymous allowed for deployment branch). Billing keys on `POST /api/license/activate` require authenticated user + tenant context. Prefer **`/api/license/billing/*`** for new Admin clients to avoid collision with deployment licensing.

### Legacy Admin Manager paths (still active)

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| GET | `/api/admin/license/mandant` | `license.manage` | Mandant license overview |
| POST | `/api/admin/license/mandant/preview` | `license.manage` | Preview key without applying |
| POST | `/api/admin/license/mandant/extend` | `license.manage` | Extend via REGK key (legacy) |
| POST | `/api/admin/license/extend` | `settings.manage` | Canonical admin extend (billing service) |

**Implementation:** `AdminBillingController`, `LicenseController` (+ `LicenseController.Manager.cs`), `AdminLicenseController` (mandant + extend partials).

---

## Offline orders API (full POS snapshots)

> **Canonical doc:** [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md)  
> **Separation guide:** [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md)  
> **Legacy (payment intents):** `/api/offline-transactions/replay`, `/api/admin/offline-transactions`, FA `/admin/tse/offline-transactions` — unchanged; separate table `offline_transactions`.

New model stores **full `CreatePaymentRequest` snapshots** in `offline_orders` (not `offline_transactions`).

### POS — `/api/pos/offline-orders`

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| POST | `/api/pos/offline-orders` | `payment.take` | Save offline order |
| GET | `/api/pos/offline-orders/pending` | `payment.take` | Query `cashRegisterId` required |
| POST | `/api/pos/offline-orders/replay` | `payment.take` | Replay pending for register; batch BelegNr reserve |
| GET | `/api/pos/offline-orders/{offlineOrderId}/status` | `payment.take` | Status by business id (`OFFLINE-…`) |

**Save body (`OfflineOrderRequest`):**

```json
{
  "cashRegisterId": "uuid",
  "orderData": { },
  "orderTotal": 12.50,
  "paymentMethod": "cash"
}
```

**Replay response (`ReplayOfflineOrdersResult`):** `{ total, success, failed, details[] }` with per-order `{ orderId, success, paymentId?, invoiceNumber?, errorMessage? }`.

### Admin — `/api/admin/offline-orders`

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| GET | `/api/admin/offline-orders` | `payment.view` | List: `status`, `cashRegisterId`, `pageNumber`, `pageSize` |
| POST | `/api/admin/offline-orders/{id}/replay` | `payment.view` | Replay one pending order |
| POST | `/api/admin/offline-orders/replay-all` | `payment.view` | Optional `cashRegisterId` query |

**FA route:** `/rksv/offline-orders` (RKSV → Diagnose). Orval hooks: `useGetApiAdminOfflineOrders`, `usePostApiAdminOfflineOrdersIdReplay`, `usePostApiAdminOfflineOrdersReplayAll` in `@/api/generated/admin/admin`.

**OpenAPI regen:**

```bash
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/verify-api-client.mjs
```

**Rules:** 72 h expiry (cleanup deletes pending); max 3 sync attempts; **voucher not allowed** offline.

**Implementation:** `PosOfflineOrdersController`, `AdminOfflineOrdersController`, `OfflineOrderService`, `SequenceReservationService`.

---

## Client migration checklist

| Client | Action |
|--------|--------|
| **frontend-admin** | Login: `loginIdentifier` + legacy `email`; display `userName` from create/quick responses |
| **frontend (POS)** | Login: `loginIdentifier` via `authService.buildLoginPayload` |
| **Integrations** | Replace `email`-only login bodies with `loginIdentifier`; keep `clientApp` |

After backend DTO changes:

1. Update `backend/swagger.json`
2. `cd frontend-admin && npm run generate:api`
3. `node scripts/verify-api-client.mjs` (repo root)

---

## Related documentation

- [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md) — operator flows, roles, FA surfaces
- [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) — billing / mandant license API and services
- [`docs/release/OFFLINE_ORDERS_FULL_SNAPSHOT.md`](release/OFFLINE_ORDERS_FULL_SNAPSHOT.md) — full POS order snapshots (2026-06-27)
- [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) — Authentication section
- [`frontend-admin/README.md`](../frontend-admin/README.md) — Schnell anlegen
- [`frontend/README.md`](../frontend/README.md) — POS login
