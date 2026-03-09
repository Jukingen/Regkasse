# Authorization Refactor — Verification & PR Review

**Current model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). **Administrator** is removed; single admin role is **Admin**. Permission-first: controllers use `[HasPermission(AppPermissions.X)]`; legacy role policies are not registered.

---

## 1) Key files (current state)

| File | Description |
|------|-------------|
| `AuthorizationExtensions.cs` | Registers permission policies only (AddPermissionPolicies). No legacy role policies. |
| `PaymentSecurityMiddleware.cs` | Permission-first, path-based: required permission per path; JWT permission claims checked. No role allow-list. |
| `CartController.cs` | Force-cleanup: `[HasPermission(AppPermissions.CartManage)]`. |
| `AuthController.cs` | Default role `Roles.Cashier`; login/me return role + permissions from RolePermissionMatrix. |
| `RoleSeedData.cs` | Seeds only `Roles.*` (Admin, SuperAdmin, …); no Administrator. |
| `Roles.cs` | Canonical roles only; no Administrator constant. |
| `RolePermissionMatrix.cs` | SuperAdmin: full set; Admin: all except system.critical, tse.diagnostics, audit.cleanup, inventory.delete. |

---

## 2) Risk checks

| Risk | Result | Evidence |
|------|--------|----------|
| **Admin lockout?** | No. Admin has payment permissions from matrix; middleware checks permission claims. | PaymentSecurityMiddlewareTests (Admin matrix permissions allow refund). |
| **Token role/permissions?** | Correct. TokenClaimsService + RolePermissionMatrix; JWT has role and permission claims. | AuthController; RolePermissionMatrixTests. |
| **PaymentSecurityMiddleware Admin?** | Allowed. Path-based permission; Admin has refund.create, payment.take, payment.cancel. | PaymentSecurityMiddlewareTests. |
| **Seed?** | Only Roles.*; single admin role Admin. | RoleSeedData.cs. |

---

## 3) Test files

See [AUTHORIZATION_HARDENING_TEST_MATRIX.md](AUTHORIZATION_HARDENING_TEST_MATRIX.md). Key: RolePermissionMatrixTests, PermissionAuthorizationHandlerTests, UserManagementAuthorizationPolicyTests, EndpointAuthorizationRepresentativeTests, PaymentSecurityMiddlewareTests.

---

## 4) Smoke checklist

- [ ] Login as Admin → token has role "Admin" and permission claims.
- [ ] Same token → GET users, POST payment/refund → not 403 (where permission present).
- [ ] Waiter token → POST payment/refund → 403.
- [ ] No "Administrator" in API responses or role lists.
