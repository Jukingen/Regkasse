# Authorization Yapısı – Envanter

**Tarih:** 2025-03-09

---

## Ortak bağlam

- Bu proje **POS + backoffice hibrit** yapıdadır.
- Authorization şu anda **büyük ölçüde role bazlı** (policy adları ve `RequireRole`).
- **Hedef mimari:** role + permission; ileride scope (tenant/branch/ownership) eklenebilir.
- **Administrator** rolü legacy alias olarak ele alınacak; hedef tek ana rol adı **Admin**.
- Amaç **big bang rewrite değil**; minimum kırılımla migration.
- **Backoffice** ve **operasyonel POS** yetkileri net ayrılmalı.
- Swagger’da **legacy** `/api/Payment` endpoint’leri deprecated; yeni POS yüzeyi **/api/pos/payment** altında.
- Benzer şekilde admin yüzeyi **/api/admin/** altında şekillenmeye başlamış.
- **Kritik alanlar:** payment, refund, order status update, order cancel, company settings update, audit log cleanup, finanzonline config/submit, inventory adjust.
- **Beklenen tasarım:** rol = iş profili; permission = aksiyon yetkisi; scope = tenant/branch/ownership sınırı.

---

## 1) Bulunan kullanım türleri

| Tür | Konum | Not |
|-----|--------|-----|
| `[Authorize]` | Çoğu controller (class) | Sadece authenticated |
| `[Authorize(Policy = "...")]` | Controller/action | Role tabanlı policy (Program.cs) |
| `[AllowAnonymous]` | TestController | Test endpoint |
| `User.IsInRole(role)` | BaseController | Helper; role string ile |
| `userRole != "Admin" && userRole != "Kasiyer"` | CartController (force-cleanup) | Inline role; typo "Kasiyer" |
| `u.Role == role` / `request.Role != user.Role` | AdminUsersController, UserManagementController | Filtre ve validasyon |
| `[HasPermission(AppPermissions.X)]` | PaymentController, OrdersController, AuditLogController (cleanup) | Yeni permission attribute (az sayıda) |

---

## 2) /api/admin/* – Tam tablo

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| CategoriesController | GetCategories | api/admin/categories | GET | Policy = PosCatalogRead | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | Hayır | Katalog okuma |
| CategoriesController | GetCategory | api/admin/categories/{id} | GET | (class) PosCatalogRead | (aynı) | Hayır | Hayır | |
| CategoriesController | CreateCategory | api/admin/categories | POST | Policy = CatalogManage | SuperAdmin, Admin, Administrator, Manager | Hayır | Hayır | |
| CategoriesController | UpdateCategory | api/admin/categories/{id} | PUT | Policy = CatalogManage | (aynı) | Hayır | Hayır | |
| CategoriesController | DeleteCategory | api/admin/categories/{id} | DELETE | Policy = CatalogManage | (aynı) | Hayır | Hayır | |
| CategoriesController | GetCategoryProducts | api/admin/categories/{id}/products | GET | (class) PosCatalogRead | (aynı) | Hayır | Hayır | |
| CategoriesController | Search | api/admin/categories/search | GET | (class) PosCatalogRead | (aynı) | Hayır | Hayır | |
| AdminProductsController | GetList | api/admin/products | GET | (class) CatalogManage | SuperAdmin, Admin, Administrator, Manager | Hayır | Hayır | Tüm action’lar class policy |
| AdminProductsController | Get(id), Search, Create, Update, PutStock, Delete, GetModifierGroups, PostModifierGroups | api/admin/products/... | GET/POST/PUT/DELETE | (class) CatalogManage | (aynı) | Hayır | Hayır | |
| AdminUsersController | Get, Get(id), Post, Patch, Deactivate, Reactivate, ForcePasswordReset, GetActivity | api/admin/users | GET/POST/PATCH | Policy = AdminUsers | SuperAdmin, Admin, Administrator | Hayır | Hayır | Backoffice kullanıcı; Role filtresi var |

---

## 3) /api/pos/* ve legacy Payment/Cart

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| CartController | GetCurrent | api/Cart/current, api/pos/cart/current | GET | (class) PosSales | Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | api/Cart evet | Legacy route deprecated |
| CartController | Get(cartId), Post, AddItem, Put/Delete items, Clear, Complete, History, ForceCleanup, TableOrdersRecovery, Increment/Decrement | api/Cart/..., api/pos/cart/... | GET/POST/PUT/DELETE | (class) PosSales | (aynı) | Hayır | api/Cart evet | ForceCleanup’ta inline role check (Admin, Kasiyer) |
| PaymentController | GetMethods | api/Payment/methods, api/pos/payment/methods | GET | (class) PosSales | (aynı) | Hayır | api/Payment evet | |
| PaymentController | CreatePayment | api/Payment, api/pos/payment | POST | PosSales + HasPermission(PaymentTake) | (aynı) | Hayır | api/Payment evet | |
| PaymentController | Get(id), GetReceipt, GetStatistics, GetDateRange, GetCustomer, GetMethod, GetQr | api/Payment/..., api/pos/payment/... | GET | (class) PosSales | (aynı) | Hayır | api/Payment evet | |
| PaymentController | Cancel | api/Payment/{id}/cancel, api/pos/payment/{id}/cancel | POST | (class) PosSales | (aynı) | Hayır | api/Payment evet | Kritik |
| PaymentController | Refund | api/Payment/{id}/refund, api/pos/payment/{id}/refund | POST | (class) PosSales | (aynı) | Hayır | api/Payment evet | Kritik |
| PaymentController | TseSignature | api/Payment/{id}/tse-signature, api/pos/payment/... | POST | Policy = PosTse | (aynı) | Hayır | api/Payment evet | |
| PaymentController | SignatureDebug | api/Payment/{id}/signature-debug, api/pos/... | GET | Policy = PosTseDiagnostics | SuperAdmin, Admin, Administrator | Hayır | api/Payment evet | Kritik |
| PaymentController | VerifySignature | api/Payment/verify-signature, api/pos/... | POST | Policy = PosTseDiagnostics | (aynı) | Hayır | api/Payment evet | Kritik |
| ProductController | Get, List, Catalog, Categories, Search, … (GET) | api/Product/..., api/pos/... | GET | (class) PosCatalogRead | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | api/Product evet | POS katalog; yazma aynı controller |

---

## 4) Orders

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| OrdersController | GetOrders | api/Orders | GET | (class) PosTableOrder | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | Hayır | |
| OrdersController | GetOrder | api/Orders/{id} | GET | (class) PosTableOrder | (aynı) | Hayır | Hayır | |
| OrdersController | CreateOrder | api/Orders | POST | (class) PosTableOrder | (aynı) | Hayır | Hayır | |
| OrdersController | UpdateOrderStatus | api/Orders/{id}/status | PUT | PosTableOrder + HasPermission(OrderUpdate) | (aynı) | Hayır | Hayır | Kritik |
| OrdersController | DeleteOrder | api/Orders/{id} | DELETE | (class) PosTableOrder | (aynı) | Hayır | Hayır | Kritik |
| OrdersController | GetOrdersByStatus | api/Orders/status/{status} | GET | (class) PosTableOrder | (aynı) | Hayır | Hayır | |

---

## 5) Payment (özet – detay yukarıda)

Legacy: **api/Payment** prefix. Yeni: **api/pos/payment**. Aynı controller, iki route; Swagger’da api/Payment deprecated.

---

## 6) Invoice

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| InvoiceController | GetList, GetPosList, Get, Get(id), Export, Search, GetByStatus, GetPdf | api/Invoice | GET | [Authorize] | Authenticated | Hayır | Hayır | |
| InvoiceController | Create, Put, Delete, Duplicate, CreditNote | api/Invoice | POST/PUT/DELETE | [Authorize] | (aynı) | Hayır | Hayır | |
| InvoiceController | BackfillFromPayments | api/Invoice/backfill-from-payments | POST | Policy = SystemCritical | SuperAdmin, Admin, Administrator | Hayır | Hayır | Kritik |

---

## 7) Inventory

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| InventoryController | GetInventory, Get(id), GetLowStock, GetTransactions | api/Inventory | GET | [Authorize] | Authenticated | Hayır | Hayır | |
| InventoryController | Create | api/Inventory | POST | Policy = InventoryManage | SuperAdmin, Admin, Administrator, Manager | Hayır | Hayır | |
| InventoryController | Update, Restock, Adjust | api/Inventory/{id}, .../restock, .../adjust | PUT/POST | Policy = InventoryManage | (aynı) | Hayır | Hayır | Kritik: adjust |
| InventoryController | Delete | api/Inventory/{id} | DELETE | Policy = InventoryDelete | SuperAdmin, Admin, Administrator | Hayır | Hayır | |

---

## 8) CompanySettings

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| CompanySettingsController | Get | api/CompanySettings | GET | [Authorize] | Authenticated | Hayır | Hayır | |
| CompanySettingsController | Put (main) | api/CompanySettings | PUT | Policy = BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | Hayır | Kritik |
| CompanySettingsController | Get/Put business-hours, banking, localization, billing, export | api/CompanySettings/... | GET/PUT | BackofficeSettings veya BackofficeManagement | (aynı) / Manager (bazı GET) | Hayır | Hayır | Kritik: tüm PUT |

---

## 9) Localization

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| LocalizationController | Get, GetLanguages, GetCurrencies, GetTimezones, GetFormat, GetCurrency | api/Localization | GET | [Authorize] | Authenticated | Hayır | Hayır | |
| LocalizationController | Put, AddLanguage, AddCurrency, RemoveLanguage, RemoveCurrency, Export | api/Localization | PUT/POST/DELETE/GET | Policy = BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | Hayır | |

---

## 10) AuditLog

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| AuditLogController | GetAuditLogs | api/AuditLog | GET | Policy = UsersView | SuperAdmin, Admin, Administrator, BranchManager, Auditor | Hayır | Hayır | |
| AuditLogController | Get(id) | api/AuditLog/{id} | GET | Policy = AuditView | SuperAdmin, Admin, Administrator, Manager | Hayır | Hayır | |
| AuditLogController | GetPayment | api/AuditLog/payment/{paymentId} | GET | Policy = AuditViewWithCashier | + Cashier | Hayır | Hayır | |
| AuditLogController | GetUser, GetCorrelation, GetSuspicious, GetTransaction, GetStatistics | api/AuditLog/... | GET | UsersView veya AuditView | (yukarıdaki setler) | Hayır | Hayır | |
| AuditLogController | CleanupOldAuditLogs | api/AuditLog/cleanup | DELETE | HasPermission(AuditCleanup) | (RolePermissionMatrix) | Hayır | Hayır | Kritik |
| AuditLogController | ExportAuditLogs | api/AuditLog/export | GET | Policy = AuditAdmin | SuperAdmin, Admin, Administrator | Hayır | Hayır | Kritik |

---

## 11) FinanzOnline

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| FinanzOnlineController | GetConfig, PutConfig, GetStatus, GetErrors, GetHistory, SubmitInvoice, TestConnection | api/FinanzOnline | GET/PUT/POST | (class) BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | Hayır | Config ve submit kritik |

---

## 12) CashRegister

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | AllowAnonymousMi | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|------------------|--------------|--------|
| CashRegisterController | Get, Get(id), GetTransactions | api/CashRegister | GET | [Authorize] | Authenticated | Hayır | Hayır | |
| CashRegisterController | Post (create) | api/CashRegister | POST | Policy = CashRegisterManage | SuperAdmin, Admin, Administrator | Hayır | Hayır | |
| CashRegisterController | Open, Close | api/CashRegister/{id}/open, close | POST | [Authorize] (class) | Authenticated | Hayır | Hayır | POS operasyon |

---

## 13) Diğer controller’lar (kısa)

| Controller | Route | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi |
|------------|-------|----------------------------|---------------|--------------|
| TableController | api/Table | PosTableOrder | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır |
| CustomerController | api/Customer | PosTableOrder | (aynı) | Hayır |
| ReceiptsController | api/Receipts | PosSales | Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır |
| TseController | api/Tse | PosTse | (aynı) | Hayır |
| TagesabschlussController | api/Tagesabschluss | PosTse | (aynı) | Hayır |
| ReportsController | api/Reports | BackofficeManagement | SuperAdmin, Admin, Administrator, Manager | Hayır |
| ModifierGroupsController | api/modifier-groups | PosCatalogRead (class); CatalogManage (write) | (ilgili setler) | Hayır |
| UserManagementController | api/UserManagement | [Authorize], UsersView, UsersManage | (Program.cs) | Hayır |
| SettingsController | api/Settings | [Authorize], BackofficeSettings | (Program.cs) | Hayır |
| MultilingualReceiptController | api/MultilingualReceipt | [Authorize], BackofficeSettings (write) | (Program.cs) | Hayır |
| EntityController (base) | api/{controller} | [Authorize]; DeletePermanent = SystemCritical | (Program.cs) | Hayır |
| AuthController | api/Auth | (login/register public) | — | Hayır |
| UserSettingsController | api/UserSettings | [Authorize] | Authenticated | Hayır |
| TestController | (test) | [AllowAnonymous] | — | — |

---

## 14) Legacy vs yeni endpoint ayrımı

| Yüzey | Route prefix | Durum | Not |
|-------|--------------|--------|-----|
| Legacy Payment | api/Payment | Deprecated (Swagger/dokümantasyon) | Client’lar api/pos/payment’a geçmeli |
| Yeni POS Payment | api/pos/payment | Tercih edilen | Aynı controller |
| Legacy Cart | api/Cart | Deprecated | api/pos/cart tercih edilen |
| Yeni POS Cart | api/pos/cart | Tercih edilen | Aynı controller |
| Legacy Product (POS) | api/Product | Deprecated | api/pos tercih edilen |
| Admin | api/admin/categories, api/admin/products, api/admin/users | Yeni admin yüzeyi | Backoffice |

---

## 15) Kısa analiz

### Backoffice alanları

- **api/admin/*** (categories, products, users): Katalog ve kullanıcı yönetimi; CatalogManage, AdminUsers.
- **CompanySettings, Localization, Settings, MultilingualReceipt, FinanzOnline:** BackofficeSettings (ve kısmen BackofficeManagement).
- **AuditLog** (liste, export, cleanup): UsersView, AuditView, AuditAdmin; cleanup artık HasPermission(AuditCleanup).
- **Reports:** BackofficeManagement.
- **UserManagement:** UsersView, UsersManage.
- **Inventory** (yazma/silme): InventoryManage, InventoryDelete.
- **Invoice** (backfill): SystemCritical.

### Operasyonel POS alanları

- **api/pos/cart, api/pos/payment:** Sepet ve ödeme; PosSales.
- **api/Orders, api/Table, api/Customer:** Sipariş/masa/müşteri; PosTableOrder.
- **api/Receipts:** PosSales.
- **api/Tse, api/Tagesabschluss:** PosTse.
- **api/Product (api/pos):** Katalog okuma PosCatalogRead; yazma CatalogManage.
- **CashRegister** open/close: Sadece [Authorize]; POS operasyon.

### Kritik / high risk endpoint’ler

- **Payment:** cancel, refund, signature-debug, verify-signature.
- **Orders:** PUT status, DELETE (order cancel).
- **CompanySettings:** Tüm PUT.
- **AuditLog:** cleanup (DELETE), export.
- **FinanzOnline:** PutConfig, SubmitInvoice.
- **Invoice:** BackfillFromPayments.
- **EntityController:** DeletePermanent.
- **Inventory:** Adjust, Delete.
- **CartController:** ForceCleanup (ek olarak inline role check; typo Kasiyer).

### Permission migration için ilk adaylar

1. **Zaten permission kullanan:** AuditLog cleanup (HasPermission(AuditCleanup)); Payment CreatePayment (HasPermission(PaymentTake)); Orders UpdateOrderStatus (HasPermission(OrderUpdate)).
2. **Kritik ve hemen permission’a alınacak:** Payment cancel, refund; AuditLog export; CompanySettings tüm PUT; FinanzOnline config, submit-invoice; Invoice backfill; EntityController DeletePermanent; Payment signature-debug, verify-signature.
3. **Inline role kaldırılacak:** CartController force-cleanup (userRole != "Admin" && userRole != "Kasiyer") → permission veya policy.
4. **Sonraki dalga:** Orders (tüm action’lar); Invoice (CRUD, export, credit-note); Inventory (view/manage/delete); CashRegister; Localization, Settings, MultilingualReceipt; UserManagement, AdminUsers; Reports; Categories, AdminProducts, Product, ModifierGroups, Table, Customer, Receipts, Tse, Tagesabschluss.

Bu envanter, AUTHORIZATION_ENDPOINT_AUDIT.md ve AUTHORIZATION_REFACTOR_FILE_AUDIT.md ile birlikte migration planı için tek referans olarak kullanılabilir.
