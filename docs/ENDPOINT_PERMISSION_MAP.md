# Endpoint → Permission Map (Authorization Inventory + Swagger)

**Sources:** `backend/swagger.json`, `AuthorizationExtensions.cs`, controller `[Authorize]` / `[HasPermission]`, `AppPermissions.cs`, `RolePermissionMatrix.cs`, `PermissionCatalog.cs`.

**Purpose:** Classify current authorization per endpoint, map legacy role-policies to candidate permissions, and produce a permission migration backlog. Routes and endpoints are taken only from swagger and controller code—no invented routes.

---

## 1. Endpoint table (by category)

Below: **HTTP method**, **route** (from swagger), **controller/action** (inferred from route), **current policy / authorize usage**, **candidate permission**, **risk level**, **migration phase**.  
Where a controller has only class-level authorization, all its endpoints inherit that policy unless a method-level attribute overrides it.

---

### 1.1 Users

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| *various* | `/api/UserManagement/*` | UserManagementController | Class `[Authorize]`; methods: UsersView, UsersManage (see controller) | user.view, user.manage | Medium | 2 |
| GET | `/api/UserManagement` | UserManagementController / List/Get | UsersView | user.view | Low | 2 |
| GET | `/api/UserManagement/{id}` | UserManagementController / Get | UsersView | user.view | Low | 2 |
| POST | `/api/UserManagement` | UserManagementController / Create | UsersManage | user.manage | Medium | 2 |
| PUT | `/api/UserManagement/{id}` | UserManagementController / Update | UsersManage | user.manage | Medium | 2 |
| DELETE | `/api/UserManagement/{id}` | UserManagementController / Delete | UsersManage | user.manage | Medium | 2 |
| *other* | `/api/UserManagement/*` | UserManagementController | UsersView or UsersManage per method | user.view / user.manage | — | 2 |

**Needs inspection:** AdminUsersController is registered at `api/admin/users` (not listed in swagger paths used for this doc). If that route is exposed, treat as AdminUsers → candidate user.manage; otherwise document as code-only.

---

### 1.2 POS (Cart, Orders, Table, Customer, Payment, Receipts, Product read)

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/Cart/current`, `/api/pos/cart/current` | CartController | PosSales (class) | sale.create or payment.take | Low | 6 |
| GET | `/api/Cart/{cartId}`, `/api/pos/cart/{cartId}` | CartController | PosSales | sale.create / payment.take | Low | 6 |
| POST | `/api/Cart`, `/api/pos/cart` | CartController | PosSales | sale.create / payment.take | Low | 6 |
| POST | `/api/Cart/add-item`, `/api/pos/cart/add-item` | CartController | PosSales | sale.create | Low | 6 |
| PUT | `/api/Cart/items/{itemId}`, `/api/pos/cart/items/{itemId}` | CartController | PosSales | cart.manage | Low | 6 |
| DELETE | `/api/Cart/{cartId}/items/{itemId}`, etc. | CartController | PosSales | cart.manage | Low | 6 |
| POST | `/api/Cart/{cartId}/clear-items`, `clear`, `clear-all`, `reset-after-payment`, `complete` | CartController | PosSales | cart.manage / sale.create | Low | 6 |
| GET | `/api/Cart/history`, `table-orders-recovery` | CartController | PosSales | sale.view / cart.view | Low | 6 |
| POST | `/api/Cart/force-cleanup`, `/api/pos/cart/force-cleanup` | CartController / ForceCleanup | PosSales (class); internal role check Admin/Cashier | sale.create or cart.manage | Low | 6 |
| POST | `/api/Cart/items/{itemId}/increment`, `decrement` | CartController | PosSales | cart.manage | Low | 6 |
| GET | `/api/Orders`, `/api/Orders/{id}` | OrdersController | PosTableOrder (class) | order.view / order.create | Low | 7 |
| POST | `/api/Orders` | OrdersController | PosTableOrder | order.create | Low | 7 |
| PUT | `/api/Orders/{id}`, `/api/Orders/{id}/status` | OrdersController | PosTableOrder + HasPermission(OrderUpdate) on method | order.update | Low | 7 |
| DELETE / cancel | OrdersController | PosTableOrder + HasPermission(OrderCancel) where applied | order.cancel | Low | 7 |
| GET | `/api/Table` | TableController | PosTableOrder (class) | table.view / table.manage | Low | 7 |
| GET | `/api/Customer/*`, `/api/Customer/{id}`, `search`, etc. | CustomerController | PosTableOrder (class) | customer.view / customer.manage | Low | 7 |
| GET | `/api/Payment/methods`, `/api/pos/payment/methods` | PaymentController | PosSales (class) | payment.view / payment.take | Low | 6 |
| POST | `/api/Payment`, `/api/pos/payment` | PaymentController | PosSales + HasPermission(PaymentTake) on method | payment.take | Low | 6 |
| GET | `/api/Payment/{id}`, `customer/{id}`, `method/...`, `date-range`, `statistics` | PaymentController | PosSales | payment.view | Low | 6 |
| POST | `/api/Payment/{id}/cancel`, `/api/pos/payment/{id}/cancel` | PaymentController | PosSales + HasPermission(PaymentCancel) | payment.cancel | Low | 6 |
| POST | `/api/Payment/{id}/refund`, `/api/pos/payment/{id}/refund` | PaymentController | PosSales + HasPermission(RefundCreate) | refund.create | Low | 6 |
| GET | `/api/Payment/{id}/receipt`, `qr.png`, `qr.svg` | PaymentController | PosSales | payment.view / receipt.reprint | Low | 6 |
| GET | `/api/Receipts*` (if in swagger) | ReceiptsController | PosSales (class) | sale.view / receipt.reprint | Low | 6 |
| GET | `/api/Product/*`, `/api/pos/*` (catalog read) | ProductController | PosCatalogRead (class) | product.view / category.view / modifier.view | Low | 8 |
| GET | `/api/Product/list`, `all`, `catalog`, `active`, `categories`, `category/{name}`, `stock/{status}`, `search` | ProductController | PosCatalogRead | product.view, category.view | Low | 8 |

---

### 1.3 Catalog (Admin: products, categories, modifier-groups)

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/admin/products`, `/api/admin/products/{id}`, `search`, `stock/{id}` | AdminProductsController | CatalogManage (class) | product.manage (read: product.view) | Low | 2 |
| POST | `/api/admin/products` | AdminProductsController | CatalogManage | product.manage | Low | 2 |
| PUT | `/api/admin/products/{id}`, `stock/{id}` | AdminProductsController | CatalogManage | product.manage | Low | 2 |
| DELETE | `/api/admin/products/{id}` | AdminProductsController | CatalogManage | product.manage | Low | 2 |
| GET/POST/PUT/DELETE | `/api/admin/products/{id}/modifier-groups` | AdminProductsController | CatalogManage | product.manage / modifier.manage | Low | 2 |
| GET | `/api/admin/categories`, `/api/admin/categories/{id}`, `search` | CategoriesController | PosCatalogRead (class) | category.view | Low | 2 |
| POST | `/api/admin/categories` | CategoriesController | CatalogManage (method) | category.manage | Low | 2 |
| PUT | `/api/admin/categories/{id}` | CategoriesController | CatalogManage | category.manage | Low | 2 |
| DELETE | `/api/admin/categories/{id}` | CategoriesController | CatalogManage | category.manage | Low | 2 |
| GET | `/api/admin/categories/{id}/products` | CategoriesController | PosCatalogRead / CatalogManage | category.view / product.view | Low | 2 |
| GET | `/api/modifier-groups`, `/{id}`, `/{groupId}/modifiers`, `/{id}/products`, etc. | ModifierGroupsController | PosCatalogRead (class) | modifier.view | Low | 8 |
| POST/PUT/DELETE | `/api/modifier-groups/*` (write actions) | ModifierGroupsController | CatalogManage (method) | modifier.manage | Low | 2 |

---

### 1.4 Inventory

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/Inventory`, `/api/Inventory/{id}`, `low-stock`, `transactions/{id}` | InventoryController | [Authorize] (class); InventoryManage on write | inventory.view (read) / inventory.manage | Low | 3 |
| POST | `/api/Inventory` | InventoryController | InventoryManage | inventory.manage | Low | 3 |
| PUT | `/api/Inventory/{id}` | InventoryController | InventoryManage | inventory.manage | Low | 3 |
| POST | `/api/Inventory/{id}/restock` | InventoryController | InventoryManage | inventory.manage | Low | 3 |
| POST | `/api/Inventory/{id}/adjust` | InventoryController | HasPermission(InventoryAdjust) | inventory.adjust | Done | — |
| DELETE | `/api/Inventory/{id}` | InventoryController | InventoryDelete | inventory.manage (or proposal: inventory.delete) | Medium | 4 |

---

### 1.5 Audit

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/AuditLog` | AuditLogController | AuditView (method) | audit.view | Low | 5 |
| GET | `/api/AuditLog/{id}` | AuditLogController | AuditView | audit.view | Low | 5 |
| GET | `/api/AuditLog/payment/{paymentId}` | AuditLogController | AuditViewWithCashier (method) | audit.view | Low | 5 |
| GET | `/api/AuditLog/user/{userId}` | AuditLogController | UsersView (method) | user.view (or audit.view) | Low | 5 |
| GET | `/api/AuditLog/correlation/{id}`, `transaction/{id}`, `suspicious-admin-actions`, `statistics` | AuditLogController | AuditView / UsersView per method | audit.view | Low | 5 |
| DELETE | `/api/AuditLog/cleanup` | AuditLogController | HasPermission(AuditCleanup) | audit.cleanup | Done | — |
| GET | `/api/AuditLog/export` | AuditLogController | AuditAdmin (method) + HasPermission(AuditExport) | audit.export | Done | — |

---

### 1.6 TSE

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/Tse/status` | TseController | PosTse (class) | Keep role-based | High | Last |
| POST | `/api/Tse/connect`, `disconnect` | TseController | PosTse | Keep role-based | High | Last |
| POST | `/api/Tse/signature` | TseController | PosTse | Keep role-based | High | Last |
| GET | `/api/Tse/devices` | TseController | PosTse | Keep role-based | High | Last |
| POST | `/api/Tagesabschluss/daily`, `monthly`, `yearly` | TagesabschlussController | PosTse (class) | Keep role-based | High | Last |
| GET | `/api/Tagesabschluss/history`, `can-close/{id}`, `statistics` | TagesabschlussController | PosTse | Keep role-based | High | Last |
| POST | `/api/Payment/{id}/tse-signature` | PaymentController | PosTse (method) | Keep role-based | High | Last |
| GET | `/api/Payment/{id}/signature-debug` | PaymentController | PosTseDiagnostics (method) | Keep role-based | High | Last |
| POST | `/api/Payment/verify-signature` | PaymentController | PosTseDiagnostics (method) | Keep role-based | High | Last |

---

### 1.7 Settings (Company, FinanzOnline, Settings, Localization, MultilingualReceipt, CashRegister, Reports)

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| GET | `/api/CompanySettings`, `business-hours`, `banking`, `localization`, `billing` | CompanySettingsController | [Authorize] (class); BackofficeSettings or BackofficeManagement per method | settings.view / settings.manage | Low | 1 |
| PUT | `/api/CompanySettings`, `business-hours`, `banking`, `localization`, `billing` | CompanySettingsController | BackofficeSettings + HasPermission(SettingsManage) on methods | settings.manage | Low | 1 |
| GET | `/api/CompanySettings/export` | CompanySettingsController | BackofficeSettings | settings.view / report.export | Low | 1 |
| GET | `/api/FinanzOnline/config`, `status`, `errors`, `history/{id}` | FinanzOnlineController | BackofficeSettings (class) + HasPermission(FinanzOnlineView) on method | finanzonline.view | Low | 1 |
| PUT | `/api/FinanzOnline/config` | FinanzOnlineController | HasPermission(FinanzOnlineManage) | finanzonline.manage | Low | 1 |
| POST | `/api/FinanzOnline/submit-invoice`, `test-connection` | FinanzOnlineController | HasPermission(FinanzOnlineSubmit) etc. | finanzonline.submit | Low | 1 |
| GET/POST/PUT/DELETE | `/api/Settings/*` | SettingsController | BackofficeSettings (method) | settings.view / settings.manage | Low | 1 |
| GET/POST/PUT/DELETE | `/api/Localization/*` | LocalizationController | BackofficeSettings (method) | localization.view / localization.manage | Low | 1 |
| GET/POST/PUT | `/api/MultilingualReceipt/*` | MultilingualReceiptController | BackofficeSettings (method) | receipttemplate.view / receipttemplate.manage | Low | 1 |
| GET | `/api/CashRegister`, `/api/CashRegister/{id}`, `transactions` | CashRegisterController | [Authorize] (class) | cashregister.view | Low | 9 |
| POST | `/api/CashRegister/{id}/open`, `close` | CashRegisterController | CashRegisterManage (method on open/close?) | cashregister.manage / cashdrawer.open/close | Low | 9 |
| GET | `/api/Reports/sales`, `products`, `customers`, `inventory`, `payments`, `export/sales` | ReportsController | HasPermission(ReportView) (class) | report.view | Done | — |
| POST | `/api/Invoice/backfill-from-payments` | InvoiceController | SystemCritical (method) | Keep role-based | High | Last |
| DELETE | `/api/Invoice/{id}/permanent` or similar (if exists) | InvoiceController | SystemCritical | Keep role-based | High | Last |
| DELETE | `*/permanent` (entity hard delete) | EntityController (base) | SystemCritical (method) | Keep role-based | High | Last |

---

### 1.8 Auth, Invoice (general), other

| Method | Route | Controller / Action | Current policy / usage | Candidate permission | Risk | Phase |
|--------|-------|---------------------|------------------------|----------------------|------|-------|
| POST | `/api/Auth/login`, `register` | AuthController | AllowAnonymous (typical) | — | — | — |
| POST | `/api/Auth/logout` | AuthController | [Authorize] | — | — | — |
| GET | `/api/Auth/me`, `refresh` | AuthController | [Authorize] | — | — | — |
| GET/POST/PUT | `/api/Invoice/*` (list, pos-list, export, get, duplicate, credit-note, pdf, search, status) | InvoiceController | [Authorize] (class) | invoice.view / invoice.manage / invoice.export | Medium | Later |
| POST | `/api/Invoice/backfill-from-payments` | InvoiceController | SystemCritical | Keep role-based | High | Last |

---

## 2. Policy → candidate permission mapping (requested)

| Legacy policy | Role definition (current) | Candidate permission(s) | Notes |
|---------------|---------------------------|--------------------------|--------|
| **CatalogManage** | BackofficeManagers (SuperAdmin, Admin, Manager) | product.manage, category.manage, modifier.manage | Use resource-specific permission per endpoint; catalog already has these. |
| **InventoryManage** | BackofficeManagers | inventory.manage | Single controller; one policy type. |
| **InventoryDelete** | AdminOnly | inventory.manage (or proposal: inventory.delete) | Today AdminOnly; optional separate permission for audit. |
| **AuditAdmin** | AdminOnly | audit.cleanup, audit.export | Already HasPermission on cleanup/export methods. |
| **BackofficeSettings** | AdminOnly | settings.view, settings.manage | Per-endpoint; FinanzOnline uses finanzonline.*. |
| **BackofficeManagement** | BackofficeManagers | report.view, settings.view, etc. | ReportsController already report.view; others by resource. |
| **PosSales** | PosSalesRoles (Cashier, Manager, Admin, SuperAdmin) | sale.create, payment.take | Class-level → one or both; method-level already payment.take, payment.cancel, refund.create. |
| **PosTableOrder** | PosOrderRoles (Waiter, Cashier, Manager, Admin, SuperAdmin) | order.create, order.update, table.manage | Class-level; some methods already OrderUpdate, OrderCancel. |
| **SystemCritical** | AdminOnly | Keep role-based (proposal later: system.critical) | High-risk; do not migrate until explicit design. |
| **PosTse** | PosSalesRoles | Keep role-based (proposal later: receipt.sign / tse.sign) | TSE/fiscal; leave for last. |
| **PosTseDiagnostics** | AdminOnly | Keep role-based (proposal later: tse.diagnostics) | Leave for last. |
| **CashRegisterManage** | AdminOnly | cashregister.manage | Single controller; permission exists. |
| **UsersView** | SuperAdmin, Admin, BranchManager, Auditor | user.view | — |
| **UsersManage** | SuperAdmin, Admin, BranchManager | user.manage | — |
| **AdminUsers** | AdminOnly | user.manage (or keep) | Same roles as UsersManage for admin scope. |

---

## 3. Low-risk-first migration order

1. **Phase 1 – Settings:** BackofficeSettings → settings.view / settings.manage (and existing FinanzOnline*, SettingsManage on CompanySettings, etc.). Many endpoints already use HasPermission; align class-level.
2. **Phase 2 – Catalog + Users:** CatalogManage → product.manage, category.manage, modifier.manage per endpoint. UsersView/UsersManage → user.view, user.manage.
3. **Phase 3 – Inventory:** InventoryManage → inventory.manage; InventoryDelete → keep role or add inventory.delete (proposal).
4. **Phase 4 – Inventory delete:** Decide inventory.delete vs inventory.manage-only; if new permission, add to catalog and matrix (AdminOnly).
5. **Phase 5 – Audit:** AuditView / AuditViewWithCashier → audit.view; remaining AuditAdmin → already audit.cleanup / audit.export on methods.
6. **Phase 6 – PosSales:** Class-level PosSales → sale.create and/or payment.take (CartController, PaymentController, ReceiptsController).
7. **Phase 7 – PosTableOrder:** Class-level → order.create, order.update, table.manage (OrdersController, TableController, CustomerController).
8. **Phase 8 – PosCatalogRead:** product.view, category.view, modifier.view at class/method for ProductController, CategoriesController (read), ModifierGroupsController (read).
9. **Phase 9 – CashRegisterManage:** cashregister.manage for CashRegisterController.
10. **Last – Do not migrate yet:** PosTse, PosTseDiagnostics, SystemCritical (keep role-based).

---

## 4. Special-case policies (keep role-based for now)

- **PosTse** – TSE operations (TseController, TagesabschlussController, PaymentController TSE actions). Fiscal/RKSV-sensitive; no permission in catalog yet.
- **PosTseDiagnostics** – Admin-only TSE diagnostics (signature-debug, verify-signature). Leave on role until tse.diagnostics (or similar) is defined.
- **SystemCritical** – Admin-only permanent delete and backfill (EntityController.HardDelete, InvoiceController.BackfillInvoicesFromPayments). Leave on role until system.critical (or equivalent) is designed and approved.

---

## 5. Suggested test cases

- **Permission vs role parity:** For each migrated endpoint, test that every role that had access under the legacy policy still has access with the new permission (RolePermissionMatrix grants that permission to the same roles).
- **Regression:** Run existing auth tests: `RolePermissionMatrixTests`, `UserManagementAuthorizationPolicyTests`, `PermissionAuthorizationHandlerTests`, `RoleCanonicalizationTests` (filter: `FullyQualifiedName~RolePermissionMatrix|UserManagementAuthorization|PermissionAuthorization|RoleCanonicalization`).
- **Smoke:** (1) Cashier: Cart, Payment (payment.take), Orders (order.create). (2) Manager: Catalog (product.manage), Inventory (inventory.manage), Audit (audit.export). (3) Admin: Settings (settings.manage), Inventory delete, TSE diagnostics, SystemCritical endpoints. (4) ReportViewer: Reports (report.view) only.
- **Negative:** ReportViewer must not access Payment take, Cart write, or Settings manage. Cashier must not access Inventory manage or Audit cleanup.
- **TSE/SystemCritical:** No permission-based tests until those policies are migrated; keep role-only tests.

---

## 6. File reference

| Topic | File |
|-------|------|
| Legacy role policies | `backend/Authorization/AuthorizationExtensions.cs` (AddLegacyRolePolicies) |
| Permission policies | `backend/Authorization/AuthorizationExtensions.cs` (AddPermissionPolicies), `PermissionCatalog.cs` |
| Permission names | `backend/Authorization/AppPermissions.cs` |
| Role → permission | `backend/Authorization/RolePermissionMatrix.cs` |
| Attribute | `backend/Authorization/HasPermissionAttribute.cs` |
| Endpoint list | `backend/swagger.json` (paths) |
| Design / migration order | `docs/architecture/PERMISSION_FIRST_ARCHITECTURE_DESIGN.md` |
