# Authorization Hardening Sprint – PR Summary

## Goals Achieved

1. **Legacy admin role removed** – Single admin role is `Admin`; migration drops legacy role from AspNetRoles; seed does not create it.
2. **Permission-first** – PaymentSecurityMiddleware uses JWT permission claims (PaymentTake, PaymentCancel, RefundCreate); FE-Admin route guard is permission-based and fail-closed.
3. **Fail-closed FE-Admin** – PermissionRouteGuard denies access when permissions are missing or empty; guard is used in protected layout.
4. **Constants** – Backend uses `Roles.*` and `AppPermissions.*`; POS frontend uses `ROLES` and permission checks where applicable.

---

## Changed Files

### Backend

| File | Change |
|------|--------|
| `Authorization/Roles.cs` | Added `FallbackUnknown` constant for token fallback role. |
| `Authorization/AuthorizationExtensions.cs` | No change (already permission-first). |
| `Middleware/PaymentSecurityMiddleware.cs` | Replaced role allow-list with permission-based check: requires at least one of `PaymentTake`, `PaymentCancel`, `RefundCreate` from JWT permission claims. |
| `Data/RoleSeedData.cs` | Comment: single admin role is Admin only. |
| `Data/UserSeedData.cs` | Use `Roles.SuperAdmin`, `Roles.Cashier` instead of string literals. |
| `Controllers/AuthController.cs` | Use `Roles.FallbackUnknown` instead of `"User"`. |
| `Services/TokenClaimsService.cs` | Use `Roles.FallbackUnknown` instead of `"User"`. |
| `Migrations/20260309120000_DropAdministratorRole.cs` | Deletes legacy admin role row from AspNetRoles (historical DB value in SQL). |

### Backend Tests

| File | Change |
|------|--------|
| `KasseAPI_Final.Tests/PaymentSecurityMiddlewareTests.cs` | Switched to permission-based tests: user with PaymentTake/PaymentCancel/RefundCreate allowed; user with no payment permission or no permission claims gets 403. |

### Frontend-Admin

| File | Change |
|------|--------|
| `src/shared/auth/PermissionRouteGuard.tsx` | **Fail-closed:** `allowed` is true only when `permissions.length > 0` and route permission check passes; otherwise redirect to `/403`. |
| `src/app/(protected)/layout.tsx` | Wrapped layout content with `<PermissionRouteGuard>`. |

### Frontend (POS)

| File | Change |
|------|--------|
| `types/auth.ts` | Added `ROLES` constant object (SuperAdmin, Admin, Manager, etc.). |
| `hooks/usePermission.ts` | Use `ROLES.*` for role shortcuts; `canManageSystemSettings`, `canViewReports`, `canManageUsers` prefer permission checks (`settings.manage`, `report.view`, `user.manage`) with role fallback. |
| `components/RoleGuard.tsx` | Import `ROLES`; `AdminOnly` uses `ROLES.Admin`, `ROLES.SuperAdmin`. |

### Docs

| File | Change |
|------|--------|
| `docs/AUTHORIZATION_HARDENING_AUDIT.md` | **New.** File-by-file audit verification and implementation order. |
| `docs/AUTHORIZATION_HARDENING_PR_SUMMARY.md` | **New.** This PR summary. |

---

## Risk

- **FE-Admin:** If the backend does not send `permissions` in the login/me response, all protected routes will redirect to `/403`. Ensure AuthController and token/me response include permission claims (already the case with `RolePermissionMatrix.GetPermissionsForRoles`).
- **PaymentSecurityMiddleware:** Tokens must include permission claims (e.g. from TokenClaimsService). Legacy clients or tokens without permission claims will receive 403 on payment security endpoints.
- **Migration:** `DropAdministratorRole` deletes the legacy admin role from AspNetRoles. Run after `CanonicalizeLegacyRoleNames`. No Down implementation (intentional).

---

## Test Impact

- **PaymentSecurityMiddlewareTests:** Updated to permission-based; all 6 tests pass.
- **Other auth tests:** No changes to PermissionAuthorizationHandlerTests, RolePermissionMatrixTests, EndpointAuthorizationRepresentativeTests; still pass.
- **Manual:** After deploy, verify (1) Admin login in FE-Admin and access to permitted routes, (2) user without permissions gets 403, (3) payment refund/cancel with token that has payment permissions succeeds.

---

## Rollback

1. **Backend:** Revert middleware to role-based allow-list; revert migration (add Administrator role back in DB if needed); revert UserSeedData/RoleSeedData/AuthController/TokenClaimsService/Roles changes.
2. **FE-Admin:** Revert PermissionRouteGuard to fail-open (allow when permissions empty) and remove PermissionRouteGuard wrapper from layout.
3. **Frontend (POS):** Revert ROLES usage and permission-first shortcuts in usePermission and RoleGuard.
4. **Migrations:** If `DropAdministratorRole` was applied, re-create legacy role in AspNetRoles and reassign users manually if required.

---

## Checklist

- [x] Legacy admin role removed from active use; migration drops it from AspNetRoles.
- [x] Single admin role is Admin (Roles.Admin).
- [x] Permission-first: middleware and FE-Admin guard use permissions.
- [x] FE-Admin fail-closed when permissions missing.
- [x] No hard-coded role strings in touched backend/FE code; Roles.* / ROLES used.
- [x] PaymentSecurityMiddlewareTests updated and passing.
- [x] Build succeeds; authorization-related tests pass.
