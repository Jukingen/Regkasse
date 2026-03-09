# Final Endpoint → Permission Map

**Single source of truth for auth model:** [FinalAuthorizationModel.md](FinalAuthorizationModel.md).

**Source:** Backend controllers + `AppPermissions`, `PermissionCatalog`, `RolePermissionMatrix`, `HasPermissionAttribute`.  
**Gaps closed:** Invoice, CashRegister, Inventory, FinanzOnline, CompanySettings, Settings, MultilingualReceipt, Localization read actions now have explicit permission attributes. **Administrator** is not a role; use **Admin**.

---

## 1. Migration table (HTTP method, route, controller/action, current/target auth, permission, roles, risk, FE, test)

| Category | HTTP | Route | Controller / Action | Current auth | Target auth | Required permission | Allowed roles (matrix) | Risk | FE impact | Test case needed |
|----------|------|--------|----------------------|--------------|-------------|----------------------|-------------------------|------|-----------|------------------|
| **Users** | GET | api/UserManagement | UserManagementController.GetUsers | HasPermission(UserView) | same | user.view | Manager, Admin, SuperAdmin | low | Menu/users page | 403 for Cashier |
| | GET | api/UserManagement/{id} | GetUser | HasPermission(UserView) | same | user.view | Manager, Admin, SuperAdmin | low | User detail | same |
| | POST | api/UserManagement | CreateUser | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin | medium | Create user | 403 for Manager |
| | PUT | api/UserManagement/{id} | UpdateUser | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin | medium | Edit user | same |
| | PUT | api/UserManagement/me/password | ChangeOwnPassword | Authorize | same | (any authenticated) | all | low | Profile | — |
| | POST | api/UserManagement/{id}/deactivate | DeactivateUser | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin | medium | Deactivate | same |
| | POST | api/UserManagement/{id}/reactivate | ReactivateUser | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin | low | Reactivate | same |
| | POST | api/UserManagement/{id}/reset-password | ResetPassword | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin | high | Reset password | SuperAdmin-only for target SuperAdmin |
| | GET | api/UserManagement/roles | GetRoles | HasPermission(UserView) | same | user.view | Manager, Admin, SuperAdmin | low | Role dropdown | same |
| | POST | api/UserManagement/roles | CreateRole | HasPermission(UserManage) | same | user.manage | Admin, SuperAdmin; create role only SuperAdmin in policy | high | Create role | SuperAdmin only |
| GET | api/admin/users (various) | AdminUsersController | HasPermission(UserManage) class | same | user.manage | Admin, SuperAdmin | medium | Admin user UI | 403 for Manager |
| **POS** | GET/POST/PUT/DELETE | api/Cart, api/pos/cart | CartController | HasPermission(CartManage) class | same | cart.manage | Cashier, Manager, Admin, SuperAdmin | medium | POS cart | Cashier/Waiter access |
| | GET/POST/PUT/DELETE | api/Orders | OrdersController | OrderCreate / OrderUpdate / OrderCancel | same | order.create, order.update, order.cancel | Cashier, Waiter, Manager, Admin, SuperAdmin | medium | Orders, tables | Waiter no cancel |
| | GET/POST | api/Table | TableController | HasPermission(TableManage) class | same | table.manage | Cashier, Waiter, Manager, Admin, SuperAdmin | low | Tables | same |
| | GET/POST/PUT | api/Customer | CustomerController | HasPermission(OrderCreate) class | same | order.create | Cashier, Waiter, Manager, Admin, SuperAdmin | low | Customers | same |
| | GET/POST | api/Payment, api/pos/payment | PaymentController | PaymentTake, PaymentCancel, RefundCreate, TseSign, TseDiagnostics | same | payment.take, payment.cancel, refund.create, tse.sign, tse.diagnostics | Cashier, Manager, Admin, SuperAdmin (diagnostics Admin only) | high | Payment, TSE | 403 for Waiter on cancel/refund |
| | GET/POST | api/Receipts | ReceiptsController | HasPermission(SaleCreate) class | same | sale.create | Cashier, Manager, Admin, SuperAdmin | medium | Receipts | same |
| **Catalog** | GET | api/Product, api/pos | ProductController | HasPermission(ProductView) class | same | product.view | Cashier, Waiter, Manager, Admin, SuperAdmin | low | POS products | same |
| | GET/POST/PUT/DELETE | api/admin/products | AdminProductsController | HasPermission(ProductManage) class | same | product.manage | Manager, Admin, SuperAdmin | medium | Admin products | 403 for Cashier |
| | GET | api/admin/categories | CategoriesController (read) | HasPermission(CategoryView) class | same | category.view | Cashier, Waiter, Manager, Admin, SuperAdmin | low | Categories read | same |
| | POST/PUT/DELETE | api/admin/categories | CategoriesController (write) | HasPermission(CategoryManage) | same | category.manage | Manager, Admin, SuperAdmin | medium | Categories write | same |
| | GET | api/modifier-groups | ModifierGroupsController (read) | HasPermission(ModifierView) class | same | modifier.view | Cashier, Waiter, Manager, Admin, SuperAdmin | low | Modifiers read | same |
| | POST/PUT/DELETE | api/modifier-groups | ModifierGroupsController (write) | HasPermission(ModifierManage) | same | modifier.manage | Manager, Admin, SuperAdmin | medium | Modifiers write | same |
| **Inventory** | GET | api/Inventory | GetInventory | HasPermission(InventoryView) | same | inventory.view | Cashier, Manager, Admin, SuperAdmin | low | Inventory list | 403 for Waiter |
| | GET | api/Inventory/{id} | GetInventoryItem | HasPermission(InventoryView) | same | inventory.view | same | low | Inventory detail | same |
| | GET | api/Inventory/low-stock | GetLowStockItems | HasPermission(InventoryView) | same | inventory.view | same | low | Low stock | same |
| | GET | api/Inventory/transactions/{id} | GetInventoryTransactions | HasPermission(InventoryView) | same | inventory.view | same | low | Transactions | same |
| | POST | api/Inventory (create) | — | HasPermission(InventoryManage) | same | inventory.manage | Manager, Admin, SuperAdmin | medium | Create item | same |
| | PUT | api/Inventory/{id} | Update | HasPermission(InventoryManage) | same | inventory.manage | same | medium | Update | same |
| | POST | api/Inventory/{id}/restock | Restock | HasPermission(InventoryManage) | same | inventory.manage | same | medium | Restock | same |
| | POST | api/Inventory/{id}/adjust | Adjust | InventoryManage + InventoryAdjust | same | inventory.manage, inventory.adjust | same | medium | Adjust | same |
| | DELETE | api/Inventory/{id} | Delete | HasPermission(InventoryDelete) | same | inventory.delete | Admin, SuperAdmin | high | Delete item | 403 for Manager |
| **Audit** | GET | api/AuditLog | GetAuditLogs | HasPermission(UserView) | same | user.view | Manager, Admin, SuperAdmin | low | Audit list | 403 for Cashier |
| | GET | api/AuditLog/{id} | GetAuditLog | HasPermission(AuditView) | same | audit.view | Manager, Admin, SuperAdmin, ReportViewer, Accountant | low | Audit detail | same |
| | GET | api/AuditLog/... (other read) | various | AuditView / UserView | same | audit.view, user.view | same | low | — | same |
| | DELETE | api/AuditLog/cleanup | CleanupOldAuditLogs | HasPermission(AuditCleanup) | same | audit.cleanup | Admin, SuperAdmin | high | Cleanup | 403 for Manager |
| | GET | api/AuditLog/export | ExportAuditLogs | HasPermission(AuditExport) | same | audit.export | Manager, Admin, SuperAdmin | medium | Export | same |
| **Reports** | GET | api/Reports/* | ReportsController | HasPermission(ReportView) class | same | report.view | Manager, Admin, SuperAdmin, ReportViewer, Accountant | low | Reports | 403 for Cashier |
| **Settings** | GET | api/Settings | GetSettings | HasPermission(SettingsView) | same | settings.view | Manager, Admin, SuperAdmin, ReportViewer | low | Settings read | same |
| | GET | api/Settings/tax-rates, notifications | GET tax-rates, GET notifications | HasPermission(SettingsView) | same | settings.view | same | low | — | same |
| | PUT | api/Settings, backup, notifications, export | Update*, Backup, Export | HasPermission(SettingsManage) | same | settings.manage | Admin, SuperAdmin | medium | Settings write | 403 for Manager |
| | GET | api/CompanySettings | GetCompanySettings | HasPermission(SettingsView) | same | settings.view | same | low | Company read | same |
| | GET | api/CompanySettings/business-hours, banking, localization, billing | GET* | SettingsView | same | settings.view | same | low | — | same |
| | PUT | api/CompanySettings/* | Update* | HasPermission(SettingsManage) | same | settings.manage | Admin, SuperAdmin | medium | Company write | same |
| | GET/POST/PUT/DELETE | api/Localization | LocalizationController | LocalizationView / LocalizationManage | same | localization.view, localization.manage | Admin, SuperAdmin (manage); ReportViewer (view in matrix is settings only) | low | Localization | 403 for Cashier |
| | GET/POST/PUT/DELETE | api/MultilingualReceipt | MultilingualReceiptController | ReceiptTemplateView / ReceiptTemplateManage | same | receipttemplate.view, receipttemplate.manage | Manager, Admin, SuperAdmin (manage); view in matrix | low | Receipt templates | same |
| **CashRegister** | GET | api/CashRegister | GetCashRegisters | HasPermission(CashRegisterView) | same | cashregister.view | Cashier, Manager, Admin, SuperAdmin | low | Register list | same |
| | GET | api/CashRegister/{id} | GetCashRegister | HasPermission(CashRegisterView) | same | cashregister.view | same | low | Register detail | same |
| | POST | api/CashRegister | CreateCashRegister | HasPermission(CashRegisterManage) | same | cashregister.manage | Admin, SuperAdmin | medium | Create register | 403 for Manager |
| | POST | api/CashRegister/{id}/open | OpenCashRegister | HasPermission(ShiftOpen) | same | shift.open | Cashier, Manager, Admin, SuperAdmin | medium | Open shift | same |
| | POST | api/CashRegister/{id}/close | CloseCashRegister | HasPermission(ShiftClose) | same | shift.close | same | medium | Close shift | same |
| | GET | api/CashRegister/{id}/transactions | GetCashRegisterTransactions | HasPermission(CashRegisterView) | same | cashregister.view | same | low | Transactions | same |
| **TSE** | GET/POST | api/Tse | TseController | HasPermission(TseSign) class | same | tse.sign | Cashier, Manager, Admin, SuperAdmin | high | TSE status, connect, sign | 403 for Waiter |
| | GET/POST | api/Tagesabschluss | TagesabschlussController | HasPermission(TseSign) class | same | tse.sign | same | high | Daily close | same |
| | GET | api/Payment/{id}/signature-debug, POST verify-signature | PaymentController | HasPermission(TseDiagnostics) | same | tse.diagnostics | Admin, SuperAdmin | high | TSE diagnostics | 403 for Manager |
| **SystemCritical** | POST | api/Invoice/backfill-from-payments | InvoiceController.BackfillInvoicesFromPayments | HasPermission(SystemCritical) | same | system.critical | SuperAdmin only | high | Backfill | 403 for Admin |
| | DELETE | api/*/{id}/permanent | EntityController.PermanentDelete | HasPermission(SystemCritical) | same | system.critical | SuperAdmin only | high | Permanent delete | same |
| **Invoice** | GET | api/Invoice/list, pos-list, export, {id}, search, status, {id}/pdf | InvoiceController (read/export) | HasPermission(InvoiceView) or InvoiceExport | same | invoice.view, invoice.export | Manager, Admin, SuperAdmin (+ export); ReportViewer/Accountant invoice.view in matrix | medium | Invoices, export | 403 for Cashier on export if no permission |
| | POST | api/Invoice | CreateInvoice | HasPermission(InvoiceManage) | same | invoice.manage | Manager, Admin, SuperAdmin | medium | Create invoice | same |
| | PUT | api/Invoice/{id} | UpdateInvoice | HasPermission(InvoiceManage) | same | invoice.manage | same | medium | Update | same |
| | DELETE | api/Invoice/{id} | DeleteInvoice (soft) | HasPermission(InvoiceManage) | same | invoice.manage | same | medium | Soft delete | same |
| | POST | api/Invoice/{id}/duplicate | DuplicateInvoice | HasPermission(InvoiceManage) | same | invoice.manage | same | low | Duplicate | same |
| | POST | api/Invoice/{id}/credit-note | CreateCreditNote | HasPermission(CreditNoteCreate) | same | creditnote.create | Manager, Admin, SuperAdmin | high | Credit note | same |
| **FinanzOnline** | GET | api/FinanzOnline/config, status, errors, history/{id} | FinanzOnlineController | FinanzOnlineView / SettingsView class | same | finanzonline.view, settings.view | Admin, SuperAdmin, Accountant (view) | high | RKSV/FinanzOnline | 403 for Cashier |
| | PUT | api/FinanzOnline/config | UpdateConfig | HasPermission(FinanzOnlineManage) | same | finanzonline.manage | Admin, SuperAdmin | high | Config | same |
| | POST | api/FinanzOnline/submit-invoice | SubmitInvoice | HasPermission(FinanzOnlineSubmit) | same | finanzonline.submit | Admin, SuperAdmin | high | Submit | same |
| | POST | api/FinanzOnline/test-connection | TestConnection | HasPermission(FinanzOnlineManage) | same | finanzonline.manage | Admin, SuperAdmin | medium | Test connection | same |
| **Auth** | POST | api/Auth/login, register | AuthController | none | same | (public) | — | — | Login/register | — |
| | GET | api/Auth/me | GetMe | Authorize (if applied) | same | (authenticated) | all | low | Me | — |
| | POST | api/Auth/logout, refresh | Logout, Refresh | same | same | (authenticated) | all | low | — | — |
| **UserSettings** | GET/PUT | api/user/settings | UserSettingsController | Authorize | same | (any authenticated) | all | low | User preferences | — |

---

## 2. Final endpoint–permission map (compact)

| Permission | Endpoints (method + route pattern) |
|------------|------------------------------------|
| user.view | GET api/UserManagement, GET api/UserManagement/{id}, GET api/UserManagement/roles; GET api/AuditLog (list) |
| user.manage | POST/PUT api/UserManagement, POST deactivate/reactivate/reset-password, POST api/UserManagement/roles; api/admin/users (all) |
| product.view | GET api/Product, GET api/pos (ProductController) |
| product.manage | api/admin/products (AdminProductsController) |
| category.view | GET api/admin/categories (CategoriesController) |
| category.manage | POST/PUT/DELETE api/admin/categories |
| modifier.view | GET api/modifier-groups (read) |
| modifier.manage | POST/PUT/DELETE api/modifier-groups |
| order.create | api/Orders (class), api/Customer (class), api/Table (class) |
| order.update | PUT api/Orders/{id}/status |
| order.cancel | DELETE api/Orders/{id} |
| table.manage | api/Table (class) |
| cart.manage | api/Cart, api/pos/cart (CartController) |
| sale.create | api/Receipts (class) |
| payment.view | (no dedicated read-only controller; PaymentController is payment.take-scoped) |
| payment.take | api/Payment, api/pos/payment (class); POST payment, GET methods |
| payment.cancel | POST api/Payment/{id}/cancel |
| refund.create | POST api/Payment/{id}/refund |
| cashregister.view | GET api/CashRegister, GET api/CashRegister/{id}, GET api/CashRegister/{id}/transactions |
| cashregister.manage | POST api/CashRegister |
| shift.open | POST api/CashRegister/{id}/open |
| shift.close | POST api/CashRegister/{id}/close |
| inventory.view | GET api/Inventory, GET api/Inventory/{id}, GET low-stock, GET transactions/{id} |
| inventory.manage | POST/PUT api/Inventory, POST restock, POST adjust (InventoryManage + InventoryAdjust on adjust) |
| inventory.delete | DELETE api/Inventory/{id} |
| invoice.view | GET api/Invoice/list, pos-list, GET api/Invoice, GET api/Invoice/{id}, search, status/{status}, {id}/pdf |
| invoice.export | GET api/Invoice/export |
| invoice.manage | POST api/Invoice, PUT api/Invoice/{id}, DELETE api/Invoice/{id}, POST api/Invoice/{id}/duplicate |
| creditnote.create | POST api/Invoice/{id}/credit-note |
| settings.view | GET api/Settings, GET api/Settings/tax-rates, GET api/Settings/notifications; GET api/CompanySettings, business-hours, banking, localization, billing; FinanzOnlineController (class) |
| settings.manage | PUT api/Settings, backup, notifications, export; PUT api/CompanySettings/* |
| localization.view | GET api/Localization, GET languages, currencies, timezones, format, currency |
| localization.manage | PUT api/Localization, POST add-language, add-currency, DELETE remove-language, remove-currency, GET export |
| receipttemplate.view | GET api/MultilingualReceipt, GET {id}, language, type, preview; POST generate |
| receipttemplate.manage | POST/PUT/DELETE api/MultilingualReceipt, GET export |
| audit.view | GET api/AuditLog/{id}, statistics, etc. (read actions) |
| audit.export | GET api/AuditLog/export |
| audit.cleanup | DELETE api/AuditLog/cleanup |
| report.view | api/Reports (class) |
| finanzonline.view | GET api/FinanzOnline/config, status, errors, history/{id} |
| finanzonline.manage | PUT api/FinanzOnline/config, POST test-connection |
| finanzonline.submit | POST api/FinanzOnline/submit-invoice |
| tse.sign | api/Tse (class), api/Tagesabschluss (class); POST api/Payment/{id}/tse-signature |
| tse.diagnostics | GET api/Payment/{id}/signature-debug, POST api/Payment/verify-signature |
| system.critical | POST api/Invoice/backfill-from-payments; DELETE api/*/{id}/permanent (EntityController) |

---

## 3. Kalan role-only alanlar

- **Auth:** login, register, me, logout, refresh — yetkilendirme “authenticated” veya public; permission yok.
- **UserSettings:** api/user/settings — sadece `[Authorize]`; kullanıcı kendi ayarları (role/permission ayrımı yok).
- **EntityController tabanlı CRUD:** ProductController (EntityController’dan türeyen) GET/POST/PUT/DELETE (soft) — class-level `ProductView` ile korunuyor; `PermanentDelete` tek action `SystemCritical`. Role-only kalan özel bir endpoint yok; tüm korumalar permission-first.

---

## 4. Test backlog

| # | Test | Priority |
|---|------|----------|
| 1 | Invoice: Cashier has invoice.view, no invoice.export → GET list 200, GET export 403 | high |
| 2 | Invoice: Manager has invoice.manage → POST/PUT/DELETE 200, POST backfill 403 | high |
| 3 | CashRegister: Cashier GET list 200, POST create 403; POST open/close 200 | high |
| 4 | Inventory: Cashier GET 200, DELETE 403; Manager GET/PUT 200, DELETE 403 | high |
| 5 | UserManagement: Manager GET users 200, POST create 403; Admin POST create 200 | high |
| 6 | TSE: Cashier POST sign 200; GET signature-debug 403; Admin GET signature-debug 200 | high |
| 7 | EntityController: PermanentDelete requires system.critical → Admin 403, SuperAdmin 200 | high |
| 8 | FinanzOnline: Accountant GET status/errors 200, PUT config 403 | medium |
| 9 | Settings/Localization/ReceiptTemplate: read (view) vs write (manage) per role | medium |
| 10 | Reports: ReportViewer GET reports 200; Cashier 403 | medium |
| 11 | Audit: ReportViewer GET audit 200; cleanup/export 403 for ReportViewer | medium |
| 12 | Integration: JWT permission claims match RolePermissionMatrix for each role | medium |

---

**Değişiklik özeti (bu turda kapatılan eksikler):**

- **InvoiceController:** list, pos-list, export, GET, GET {id}, POST, PUT, DELETE, duplicate, credit-note, pdf, search, status → ilgili action’lara `InvoiceView`, `InvoiceExport`, `InvoiceManage`, `CreditNoteCreate` eklendi.
- **CashRegisterController:** GET, GET {id}, GET {id}/transactions → `CashRegisterView`; POST {id}/open → `ShiftOpen`; POST {id}/close → `ShiftClose`.
- **InventoryController:** GET (list), GET {id}, GET low-stock, GET transactions/{id} → `InventoryView`.
- **FinanzOnlineController:** GET status, errors, history → `FinanzOnlineView`; POST test-connection → `FinanzOnlineManage`.
- **CompanySettingsController:** GET, GET business-hours, GET localization → `SettingsView`.
- **SettingsController:** GET, GET tax-rates, GET notifications → `SettingsView`.
- **MultilingualReceiptController:** GET list, {id}, language, type, preview, POST generate → `ReceiptTemplateView`.
- **LocalizationController:** GET, GET languages, currencies, timezones, format, currency → `LocalizationView`.
