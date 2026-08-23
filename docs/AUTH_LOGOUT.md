# Logout security

Technical documentation (English). UI copy stays in product i18n (German on POS; FA de/en/tr).

Hub for **how sessions end** after login. Related: [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md), cookie config in [`../backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) § Auth cookies, session admin in [`AGENTS.md`](../AGENTS.md) § Super Admin.

---

## Summary

| Surface | How the client authenticates logout | Cookies cleared | Refresh / `auth_sessions` | Identity `SecurityStamp` |
|---------|--------------------------------------|-----------------|---------------------------|---------------------------|
| **FA** (`clientApp=admin`) | `POST /api/Auth/logout` with **HttpOnly cookies** (`withCredentials`). Do not send a Bearer token from `localStorage`. | `rk_admin_access_token`, `rk_admin_refresh_token`, plus legacy `access_token` / `refresh_token` | Current `sid` + all **admin** sessions for that user | Rotated (JWT `sst` check fails) |
| **POS** (`clientApp=pos`) | `POST /api/Auth/logout` with **`Authorization: Bearer`** (SecureStore / native). Web POS may also present `rk_pos_*` cookies. | `rk_pos_access_token`, `rk_pos_refresh_token`, plus legacy names | Current `sid` + all **pos** sessions for that user | Rotated |
| **Logout-all** | `POST /api/Auth/logout-all` (authenticated) | **Both** apps + legacy | **All** sessions for that user | Rotated |
| **Force logout** (Super Admin) | `/api/admin/sessions` | Not a browser logout; tokens fail on next request | All sessions (`LogoutAllAsync`) | Rotated (`ISessionManagementService.ForceLogoutAsync`) |

**Cookie split:** FA and POS use **different HttpOnly cookie names** so two users (or the same user) can hold sessions in one browser without overwriting each other. Logout of one app must not expire the other app’s cookies.

**Stamp vs cookie split:** Rotating `SecurityStamp` on a single-app logout invalidates **all outstanding access JWTs** for that user (both apps) via JWT `OnTokenValidated`. The **other app’s refresh sessions are not revoked**. That app can recover by rotating refresh (`POST /api/Auth/refresh`) and receiving a new JWT with the new `sst`.

---

## Threat model (what logout must stop)

| Threat | Control |
|--------|---------|
| Stolen access JWT reused after “log out” | Access-token **blacklist** (digest, not raw JWT in logs) + `sst` mismatch after stamp rotation |
| Stolen refresh token used to mint new access tokens | Session + refresh rows revoked (`LogoutSessionAsync` / `RevokeForUserAndClientAppAsync` / `LogoutAllAsync`) |
| FA logout wiping a POS cashier in the same browser | App-scoped cookie names + app-scoped refresh revoke; unknown `clientApp` is a **no-op** for scoped revoke |
| Leftover shared cookies (`access_token`) colliding after rollout | Legacy names are still **read and expired** on clear |
| Stolen CSRF double-submit token reused after logout | Server cache entry removed + `XSRF-TOKEN` cookie expired |
| Client still holding session UX after API logout | FA clears tenant/theme/preference localStorage + React Query; POS clears SecureStore + offline queues |
| Enumeration / JWT leakage in audit | Logout audit logs user id / reason only — never the raw token |

Logout is **authenticated**. Unauthenticated callers receive **401**. Failures in cart cleanup, shift auto-close, CSRF invalidate, or stamp rotation **must not** abort cookie/session teardown (best-effort, logged).

---

## Endpoints

| Method | Path | Auth | CSRF (Production, `Security:Csrf:Enabled=true`) |
|--------|------|------|--------------------------------------------------|
| `POST` | `/api/Auth/logout` | JWT (Bearer **or** app cookie) | **Required** for browser mutations (not on the login/refresh exempt list) |
| `POST` | `/api/Auth/logout-all` | JWT | Required for browser mutations |
| `POST` | `/api/Auth/revoke` | JWT + refresh token in body | Required for browser mutations |
| `POST` | `/api/Auth/refresh` | Refresh cookie or body | **Exempt** (same as login) |

Token resolution order (middleware / JWT `OnMessageReceived`): **`Authorization: Bearer` first**, then app cookies. Bearer-first avoids an FA cookie shadowing a POS native Bearer in a shared browser.

`clientApp` for scoped logout: JWT `app_context` claim, else `X-App-Context`, else Origin (`admin.*` / `pos.*`). Unknown app → scoped refresh revoke is skipped (POS cannot wipe FA).

---

## Backend sequence (`POST /api/Auth/logout`)

Implementation: `AuthController.Logout`.

1. Require authenticated user id (else 401).
2. **Blacklist** the current access token (header or cookie).
3. Resolve `logoutClientApp`.
4. If JWT has `sid`, **end that session** (`IRefreshTokenService.LogoutSessionAsync`).
5. **Revoke refresh tokens / sessions for that user and client app only** (`RevokeForUserAndClientAppAsync`).
6. **Rotate Identity `SecurityStamp`** (`UserManager.UpdateSecurityStampAsync`) so existing JWTs fail `sst` vs database stamp in `OnTokenValidated` → `ISessionManagementService.IsSessionValidAsync`.
7. **Invalidate CSRF** for the request cookie/header value and expire `XSRF-TOKEN`.
8. Best-effort: user cart cleanup, POS shift auto-close.
9. Audit `UserLogout` (`reason=logout`); never log the raw JWT.
10. **Expire auth cookies** for that app + legacy names (`IAuthCookieService.ClearAuthCookies`).

`POST /api/Auth/logout-all` blacklists the current access token, auto-closes shift, `LogoutAllAsync`, rotates stamp, invalidates CSRF, and clears **all** auth cookies (`clientApp: null`).

Refresh tokens are stored **hashed**. Revoke marks `auth_sessions.RevokedAtUtc` and related refresh rows — the opaque token itself is never written to logs.

---

## Cookie names

Writes always use app-specific names. Clears also expire legacy aliases.

| App | Access | Refresh |
|-----|--------|---------|
| FA (`admin`) | `rk_admin_access_token` | `rk_admin_refresh_token` |
| POS (`pos`) | `rk_pos_access_token` | `rk_pos_refresh_token` |
| Legacy (read + expire only) | `access_token` | `refresh_token` |

FA also uses a **non-secret** Edge presence cookie (`rk_admin_edge_session`) so Next.js `proxy.ts` can gate routes without reading the HttpOnly JWT. Logout clears it on the client (`authStorage.removeToken`).

Config: `AuthCookies` in [`../backend/CONFIGURATION.md`](../backend/CONFIGURATION.md). Production `Domain` (e.g. `.regkasse.at`) makes admin cookies visible to Edge; `HttpOnly` + `Secure` + `SameSite=Lax` in production.

---

## Frontend

### Frontend Admin (FA)

1. User clicks logout → `useAuth.logout` → `POST /api/Auth/logout` (cookies, `X-App-Context: admin`, CSRF header when enabled).
2. `finally` (even if the API call fails):
   - Clear in-memory CSRF cache.
   - `authStorage.removeToken()` — Edge cookie, tenant bootstrap, personalization/theme keys, format locale; **not** UI language (`app_language`).
   - React Query auth user → `null` + cache `clear`.
   - `AuthSessionInvalidationListener` resets Zustand UI preferences on `AUTH_SESSION_CLEARED_EVENT`.
   - Redirect `/login`.

JWTs are **not** stored in `localStorage`. Do not put tokens in Zustand.

### POS

1. Cashier clicks logout → `AuthContext` / `authService.logout` → `POST /api/Auth/logout` with Bearer from SecureStore.
2. `finally`:
   - Offline order queue `clearAll()`.
   - Pending payment / TSE intent queue cleared (never persist voucher codes).
   - `sessionManager.clearSession()` (`token`, `refreshToken`, user, expiry).
3. Auth context reset → `/(auth)/login`.

Native POS source of truth remains **SecureStore + Bearer**, not cookies.

---

## Admin force logout vs user logout

| | User logout (this app) | Logout-all | Super Admin force logout |
|--|------------------------|------------|---------------------------|
| Refresh sessions | This `clientApp` only | All | All |
| Cookies | This app + legacy | Both apps | N/A (other devices drop on next API call) |
| `SecurityStamp` | Yes | Yes | Yes |
| Typical UI | FA / POS logout button | Account “sign out everywhere” | `/admin/sessions`, user detail |

Username change and password reset also rotate `SecurityStamp` and revoke sessions (see `AGENTS.md` § Session Management).

---

## Tests (keep in sync)

| Area | Tests |
|------|--------|
| Stamp + app-scoped revoke | `AuthControllerTests.Logout_RotatesSecurityStamp_AndRevokesClientAppSessions` |
| Admin revoke does not kill POS | `RefreshTokenServiceTests.RevokeForUserAndClientApp_*` |
| Cookie clear scoped | `AuthCookieServiceTests.ClearAuthCookies_admin_does_not_expire_pos` |
| CSRF cache drop | `CsrfTokenServiceTests.InvalidateToken_*` |
| FA localStorage | `authStorage.test.ts` (`removeToken` clears tenant/theme/preferences) |
| POS offline queues | `offlineStorage` `clearAll` tests |

---

## Operational notes

- CSRF is **on** in Production (`Security:Csrf:Enabled=true`). FA logout is a POST: the client must send `X-XSRF-TOKEN` matching `XSRF-TOKEN`. Login and refresh stay exempt.
- After FA logout, a POS tab for the **same user** may see 401 on the next API call until refresh; interceptors should refresh once, not immediately treat stamp mismatch as a hard logout if a POS refresh session still exists.
- Do not reintroduce a shared `access_token` write path.
- Do not invent `InvalidateTokenAsync(userId)` for CSRF — the cache is keyed by **token value**.
- Do not log passwords, refresh tokens, voucher codes, or raw JWTs on the logout path.
