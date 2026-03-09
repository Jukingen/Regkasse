# Authorization Hardening – Test Matrix & Go/No-Go

## 1. Updated test matrix

| Area | Test file | Coverage |
|------|-----------|----------|
| **Administrator absent** | RolePermissionMatrixTests | `Administrator` role not in matrix; GetPermissionsForRoles("Administrator") returns empty. |
| **Token / matrix** | RolePermissionMatrixTests | SuperAdmin/Admin separation (SystemCritical, TseDiagnostics, AuditCleanup, InventoryDelete); canonical roles only. |
| **PaymentSecurityMiddleware** | PaymentSecurityMiddlewareTests | Path-based: Refund→RefundCreate, Cancel→PaymentCancel, Update-status→PaymentTake; Admin/Cashier/Manager matrix perms allow refund; Waiter matrix perms → 403 on refund; no claims / unauthenticated → 403. |
| **Settings** | EndpointAuthorizationRepresentativeTests | SettingsView: Manager allowed, Cashier denied; SettingsManage: Admin allowed, Manager denied. |
| **CashRegister** | EndpointAuthorizationRepresentativeTests | CashRegisterView, CashdrawerOpen: Cashier allowed. |
| **Users** | UserManagementAuthorizationPolicyTests | UserView: Admin, SuperAdmin, Manager allowed; Cashier, Waiter denied. UserManage: Admin, SuperAdmin allowed; Manager, Cashier denied. |
| **Inventory** | EndpointAuthorizationRepresentativeTests, RolePermissionMatrixTests | InventoryView: Cashier allowed; InventoryDelete: SuperAdmin allowed, Admin and Manager denied. |
| **Reports** | EndpointAuthorizationRepresentativeTests | ReportView, ReportExport: ReportViewer allowed; Cashier denied. |
| **SystemCritical** | PermissionAuthorizationHandlerTests, EndpointAuthorizationRepresentativeTests | SuperAdmin allowed; Admin denied. |
| **Permission handler (SuperAdmin-only)** | PermissionAuthorizationHandlerTests | AuditCleanup, InventoryDelete, TseDiagnostics: SuperAdmin allowed; Admin denied. |
| **FE route guard** | PermissionRouteGuard.test.tsx | No permissions → redirect /403; insufficient permission → /403; has required permission → render children. |
| **FE admin gate** | AdminOnlyGate.test.tsx | No admin permission/role → redirect /403; Admin role or user.manage/settings.manage → render children. |

---

## 2. Added tests (this pass)

### Backend
- **RolePermissionMatrixTests**: `RoleHasPermission_AdministratorRole_NotInMatrix_ReturnsFalse`, `GetPermissionsForRoles_Administrator_ReturnsEmpty`.
- **PaymentSecurityMiddlewareTests**: `InvokeAsync_RefundEndpoint_WithAdminMatrixPermissions_AllowsRequest`, `WithCashierMatrixPermissions_AllowsRequest`, `WithManagerMatrixPermissions_AllowsRequest`, `WithWaiterMatrixPermissions_Returns403`.
- **PermissionAuthorizationHandlerTests**: AuditCleanup/InventoryDelete/TseDiagnostics SuperAdmin allowed + Admin denied; renamed/added so no test asserts Admin has these.
- **EndpointAuthorizationRepresentativeTests**: `Inventory_InventoryDelete_Admin_Denied`, `Inventory_InventoryDelete_SuperAdmin_Allowed`, `Settings_SettingsView_Cashier_Denied`, `Settings_SettingsView_Manager_Allowed`, `Reports_ReportExport_ReportViewer_Allowed`, `Reports_ReportExport_Cashier_Denied`, `CashRegister_CashRegisterView_Cashier_Allowed`, `CashRegister_CashdrawerOpen_Cashier_Allowed`.

### Frontend-admin
- **shared/auth/__tests__/PermissionRouteGuard.test.tsx**: Fail-closed when no permissions; insufficient permission → /403; has required permission → render.
- **shared/auth/__tests__/AdminOnlyGate.test.tsx**: Non-admin → /403; Admin role or admin permission → render.

---

## 3. Missing tests (recommended)

| Priority | Missing test | Notes |
|----------|--------------|--------|
| High | Settings write (settings.manage) – Manager denied on write endpoint | Representative controller test or integration: PUT/POST settings with Manager token → 403. |
| High | CashRegister manage/open/close – Waiter denied for manage | Endpoint or handler: CashRegisterManage / CashdrawerClose for Waiter → deny. |
| Medium | Token claims: login/me never return role "Administrator" | Integration: AuthController login/me response role in canonical set only. |
| Medium | Reports export – ReportView-only role denied for export | ReportViewer has export; add test that a role with only ReportView (if any) is denied for ReportExport. |
| Low | Menu visibility: menu item hidden when permission missing | FE: assert menu items filtered by permission (e.g. mock useAuth with limited permissions). |
| Low | PermissionGate / button visibility | FE: button hidden or disabled when permission missing. |
| Low | Integration: SettingsController GET with Cashier token → 403 | Full HTTP integration test. |
| Low | Integration: Inventory DELETE with Admin token → 403 | Full HTTP integration test. |

---

## 4. Manual smoke checklist

- [ ] Login as **Admin**: can open Users, Settings, Products; cannot open TSE diagnostics (if UI exists); cannot run audit cleanup; cannot delete inventory (if button exists).
- [ ] Login as **SuperAdmin**: can open same as Admin; can run TSE diagnostics; can run audit cleanup; can delete inventory.
- [ ] Login as **Manager**: can open Users (list), Reports, Audit; cannot create/edit/deactivate users; cannot open Settings (or only view if allowed); cannot run audit cleanup.
- [ ] Login as **Cashier**: can use POS, payment, refund; cannot open Users, Settings, Reports; can open Products/Inventory (view).
- [ ] Login as **Waiter**: can use orders, tables, sales view; cannot process refund (or refund UI hidden); cannot manage cart (if applicable).
- [ ] **403**: Accessing protected route without permission redirects to /403 (or shows 403 page).
- [ ] **No Administrator**: Role dropdown / API responses do not show "Administrator".
- [ ] **Payment**: Admin and Cashier can complete payment/refund flows; Waiter cannot access refund endpoint (403 or UI hidden).

---

## 5. Go / no-go criteria

**Go (test phase / release)**  
- All backend authorization unit tests pass (RolePermissionMatrix, PermissionAuthorizationHandler, UserManagementAuthorizationPolicy, EndpointAuthorizationRepresentative, PaymentSecurityMiddleware).
- Frontend-admin PermissionRouteGuard and AdminOnlyGate tests pass.
- No test expects role "Administrator" or Admin to have SystemCritical, TseDiagnostics, AuditCleanup, or InventoryDelete.
- Manual smoke: Admin vs SuperAdmin separation (inventory delete, audit cleanup, TSE diagnostics) and Waiter refund deny are verified.

**No-go**  
- Any of the above test suites failing.
- Administrator still present as selectable or returned role in API.
- PaymentSecurityMiddleware allows Waiter on refund path when using matrix permissions.
- PermissionRouteGuard or AdminOnlyGate allows access without required permission/role (fail-open).
- Critical regression: Cashier/Manager/Admin cannot complete normal payment/refund where required.

---

## 6. Representative integration / smoke test list

| # | Scenario | Expected |
|---|----------|----------|
| 1 | POST /api/auth/login as Admin → response.role | "Admin" (not Administrator) |
| 2 | GET /api/auth/me as Admin → permissions | Contains settings.manage, user.manage; does not contain system.critical |
| 3 | GET /api/auth/me as SuperAdmin → permissions | Contains system.critical, audit.cleanup, inventory.delete, tse.diagnostics |
| 4 | POST /api/payment/refund with Cashier token | 200 (or business error only) |
| 5 | POST /api/payment/refund with Waiter token | 403 |
| 6 | GET /api/Settings/* with Cashier token | 403 (if endpoint requires settings.view) |
| 7 | DELETE /api/Inventory/{id} with Admin token | 403 |
| 8 | DELETE /api/Inventory/{id} with SuperAdmin token | 200 or 404 |
| 9 | Audit cleanup endpoint with Admin token | 403 |
| 10 | TSE diagnostics endpoint with Cashier token | 403 |
