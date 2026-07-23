# API Contract (Regkasse)

Human-oriented summary of the HTTP API.  

| Source | Role |
|--------|------|
| **`backend/swagger.json`** | **Authoritative** OpenAPI 3 contract (`info.version`: **v1**, title: *Kasse API*) |
| This file (`API_CONTRACT.md`) | Curated index for Auth, Users, Digital, Billing |
| [`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) | Detailed deltas (username rules, Quick Create, billing notes) |
| [`ai/03_API_CONTRACT.md`](ai/03_API_CONTRACT.md) / [`ai/10_API_BOUNDARY_POLICY.md`](ai/10_API_BOUNDARY_POLICY.md) | Agent boundaries |
| Orval | `frontend-admin/src/api/generated/**` from swagger |

**Aligned with:** committed `backend/swagger.json` as of 2026-07-21.  
When docs and swagger disagree, **regenerate swagger from code** (`node scripts/generate-backend-openapi.mjs`) and treat implementation + tests as truth until swagger is refreshed.

### Client boundaries

| Client | Prefix | Forbidden |
|--------|--------|-----------|
| POS | `/api/pos/*`, shared Auth/Receipts as documented | `/api/admin/*` |
| Admin (FA) | `/api/admin/*`, shared Auth | `/api/pos/*` |
| Sites / public | `/api/public/*`, `/api/sites/*` | POS/FA fiscal write surfaces |
| Shared | `/api/Auth/*`, `/api/user/*`, `/api/rksv/*`, `/api/license/*` | — |

Cross-tenant resource access → **HTTP 404** (not 403). Production tenant from JWT; Dev may use `X-Tenant-Id` / `?tenant=`.

---

## Authentication API

OpenAPI paths under `/api/Auth/*` (case-sensitive controller segment **`Auth`**):

| Method | Path | Auth | Notes |
|--------|------|------|--------|
| `POST` | `/api/Auth/login` | Anonymous | Prefer `loginIdentifier` + `clientApp` |
| `POST` | `/api/Auth/verify-2fa` | Anonymous | SuperAdmin TOTP after challenge |
| `GET` | `/api/Auth/me` | Bearer | Current user + permissions |
| `POST` | `/api/Auth/refresh` | Anonymous/body | Refresh token rotation |
| `POST` | `/api/Auth/refresh-session` | Bearer | Session keep-alive |
| `POST` | `/api/Auth/logout` | Bearer | |
| `POST` | `/api/Auth/logout-all` | Bearer | All sessions |
| `POST` | `/api/Auth/revoke` | Bearer | |
| `POST` | `/api/Auth/forgot-password` | Anonymous | |
| `POST` | `/api/Auth/forgot-username` | Anonymous | Admin app; no enumeration |
| `POST` | `/api/Auth/register` | (policy) | Limited / legacy surface |

### `POST /api/Auth/login`

Swagger schema: `LoginModel`.

```json
{
  "loginIdentifier": "manager1",
  "password": "***",
  "clientApp": "admin"
}
```

| Field | Required | Notes |
|-------|----------|--------|
| `loginIdentifier` | Preferred | Email **or** username (case-insensitive username) |
| `email` | Legacy | **Deprecated** in OpenAPI; used only if `loginIdentifier` empty |
| `password` | Yes | |
| `clientApp` | Usually yes | `"pos"` \| `"admin"` |

| Outcome | HTTP | Notes |
|---------|------|--------|
| Success | 200 | Access + refresh tokens + `user` |
| Invalid credentials | **401** | Generic message (no user enumeration) |
| Missing identifier/password / bad `clientApp` | 400 | |
| SuperAdmin 2FA required | 200 | `requires2FA: true`, `twoFactorToken` — no access token yet |

Hub: [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md). Detail: [`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) § Authentication.

### `POST /api/Auth/verify-2fa`

Completes SuperAdmin login with TOTP (or Development bypass codes only in Development).

### `GET /api/Auth/me`

Returns authenticated profile; Admin FA permissions filtered via `AdminAppPermissionProfile` when `app_context=admin`.

---

## User Management API

Canonical Admin prefix: **`/api/admin/users`**. Permission: typically `users.view` / `users.manage` (`user.manage` naming in some docs).

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/admin/users` | List (`type=platform`\|`tenant`, `search=…`) |
| `POST` | `/api/admin/users` | Create platform or tenant user (`tenantId` optional) |
| `GET` | `/api/admin/users/{id}` | Get |
| `PATCH` | `/api/admin/users/{id}` | Update |
| `PATCH` | `/api/admin/users/{id}/username` | Rename login; audit + session revoke |
| `GET` | `/api/admin/users/{id}/username-history` | History |
| `GET` | `/api/admin/users/username-suggestions` | Quick Create preview |
| `POST` | `/api/admin/users/{id}/deactivate` | Deactivate |
| `POST` | `/api/admin/users/{id}/reactivate` | Reactivate |
| `POST` | `/api/admin/users/{id}/generate-temporary-password` | Temp password |
| `POST` | `/api/admin/users/{id}/force-password-reset` | Force change |
| `POST` | `/api/admin/users/{id}/reset-password` | Reset |
| `GET` | `/api/admin/users/{id}/permissions` | Effective permissions |
| `GET`/`PUT` | `/api/admin/users/{id}/tenants` | Memberships |
| `GET` | `/api/admin/users/{id}/activity` | Activity |
| `GET` | `/api/admin/users/{id}/activity-report` | Report |
| `GET` | `/api/admin/users/bulk-import/template` | CSV template |
| `POST` | `/api/admin/users/bulk-import/preview` | Preview |
| `POST` | `/api/admin/users/bulk-import` | Start job |
| `GET`/`DELETE` | `/api/admin/users/bulk-import/jobs/{jobId}` | Job status / cancel |
| `GET` | `/api/admin/users/bulk-import/results/{resultId}` | Results |

### Create (`POST /api/admin/users`)

Swagger: `AdminCreateUserRequest` — **required:** `email`, `role`. Optional: `userName`, `tenantId`, `password`, names, `isOwner`, etc.

- Omit `userName` → auto `{rolePrefix}{n}`.
- Omit `password` → generated once in response.
- Tenant create also via `/api/admin/tenants/{tenantId}/users` and **Quick Create** `/users/quick` (see supplements doc).

### Username change (`PATCH …/username`)

- Body: `newUsername`, optional `reason`.
- Case-insensitive uniqueness; reserved names blocked; rate limits apply.
- Audit `UserNameChanged`; security stamp rotation; sessions invalidated.

Operator guide: [`docs/USER_MANAGEMENT.md`](docs/USER_MANAGEMENT.md).

---

## Digital Services & Online Orders

Non-fiscal website/app generation and online-order inbox. Working hours gate **public intake only** — never POS/FA ([`docs/WORKING_HOURS.md`](docs/WORKING_HOURS.md)).

### Public / sites (storefront)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/sites/{tenantSlug}/status` | Open/closed + `canOrder` |
| `GET` | `/api/sites/{tenantSlug}/status/special` | Special hours overlay |
| `GET` | `/api/public/tenants/{slug}` | Public tenant metadata |
| `GET` | `/api/public/tenants/{slug}/menu` | Public menu |
| `GET` | `/api/public/sites/{slug}` | Site payload |
| `POST` | `/api/public/online-orders` | Place order (`ONLINE_ORDERS_CLOSED` when closed) |
| `GET` | `/api/public/online-orders/status` | Customer status lookup |
| `POST` | `/api/public/online-orders/{orderId}/payment-intent` | Payment intent |
| `POST` | `/api/public/online-orders/payments/confirm` | Confirm payment |
| `GET` | `/api/public/customer/dashboard` | Customer dashboard |

### Admin — digital control

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/admin/digital/tenants` | List digital tenants |
| `GET` | `/api/admin/digital/tenants/{tenantId}` | Detail |
| `POST` | `/api/admin/digital/{tenantId}/enable` | Enable |
| `POST` | `/api/admin/digital/{tenantId}/toggle` | Toggle |
| `PUT` | `/api/admin/digital/{tenantId}/price` | Pricing |
| `POST` | `/api/admin/digital/{tenantId}/request` | Mandant request |
| `GET` | `/api/admin/digital/{tenantId}/requests` | Tenant requests |
| `GET` | `/api/admin/digital/requests` | Platform queue |
| `POST` | `/api/admin/digital/requests/{id}/approve` | Approve |
| `POST` | `/api/admin/digital/requests/{id}/reject` | Reject |

### Admin — website generators

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/admin/website/templates` | Templates |
| `GET` | `/api/admin/website/pricing` | Pricing |
| `GET` | `/api/admin/website/my-services` | Mandant services |
| `POST` | `/api/admin/website/preview` | Preview |
| `POST` | `/api/admin/website/generate` | Generate site |
| `POST` | `/api/admin/website/menu-sync` | Sync menu |
| `POST` | `/api/admin/website/mobile/generate` | Mobile |
| `POST` | `/api/admin/website/mobile/package` | Package |

### Admin — online orders inbox

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/admin/online-orders` | List |
| `GET` | `/api/admin/online-orders/analytics` | Analytics |
| `GET` | `/api/admin/online-orders/{id}` | Detail |
| `GET` | `/api/admin/online-orders/{id}/timeline` | Timeline |
| `PATCH` | `/api/admin/online-orders/{id}/status` | Status lifecycle |
| `POST` | `/api/admin/online-orders/{id}/accept` | Accept (+ optional POS bridge; Super Admin approve permission) |
| `POST` | `/api/admin/online-orders/{id}/payment-intent` | Payment intent |

Guides: [`docs/DIGITAL_SERVICES.md`](docs/DIGITAL_SERVICES.md), [`docs/ONLINE_ORDERS.md`](docs/ONLINE_ORDERS.md).

---

## Billing / License Management

Two layers (do not conflate):

| Layer | Purpose | Primary routes |
|-------|---------|----------------|
| **Mandant license sales** | Super Admin sells/extends tenant licenses (`REGK-…` billing keys) | `/api/admin/billing/*`, `/api/license/extend`, `/api/license/billing/*` |
| **Deployment / offline license** | Machine-bound On-Premise activation | `/api/license/status`, `/activate`, `/features` |

Hub: [`docs/BILLING_TENANT_LICENSE.md`](docs/BILLING_TENANT_LICENSE.md), [`docs/LICENSE_SYSTEM.md`](docs/LICENSE_SYSTEM.md).

### Super Admin billing

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/admin/billing/license-sales/preview` | Preview quote (`LicenseSalePreviewRequest`) |
| `POST`/`GET` | `/api/admin/billing/license-sales` | Create / list sales |
| `GET` | `/api/admin/billing/license-sales/{id}` | Detail |
| `GET` | `/api/admin/billing/license-sales/{id}/pdf` | Invoice PDF |
| `POST` | `/api/admin/billing/license-sales/preview-pdf` | Preview PDF |
| `POST` | `/api/admin/billing/license-sales/{id}/cancel` | Cancel |
| `GET` | `/api/admin/billing/license-sales/by-key/{licenseKey}` | Lookup by key |
| `GET` | `/api/admin/billing/license-sales/expiring` | Expiring |
| `GET` | `/api/admin/billing/stats` | Stats |
| `GET` | `/api/admin/billing/tenants/{tenantId}/license` | Tenant license view |
| `GET` | `/api/admin/billing/audit` | Billing audit |
| `GET` | `/api/admin/billing/license-sales/{id}/audit` | Sale audit |
| `POST`/`GET` | `/api/admin/billing/subscriptions` | Subscriptions |
| `GET` | `/api/admin/billing/tenants/{tenantId}/subscriptions` | Per tenant |
| `POST` | `/api/admin/billing/subscriptions/{subscriptionId}/cancel` | Cancel sub |
| `GET` | `/api/admin/billing/digital` / `digital-pricing` | Digital billing views |
| `POST` | `/api/admin/billing/reminders/check` \| `/send` | Reminders |
| `GET` | `/api/admin/billing/tenants/{tenantId}/reminders` | Tenant reminders |
| Billing backup helpers | `/api/admin/billing/backup/*` | Billing-domain backup utilities (not RKSV DR) |

Preview body (swagger `LicenseSalePreviewRequest`): **required** `tenantId`, `licensePlan`, `priceNet`; optional `vatRate`, `customValidUntilUtc`.

### Mandant / shared license activation

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/license/billing/status` | Mandant billing status |
| `POST` | `/api/license/billing/activate` | Activate billing key |
| `POST` | `/api/license/extend` | Extend with key (Mandanten-Admin) |
| `GET` | `/api/license/status` | Deployment license status |
| `GET` | `/api/license/features` | Features |
| `POST` | `/api/license/activate` | Activate deployment license |

---

## Regenerating the contract

```bash
node scripts/generate-backend-openapi.mjs
npm run generate:api                 # Orval → frontend-admin
npm run verify:api-client
node scripts/validate-critical-openapi-paths.mjs
```

Governance: [`ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`](ai/11_OPENAPI_CONTRACT_GOVERNANCE.md).

---

## Changelog

Record **intentional public API** changes here (and regenerate swagger + Orval in the same PR when possible).

| Date | Change | Impact |
|------|--------|--------|
| 2026-07-21 | Root `API_CONTRACT.md` created; sections aligned to current `backend/swagger.json` (Auth, Users, Digital/Online Orders, Billing/License path inventory). | Docs |
| 2026-07 | Invalid `/api/Auth/login` prefers `loginIdentifier`; `email` marked **deprecated** in OpenAPI `LoginModel`. Invalid credentials → **401**. | Clients must migrate off `email`-only bodies |
| 2026-07 | SuperAdmin `POST /api/Auth/verify-2fa` + login challenge (`requires2FA`). | FA Production login |
| 2026-06+ | Admin user username lifecycle: `PATCH …/username`, history, suggestions, Quick Create, bulk-import job APIs. | FA Access hub |
| 2026-06+ | Digital services + online orders: `/api/admin/digital/*`, `/api/admin/website/*`, `/api/admin/online-orders/*`, `/api/public/*`, `/api/sites/*`. | Sites + FA |
| 2026-05+ | Mandant billing: `/api/admin/billing/license-sales*`, `/api/license/extend`, billing status/activate. | Super Admin / Mandanten-Admin |
| Ongoing | Critical path CI gate: `scripts/validate-critical-openapi-paths.mjs` (POS payment, backup, offline-orders, billing preview/sales, RKSV overviews). | CI |

Older narrative detail remains in [`docs/API_CONTRACTS.md`](docs/API_CONTRACTS.md) and [`docs/CHANGELOG*.md`](docs/README.md).

---

## Related

| Doc | Topic |
|-----|--------|
| [`docs/API_LEGACY_DEPRECATION.md`](docs/API_LEGACY_DEPRECATION.md) | Legacy `/api/Payment` etc. sunset |
| [`ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`](ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md) | Route inventory |
| [`testsprite/api/`](testsprite/api/) | Smoke YAML vs swagger |
| Swagger UI (Dev) | `http://localhost:5184/swagger` |
