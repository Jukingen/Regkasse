# Postman guide

Critical Regkasse API calls as a Postman Collection v2.1 file. The collection is generated from [`backend/swagger.json`](../backend/swagger.json). It is a working subset (login, cart, payment, shift, users, tenants, receipts, TSE, DEP), not all 1,200+ OpenAPI paths.

**Collection file:** [`postman/Regkasse.postman_collection.json`](postman/Regkasse.postman_collection.json)

**Last updated:** 2026-11-13

## Import

1. Open Postman.
2. **Import** → **File** → select `docs/postman/Regkasse.postman_collection.json`.
3. Open the collection **Regkasse API** → **Variables**.
4. Set `baseUrl` (`http://localhost:5184` for Development, `https://api.regkasse.at` for production).
5. Set `loginIdentifier` and `password` for a user that exists in that environment. Leave `password` empty in git. Do not commit a filled-in copy.
6. Set `clientApp` to `pos` or `admin`.

Run **Getting Started** from top to bottom. The login test script stores `jwt`, `refreshToken`, `tenantId`, `tenantSlug`, and `userId`. Later requests send `Authorization: Bearer {{jwt}}`.

## Collection variables

| Variable | Role |
|----------|------|
| `baseUrl` | API origin, no trailing slash |
| `jwt` | Access token from login |
| `refreshToken` | Refresh token from login |
| `tenantSlug` | Development `X-Tenant-Id` value. Seeded demo slug is `dev` |
| `tenantId` | Mandant id, filled by login when the user has one |
| `csrfToken` | Filled by **CSRF token** |
| `cashRegisterId`, `productId`, `customerId`, `cartId`, `itemId`, `paymentId`, `receiptId`, `receiptNumber` | Ids you copy from earlier responses |
| `clientApp` | `pos` or `admin` |

## Tenant header and CSRF

Every request sends `X-Tenant-Id: {{tenantSlug}}`.

| Environment | Tenant |
|-------------|--------|
| Development | `X-Tenant-Id` wins, then `?tenant=`, then the host slug. Prefer the header. |
| Production (`api.regkasse.at`) | JWT `tenant_id` after login. The header is ignored. |

**Set tenant header** calls `GET /api/tenants/switcher` so you can confirm the header. Change `tenantSlug`, then send that request again.

`X-XSRF-TOKEN` is sent as `{{csrfToken}}`. Development skips CSRF. When `Security:Csrf` is enabled, run **CSRF token** before POST, PUT, PATCH, or DELETE. Login, refresh, and `/api/health` are exempt. See [`.cursor/rules/100-backend.mdc`](../.cursor/rules/100-backend.mdc) (CSRF).

## Folders

```text
Getting Started
  POST  Auth login (get JWT)
  GET   Set tenant header
  GET   Health check
  GET   CSRF token
Auth
  POST  Login
  GET   Current user
  POST  Refresh token
  POST  Verify 2FA
  POST  Logout
  POST  Logout all sessions
  POST  Revoke refresh token
POS
  Cart
    GET   Current cart
    POST  Open cart
    POST  Add product
    GET   Get cart
    PUT   Update line
    DELETE Remove line
    POST  Clear cart
  Payment
    GET  Payment methods
    POST Pay cash
    GET  Get payment
    GET  Receipt
    GET  Receipt QR
    POST Storno
  Shift
    GET  Current shift
    POST Start shift
    POST End shift
    GET  Daily closing status
    POST Daily closing
Admin
  Users
    GET  List users
    POST Create user
    GET  Get user
    POST Deactivate user
    POST Reset password
  Tenants
    GET  List tenants
    GET  Slug availability
    POST Create tenant
    GET  Get tenant
    GET  List tenant users
    POST Create cashier
    POST Assign user role
    POST Impersonate tenant
  Receipts
    GET List receipts
    GET Get receipt
    GET Receipt by payment
    GET List payments
    GET Get payment
    GET Download receipt PDF
Fiscal
  TSE
    GET  TSE status
    GET  TSE health
    GET  Admin TSE health
    GET  List TSE devices
    POST Provision TSE
    GET  Offline queue status
    GET  Ausfall episodes
  DEP
    GET  RKSV status
    GET  RKSV runtime config
    GET  DEP export
    POST Validate DEP export
    GET  DEP export history
    GET  DEP export status
    GET  Last DEP export
    POST Nullbeleg
    POST Startbeleg
    POST Monatsbeleg (force past month)
    POST Jahresbeleg
    POST Schlussbeleg
    GET  Monatsbeleg list
```

68 requests. Sample JSON bodies use the fields those operations require in `swagger.json` (cash sale, cashier create, Austrian tenant create, forced Monatsbeleg for October 2026).

## API boundaries

| Client | Call | Do not call |
|--------|------|-------------|
| POS | `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` | `/api/admin/*` |
| Admin | `/api/admin/*`, `/api/Auth/*` | `/api/pos/*` |
| Shared | `/api/Auth/*`, `/api/rksv/*` | — |

A missing or foreign mandant returns **404**, not 403. Backend role names stay `SuperAdmin`, `Manager` (Mandanten-Admin in the FA UI), and `Cashier`.

`POST /api/pos/payment` recomputes totals from the catalog. `totalAmount` is a client hint. Storno uses the RKSV reason name (`KundeStorniert`, `FalscherBetrag`, `TechnischerFehler`, `Anderes`). `POST /api/rksv/special-receipts/monatsbeleg?force=true` is the past-month catch-up. Tagesabschluss (`POST /api/pos/shift/daily-closing`) closes the business day.

## Regenerate

After `backend/swagger.json` changes:

```bash
node scripts/generate-postman-collection.mjs
```

The script fails if a listed method and path are no longer in the OpenAPI document. Commit the updated JSON with the script.

## Related

- [`MULTI_TENANT.md`](MULTI_TENANT.md) — tenant resolution
- [`API_CONTRACTS.md`](API_CONTRACTS.md) — contract notes
- [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md) — Super Admin TOTP
- [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md) — RKSV operations
