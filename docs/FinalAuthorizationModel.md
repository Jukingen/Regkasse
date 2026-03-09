# Final Authorization Model — Single Source of Truth

**→ Current canonical doc:** [architecture/FINAL_AUTHORIZATION_MODEL.md](architecture/FINAL_AUTHORIZATION_MODEL.md). This file is kept for backward links; the architecture doc is the single source of truth.

**Audience:** Test team, FE team, backend developers.  
**Canonical code:** `backend/Authorization/Roles.cs`, `AppPermissions.cs`, `RolePermissionMatrix.cs`, `AuthorizationExtensions.cs`.

---

## 1. Final role model

| Role | Description | Notes |
|------|-------------|--------|
| **SuperAdmin** | Full system access including system-critical actions | Only role with `system.critical` (permanent delete, backfill). |
| **Admin** | Backoffice and operational management | All permissions except `system.critical`. Single admin role (no "Administrator"). |
| **Manager** | Operations, catalog, inventory, reports, audit export | No user manage, no settings manage, no audit cleanup, no inventory delete, no TSE diagnostics. |
| **Cashier** | POS sales, cart, payment, receipts, shift, inventory view | No users, no reports, no settings; has TSE sign. |
| **Waiter** | Orders, tables, cart view, payment take | No cart manage (no force-cleanup), no payment cancel/refund, no TSE sign. |
| **Kitchen** | Kitchen display, order status | Optional. |
| **ReportViewer** | Reports, audit view, settings view | Read-only. |
| **Accountant** | Reports, audit view, FinanzOnline view | No settings manage. |

**Removed:** Administrator (replaced by Admin). BranchManager, Auditor (not in canonical list; use Manager / ReportViewer as needed).

**Source of truth:** `backend/Authorization/Roles.cs` — constants and `Roles.Canonical` list.

---

## 2. Final permission catalog

Permissions follow `resource.action`. Full list in `backend/Authorization/AppPermissions.cs` and `PermissionCatalog.All`.

| Group | Permissions |
|-------|-------------|
| **User / Role** | user.view, user.manage, role.view, role.manage |
| **Catalog** | product.view, product.manage, category.view, category.manage, modifier.view, modifier.manage |
| **Order / Table / Cart / Sale** | order.view, order.create, order.update, order.cancel, table.view, table.manage, cart.view, cart.manage, sale.view, sale.create, sale.cancel |
| **Payment / Refund** | payment.view, payment.take, payment.cancel, refund.create, discount.apply |
| **CashRegister / Shift** | cashregister.view, cashregister.manage, cashdrawer.open, cashdrawer.close, shift.view, shift.open, shift.close |
| **Inventory / Customer** | inventory.view, inventory.manage, inventory.adjust, inventory.delete, customer.view, customer.manage |
| **Invoice / CreditNote** | invoice.view, invoice.manage, invoice.export, creditnote.create |
| **Settings / Localization / Receipt** | settings.view, settings.manage, localization.view, localization.manage, receipttemplate.view, receipttemplate.manage |
| **Audit / Report** | audit.view, audit.export, audit.cleanup, report.view, report.export |
| **FinanzOnline** | finanzonline.view, finanzonline.manage, finanzonline.submit |
| **Kitchen** | kitchen.view, kitchen.update |
| **TSE** | tse.sign, tse.diagnostics |
| **System** | system.critical |
| **Convenience** | price.override, receipt.reprint |

---

## 3. Role → permission matrix summary

| Role | user | catalog | cart | payment | inventory | invoice | report | settings | TSE | system.critical |
|------|------|---------|------|---------|-----------|---------|--------|----------|-----|-----------------|
| **SuperAdmin** | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | **✓** |
| **Admin** | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✗ |
| **Manager** | view | ✓ all | ✓ all | ✓ all | view+manage | ✓ view+manage+export | ✓ | view | sign | ✗ |
| **Cashier** | ✗ | view | ✓ manage | ✓ take/cancel/refund | view | view | ✗ | ✗ | sign | ✗ |
| **Waiter** | ✗ | view | **view only** | take only | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Kitchen** | ✗ | view | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **ReportViewer** | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ view+export | view | ✗ | ✗ |
| **Accountant** | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ view+export | ✗ | ✗ | ✗ |

- **SuperAdmin-only permissions:** system.critical, tse.diagnostics, audit.cleanup, inventory.delete. Admin does **not** have these four.

**Source of truth:** `backend/Authorization/RolePermissionMatrix.cs`.

---

## 4. Endpoint → permission strategy

- **Backend:** Controllers use `[Authorize]` plus `[HasPermission(AppPermissions.X)]` per action (or class). No legacy role policies (e.g. AdminUsers, UsersView, PosSales) are registered.
- **Flow:** JWT contains `role` and `permission` claims. `PermissionAuthorizationHandler` allows access if the user has the required permission (from claim or derived from role via `RolePermissionMatrix`).
- **Mapping:** See `docs/ENDPOINT_PERMISSION_MAP_FINAL.md` for the full table (route, method, required permission, allowed roles).

Summary by area:

| Area | Read permission | Write / action permission |
|------|------------------|---------------------------|
| Users | user.view | user.manage |
| Catalog | product.view, category.view, modifier.view | product.manage, category.manage, modifier.manage |
| Inventory | inventory.view | inventory.manage, inventory.adjust; inventory.delete (Admin+) |
| Reports | report.view | report.export |
| Settings | settings.view | settings.manage |
| POS Cart | — | cart.manage (Waiter has cart.view only) |
| Payment | — | payment.take, payment.cancel, refund.create |
| TSE | — | tse.sign; tse.diagnostics (Admin+) |
| System-critical | — | system.critical (SuperAdmin only) |

---

## 5. FE authorization strategy

- **Single source:** Same role and permission names as backend. FE reads `user.role` and `user.permissions` from API (e.g. `/api/Auth/me`).
- **Prefer permissions:** Where possible, gate UI by permission (e.g. `hasPermission('user.view')`) instead of role. Use role as fallback when permissions are not yet available.
- **Menu / routes:** Admin (frontend-admin): menu visibility and route guards use `usePermissions` + `isMenuItemAllowed` / route permission map. POS (frontend): `PermissionHelper`, `usePermission`, `RoleBasedNavigation` aligned with backend matrix.
- **Guards:** `AdminOnlyGate` = Admin or SuperAdmin. User management: canView/canCreate/canEdit from `useUsersPolicy` (permission-first with role fallback).
- **No Administrator:** All FE checks use `Admin` or `SuperAdmin`; no references to "Administrator".

**References:** `frontend-admin/src/shared/auth/permissions.ts`, `menuPermissions.ts`, `routePermissions.ts`, `usersPolicy.ts`; `frontend/shared/utils/PermissionHelper.ts`, `hooks/usePermission.ts`.

---

## 6. Removed legacy items

| Item | Status |
|------|--------|
| **Administrator** role | Removed. Use **Admin** only. |
| **Administrator** in Roles.cs | Removed. |
| **Administrator** seed | Removed from RoleSeedData. |
| **Administrator** in RolePermissionMatrix | Removed. |
| **Administrator → Admin** canonicalization | Removed from RoleCanonicalization (only trim/empty handling remains). |
| **Legacy role policies** (AdminUsers, UsersView, UsersManage, PosSales, etc.) | Not registered. Controllers use `[HasPermission(...)]` only. |
| **BranchManager, Auditor** in policy RequireRole | Removed (were in old policy list; not in canonical roles). |
| **CanonicalizeLegacyRoleNames.sql** | Deprecated / no-op. |

---

## 7. Testing strategy

- **Unit:** Permission policy evaluation (`PermissionAuthorizationHandlerTests`), role–permission matrix (`RolePermissionMatrixTests`), user view/manage by role (`UserManagementAuthorizationPolicyTests`). Payment middleware: Admin/Cashier/Manager allowed, Waiter denied (`PaymentSecurityMiddlewareTests`). Cart force-cleanup requires CartManage (`CartControllerForceCleanupAuthorizationTests`). Representative endpoint auth (`EndpointAuthorizationRepresentativeTests`).
- **Integration / API:** Use JWT with role (and permissions) and assert 200 vs 403 on key endpoints (users, inventory delete, reports, settings, TSE diagnostics, system-critical).
- **FE:** Users page: canView false shows no-permission alert. Menu and route guards follow permission/role config.
- **Smoke / manual:** Per-role login and check access (see `docs/AUTH_AUTHORIZATION_TEST_MATRIX.md` for checklist and go/no-go).

**Reference:** `docs/AUTH_AUTHORIZATION_TEST_MATRIX.md`.

---

## 8. Demo rollout notes

- **Seeds:** Only canonical roles (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant) are seeded. No Administrator.
- **Existing DBs:** If any user still has role "Administrator", treat as one-time data fix (e.g. update to "Admin") outside this doc. Application code does not reference Administrator.
- **JWT:** Token includes `role` and `permission` claims. FE and BE use the same names.
- **Go-live check:** All auth/permission unit tests green; smoke checklist passed for Admin, Manager, Cashier, Waiter, ReportViewer; no 200 on forbidden endpoints (e.g. Cashier on Users, Waiter on cart force-cleanup, Admin on backfill).

---

**Doc version:** 1.0 — Post authorization refactor (permission-first, Administrator removed).  
**For detailed endpoint list:** see `docs/ENDPOINT_PERMISSION_MAP_FINAL.md`.  
**For test details:** see `docs/AUTH_AUTHORIZATION_TEST_MATRIX.md`.
