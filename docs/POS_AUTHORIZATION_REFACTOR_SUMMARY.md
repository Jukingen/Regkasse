# POS Authorization Refactor — Summary & Verification

**Current model:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). Permission-first; single admin role **Admin** (no Administrator).

**Summary:** Role cleanup and permission migration are complete. Controllers use `[HasPermission(AppPermissions.X)]` only; legacy role policies are not registered. FE route guard and admin gate are fail-closed.

---

## 1) Implemented state

| Target | Implementation |
|--------|----------------|
| Single admin role | `Roles.cs`: Admin, SuperAdmin only; no Administrator. Seed and token use `Roles.*`. |
| Token / permissions | `TokenClaimsService`: role + permissions from `RolePermissionMatrix`; JWT has permission claims. |
| PaymentSecurityMiddleware | Permission-first, path-based; no role list. Refund → refund.create; cancel → payment.cancel; update-status → payment.take. Admin/Cashier/Manager have these permissions; Waiter does not (403 on refund). |
| Controller protection | All use `[HasPermission(AppPermissions.X)]`; no legacy policy names. |
| Cart force-cleanup | `[HasPermission(AppPermissions.CartManage)]`; Waiter denied. |
| FE | PermissionRouteGuard fail-closed; AdminOnlyGate permission-first with Admin/SuperAdmin fallback; menu/route by permission. |

---

## 2) Key files

| File | Role |
|------|------|
| `AuthorizationExtensions.cs` | Permission policies only; no legacy policies. |
| `PaymentSecurityMiddleware.cs` | Path → required permission; JWT permission claims. |
| `RolePermissionMatrix.cs` | SuperAdmin full set; Admin all except system.critical, tse.diagnostics, audit.cleanup, inventory.delete. |
| `CartController.cs` | CartManage for force-cleanup. |
| `AuthController.cs` | Login/me return role + permissions; default role `Roles.Cashier`. |
| `RoleSeedData.cs` | Only `Roles.*`; no Administrator. |

---

## 3) Test / verification

See [AUTHORIZATION_HARDENING_TEST_MATRIX.md](AUTHORIZATION_HARDENING_TEST_MATRIX.md) for test filters, go/no-go criteria, and manual smoke checklist.

**Expected:** All backend auth tests and frontend-admin guard tests pass. No Administrator in matrix or API.
