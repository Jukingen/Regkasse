# Final Authorization Model

**Audience:** Test team, FE team, backend developers.  
**Canonical code:** `backend/Authorization/Roles.cs`, `AppPermissions.cs`, `RolePermissionMatrix.cs`, `AuthorizationExtensions.cs`.

This document is the single source of truth for the **current** authorization state after the permission-first hardening. No plan/state mix; Administrator is not a role.

---

## 1. Final role model

| Role | Description |
|------|-------------|
| **SuperAdmin** | Full system access including system-critical actions. Only role with `system.critical`, `tse.diagnostics`, `audit.cleanup`, `inventory.delete`. |
| **Admin** | Backoffice and operational management. All permissions **except** SuperAdmin-only: `system.critical`, `tse.diagnostics`, `audit.cleanup`, `inventory.delete`. **Single admin role** (no Administrator). |
| **Manager** | Operations, catalog, inventory, reports, audit export. No user.manage, no settings.manage, no audit.cleanup, no inventory.delete, no tse.diagnostics. |
| **Cashier** | POS sales, cart, payment, receipts, shift, inventory view. No users, no report.view, no settings; has tse.sign. |
| **Waiter** | Orders, tables, cart.view, payment.take. No cart.manage (no force-cleanup), no payment.cancel, no refund.create, no tse.sign. |
| **Kitchen** | Kitchen display, order status. |
| **ReportViewer** | Reports, audit view, settings view. Read-only. |
| **Accountant** | Reports, audit view, FinanzOnline view. No settings.manage. |

**Source of truth:** `backend/Authorization/Roles.cs` — constants and `Roles.Canonical`.

---

## 2. Final permission catalog

Permissions follow `resource.action`. Full list in `backend/Authorization/AppPermissions.cs` and `PermissionCatalog.All`.

| Group | Permissions |
|-------|-------------|
| **User / Role** | user.view, user.manage, role.view, role.manage |
| **Catalog** | product.view, product.manage, category.view, category.manage, modifier.view, modifier.manage |
| **Order / Table / Cart / Sale** | order.view, order.create, order.update, order.cancel, table.view, table.manage, cart.view, cart.manage, sale.view, sale.create, sale.cancel |
| **Payment / Refund** | payment.view, payment.take, payment.cancel, refund.create, discount.apply |
| **CashRegister / Shift** | cashregister.view, cashregister.manage, cashdrawer.open, cashdrawer.close, shift.view, shift.open, shift.close, shift.manage |
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

## 3. Role–permission matrix summary

| Role | user | catalog | cart | payment | inventory | report | settings | TSE | SuperAdmin-only |
|------|------|---------|------|---------|-----------|--------|----------|-----|-----------------|
| **SuperAdmin** | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all | **✓** |
| **Admin** | ✓ all | ✓ all | ✓ all | ✓ all | ✓ all* | ✓ all | ✓ all | sign | ✗ |
| **Manager** | view | ✓ all | ✓ all | ✓ all | view+manage | ✓ | view | sign | ✗ |
| **Cashier** | ✗ | view | ✓ manage | ✓ take/cancel/refund | view | ✗ | ✗ | sign | ✗ |
| **Waiter** | ✗ | view | **view only** | take only | ✗ | ✗ | ✗ | ✗ | ✗ |
| **ReportViewer** | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ view+export | view | ✗ | ✗ |
| **Accountant** | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ view+export | ✗ | ✗ | ✗ |

\* Admin has inventory.view, inventory.manage, inventory.adjust; **not** inventory.delete.

**SuperAdmin-only permissions:** `system.critical`, `tse.diagnostics`, `audit.cleanup`, `inventory.delete`.

**Source of truth:** `backend/Authorization/RolePermissionMatrix.cs`.

---

## 4. Endpoint protection strategy

- **Backend:** Controllers use `[Authorize]` plus `[HasPermission(AppPermissions.X)]` per action or class. **No legacy role policies** (AdminUsers, UsersView, PosSales, etc.) are registered.
- **Flow:** JWT contains `role` and `permission` claims (permissions from `RolePermissionMatrix` at login). `PermissionAuthorizationHandler` allows access if the user has the required permission (from claim or derived from role).
- **Payment paths:** `PaymentSecurityMiddleware` is permission-first and path-based: e.g. `/api/payment/refund` → `refund.create`, `/api/payment/cancel` → `payment.cancel`. No role allow-list; JWT permission claims are checked. Fail-closed: missing permission → 403.
- **Mapping:** See `docs/ENDPOINT_PERMISSION_MAP_FINAL.md` for route → permission table.

---

## 5. FE authorization strategy

- **Source:** Same role and permission names as backend. FE reads `user.role` and `user.permissions` from API (e.g. `/api/Auth/me`).
- **Prefer permissions:** Gate UI by permission when available; use role only as fallback when permissions are empty (e.g. migration/dev).
- **Admin (frontend-admin):** Menu and route guards use permission map (`routePermissions.ts`, `menuPermissions.ts`). `PermissionRouteGuard` is **fail-closed**: no/insufficient permission → redirect to `/403`. `AdminOnlyGate`: permission-first (e.g. user.manage, settings.manage) with Admin/SuperAdmin role fallback.
- **POS (frontend):** `PermissionHelper`, `usePermission`, `RoleBasedNavigation`; screen access by permission when `user.permissions` present, else role-based fallback.
- **No Administrator:** All FE checks use `Admin` or `SuperAdmin`; no "Administrator" string.

**References:** `frontend-admin/src/shared/auth/permissions.ts`, `routePermissions.ts`, `usersPolicy.ts`; `frontend/shared/utils/PermissionHelper.ts`, `hooks/usePermission.ts`.

---

## 6. Removed legacy items

| Item | Status |
|------|--------|
| **Administrator** role | Removed. Use **Admin** only. |
| Administrator in Roles.cs / seed / matrix | Removed. |
| Administrator → Admin canonicalization | Removed from RoleCanonicalization (trim/empty only). |
| **Legacy role policies** (AdminUsers, UsersView, PosSales, BackofficeSettings, etc.) | Not registered. All protection via `[HasPermission]`. |
| BranchManager, Auditor in policy RequireRole | Not in canonical roles; not used in current policies. |
| PaymentSecurityMiddleware role allow-list | Replaced by path → permission and JWT permission claims. |
| CanonicalizeLegacyRoleNames.sql | Deprecated / no-op. |

---

## 7. Test / readiness checklist

- **Backend unit:** RolePermissionMatrixTests (Administrator absent; SuperAdmin vs Admin for system.critical, tse.diagnostics, audit.cleanup, inventory.delete). PermissionAuthorizationHandlerTests. UserManagementAuthorizationPolicyTests. PaymentSecurityMiddlewareTests (path/permission). EndpointAuthorizationRepresentativeTests. CartControllerForceCleanupAuthorizationTests.
- **Frontend-admin:** PermissionRouteGuard (fail-closed, redirect to /403 when no/insufficient permission). AdminOnlyGate (permission-first, deny non-admin).
- **Integration / smoke:** Login as Admin → token has role + permissions; access users, payment/refund as allowed. Waiter → 403 on refund. SuperAdmin → allowed on system-critical; Admin → 403 on inventory.delete / audit.cleanup / tse.diagnostics.
- **Go/no-go:** All auth-related tests green; smoke checklist passed; no Administrator in API or matrix.

**Reference:** `docs/AUTHORIZATION_HARDENING_TEST_MATRIX.md`.
