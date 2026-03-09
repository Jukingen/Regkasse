# Authorization Inventory — Current State

**Canonical reference:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md).  
**Endpoint mapping:** [ENDPOINT_PERMISSION_MAP_FINAL.md](ENDPOINT_PERMISSION_MAP_FINAL.md).

---

## Current model (permission-first)

- **Roles:** SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. **Administrator is not a role** (removed); use **Admin** only.
- **Policies:** No legacy role policies are registered. Controllers use `[HasPermission(AppPermissions.X)]` only. Each permission in `PermissionCatalog.All` has a policy `"Permission:" + permission`; evaluation uses `PermissionAuthorizationHandler` and `RolePermissionMatrix`.
- **Source files:** `backend/Authorization/AuthorizationExtensions.cs` (AddAppAuthorization, AddPermissionPolicies only), `Roles.cs`, `AppPermissions.cs`, `PermissionCatalog.cs`, `RolePermissionMatrix.cs`, `HasPermissionAttribute.cs`.

## PaymentSecurityMiddleware

- **Enforcement:** Permission-first, path-based. No role allow-list. Required permission per path (e.g. `/api/payment/refund` → `refund.create`, `/api/payment/cancel` → `payment.cancel`, `/api/payment/update-status` → `payment.take`). JWT permission claims are checked; missing or insufficient permission → 403.
- **Admin access:** Admin (and SuperAdmin, Manager, Cashier) receive permissions from `RolePermissionMatrix` at login; refund/cancel/update-status allowed when the corresponding permission is present. Waiter has no `refund.create` → 403 on refund path.

## CartController force-cleanup

- **Protection:** `[HasPermission(AppPermissions.CartManage)]`; Waiter has only CartView and is denied.

## Hard-coded role usage

- Use `Roles.*` constants only. No "Administrator", "BranchManager", or "Auditor" in authorization code. Display labels (e.g. "Kasiyer") are UI-only.

---

For full role matrix, permission list, and FE strategy, see **architecture/FINAL_AUTHORIZATION_MODEL.md**.
