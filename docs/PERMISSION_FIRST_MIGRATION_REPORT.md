# Permission-First Authorization Migration Report

**Date:** 2025-03  
**Scope:** Backend endpoint authorization migrated from role-based policies to `[HasPermission(AppPermissions.X)]`. Legacy role policies removed from registration.

---

## 1) Legacy policies removed

The following role-based policies are **no longer registered** in `AuthorizationExtensions.AddLegacyRolePolicies`:

| Legacy policy | Replaced by permission(s) |
|---------------|---------------------------|
| AdminUsers | user.manage |
| UsersView | user.view |
| UsersManage | user.manage |
| BackofficeManagement | report.view / settings.view (per controller) |
| BackofficeSettings | settings.view, settings.manage, localization.manage, receipttemplate.manage |
| PosSales | cart.manage, payment.take, sale.create (per controller) |
| PosTableOrder | order.create, table.manage |
| PosCatalogRead | product.view, category.view, modifier.view |
| CatalogManage | product.manage, category.manage, modifier.manage |
| InventoryManage | inventory.manage |
| InventoryDelete | inventory.delete |
| AuditView / AuditViewWithCashier | audit.view |
| AuditAdmin | audit.export (audit.cleanup already used on cleanup action) |
| PosTse | tse.sign |
| PosTseDiagnostics | tse.diagnostics |
| SystemCritical | system.critical |
| CashRegisterManage | cashregister.manage |

---

## 2) New permissions added

| Permission | Constant | Description |
|------------|----------|-------------|
| inventory.delete | AppPermissions.InventoryDelete | Delete inventory (was AdminOnly). |
| tse.sign | AppPermissions.TseSign | TSE signing (receipts, daily closing). |
| tse.diagnostics | AppPermissions.TseDiagnostics | TSE diagnostics (AdminOnly). |
| system.critical | AppPermissions.SystemCritical | Permanent delete, high-risk operations (AdminOnly). |

All added to `AppPermissions.cs`, `PermissionCatalog.All`, and (where applicable) `RolePermissionMatrix`:
- **InventoryDelete:** Only via `PermissionCatalog.All` (Admin/SuperAdmin have all).
- **TseSign:** Manager, Cashier (and Admin/SuperAdmin via all).
- **TseDiagnostics, SystemCritical:** Only Admin/SuperAdmin (via all).

---

## 3) Controller / action migration

| Controller | Previous | Now |
|------------|----------|-----|
| **AdminUsersController** | [Authorize(Policy = "AdminUsers")] | [Authorize] + [HasPermission(UserManage)] |
| **UserManagementController** | UsersView / UsersManage | [HasPermission(UserView)] / [HasPermission(UserManage)] |
| **AuditLogController** | UsersView, AuditView, AuditViewWithCashier, AuditAdmin | UserView, AuditView, AuditExport |
| **CategoriesController** | PosCatalogRead, CatalogManage | CategoryView, CategoryManage |
| **ModifierGroupsController** | PosCatalogRead, CatalogManage | ModifierView, ModifierManage |
| **ProductController** | PosCatalogRead | ProductView |
| **AdminProductsController** | CatalogManage | ProductManage |
| **InventoryController** | InventoryManage, InventoryDelete | InventoryManage, InventoryDelete |
| **CompanySettingsController** | BackofficeSettings, BackofficeManagement | SettingsManage, SettingsView |
| **FinanzOnlineController** | BackofficeSettings | SettingsView (class); actions already HasPermission(FinanzOnline*) |
| **SettingsController** | BackofficeSettings | SettingsManage |
| **LocalizationController** | BackofficeSettings | LocalizationManage |
| **MultilingualReceiptController** | BackofficeSettings | ReceiptTemplateManage |
| **CartController** | PosSales | CartManage |
| **PaymentController** | PosSales, PosTse, PosTseDiagnostics | PaymentTake, TseSign, TseDiagnostics |
| **OrdersController** | PosTableOrder | OrderCreate |
| **TableController** | PosTableOrder | TableManage |
| **CustomerController** | PosTableOrder | OrderCreate |
| **ReceiptsController** | PosSales | SaleCreate |
| **TagesabschlussController** | PosTse | TseSign |
| **TseController** | PosTse | TseSign |
| **EntityController** (PermanentDelete) | SystemCritical | SystemCritical |
| **InvoiceController** (one action) | SystemCritical | SystemCritical |
| **CashRegisterController** | CashRegisterManage | CashRegisterManage |
| **ReportsController** | (already) HasPermission(ReportView) | unchanged |

---

## 4) Riskier migrations

- **TSE (tse.sign, tse.diagnostics):** Permission-first. tse.sign granted to Manager and Cashier (same as former PosSales for TSE). tse.diagnostics only Admin/SuperAdmin (same as PosTseDiagnostics). No behavior change intended.
- **SystemCritical (system.critical):** Permission-first. Only Admin/SuperAdmin (via “all”). Used for permanent delete (EntityController, InvoiceController). No behavior change.
- **InventoryDelete:** New permission; only in PermissionCatalog.All (Admin/SuperAdmin). Same as former InventoryDelete policy.
- **User/audit/settings:** UserView and UserManage map to former UsersView/UsersManage; Auditor/BranchManager are no longer in policy (legacy policies listed them). If BranchManager or Auditor must still have user view, they need user.view (and optionally user.manage) in RolePermissionMatrix. **Current matrix:** Manager has UserView; ReportViewer and Accountant do not have UserView. BranchManager and Auditor are not in RolePermissionMatrix. So after migration, only roles that have user.view in the matrix can access user endpoints (Manager, Admin, SuperAdmin). If you need BranchManager/Auditor to see users, add UserView to a role or introduce BranchManager/Auditor in the matrix with UserView.

---

## 5) Test requirements

- **Existing:** RolePermissionMatrixTests, UserManagementAuthorizationPolicyTests (build their own policies), PermissionAuthorizationHandlerTests, RoleCanonicalizationTests — all run and pass (36 tests).
- **Recommended:**
  - Add RolePermissionMatrix tests for new permissions: InventoryDelete (Admin has, Cashier does not), TseSign (Cashier/Manager have), TseDiagnostics and SystemCritical (only Admin/SuperAdmin).
  - PermissionAuthorizationHandlerTests: add cases for tse.sign, tse.diagnostics, system.critical, inventory.delete where relevant.
  - Smoke: login as Cashier, Admin, Manager; call Cart, Payment, TSE, Inventory, User, Settings endpoints and assert 200/403 as expected.
- **Integration:** Any test that assumed legacy policy names (e.g. "UsersView") for authorization will no longer see those policies in the app; they should use permission-based checks or build a test auth scheme with the same permissions.

---

## 6) RolePermissionMatrix changes

- **Manager, Cashier:** TseSign added (TSE operations).
- **Admin, SuperAdmin:** No change (they have “all”, including InventoryDelete, TseSign, TseDiagnostics, SystemCritical).
- **Other roles:** Unchanged.

PermissionCatalog.All includes the four new permissions; AddPermissionPolicies registers one policy per permission as before.
