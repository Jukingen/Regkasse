# Authorization Migration – Endpoint Taraması ve Tablo

**Tarih:** 2025-03-09  
**Bağlam:** Role + permission migration; Administrator = legacy alias (hedef rol Admin); backoffice vs POS ayrımı; minimum kırılımlı geçiş.

---

## 1) Endpoint grupları – tam tablo

Aşağıdaki tabloda: **Controller**, **Action** (route template veya anlamı), **Route** (tam prefix), **HttpMethod**, **ExistingAuthorizeAttribute**, **ExistingRoles** (policy’nin gerektirdiği roller – Program.cs’ten), **DeprecatedMi**, **Notes**.

### /api/admin/*

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| CategoriesController | GetCategories | api/admin/categories | GET | Policy = PosCatalogRead | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | Katalog okuma |
| CategoriesController | GetCategory | api/admin/categories/{id} | GET | (class) PosCatalogRead | (aynı) | Hayır | |
| CategoriesController | CreateCategory | api/admin/categories | POST | Policy = CatalogManage | SuperAdmin, Admin, Administrator, Manager | Hayır | Katalog yazma |
| CategoriesController | UpdateCategory | api/admin/categories/{id} | PUT | Policy = CatalogManage | (aynı) | Hayır | |
| CategoriesController | DeleteCategory | api/admin/categories/{id} | DELETE | Policy = CatalogManage | (aynı) | Hayır | |
| CategoriesController | GetCategoryProducts | api/admin/categories/{id}/products | GET | (class) PosCatalogRead | (aynı) | Hayır | |
| CategoriesController | Search | api/admin/categories/search | GET | (class) PosCatalogRead | (aynı) | Hayır | |
| AdminProductsController | Get (list) | api/admin/products | GET | (class) CatalogManage | SuperAdmin, Admin, Administrator, Manager | Hayır | Tüm endpoint’ler CatalogManage |
| AdminProductsController | Get(id) | api/admin/products/{id} | GET | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | Search | api/admin/products/search | GET | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | Create | api/admin/products | POST | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | Update | api/admin/products/{id} | PUT | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | PutStock | api/admin/products/stock/{id} | PUT | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | Delete | api/admin/products/{id} | DELETE | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | GetModifierGroups | api/admin/products/{id}/modifier-groups | GET | (class) CatalogManage | (aynı) | Hayır | |
| AdminProductsController | PostModifierGroups | api/admin/products/{id}/modifier-groups | POST | (class) CatalogManage | (aynı) | Hayır | |
| AdminUsersController | Get, Get(id), Post, Patch, Deactivate, Reactivate, ForcePasswordReset, GetActivity | api/admin/users | GET/POST/PATCH | Policy = AdminUsers | SuperAdmin, Admin, Administrator | Hayır | Backoffice kullanıcı yönetimi |

### /api/pos/* (ve legacy Cart/Payment)

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| CartController | GetCurrent, Get(cartId), Post, AddItem, Put items, Delete, Clear, Complete, … | api/Cart, api/pos/cart | GET/POST/PUT/DELETE | Policy = PosSales | Cashier, Manager, Admin, Administrator, SuperAdmin | api/Cart evet | İki route; pos/cart tercih |
| PaymentController | GetMethods, Post, Get(id), GetReceipt, Cancel, Refund, Statistics, TseSignature, SignatureDebug, VerifySignature | api/Payment, api/pos/payment | GET/POST | Class: PosSales; TSE sign: PosTse; debug/verify: PosTseDiagnostics | PosSales: Cashier+; PosTseDiagnostics: SuperAdmin, Admin, Administrator | api/Payment evet | Aşağıda ayrı açıklama |
| ProductController | Get, List, Catalog, Categories, Search, … (GET’ler) | api/Product, api/pos | GET | Policy = PosCatalogRead | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | api/Product path evet | POS katalog okuma; yazma aynı controller’da |

### Orders

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| OrdersController | Get, Get(id), Post, PutStatus, Delete, GetByStatus | api/Orders | GET/POST/PUT/DELETE | Policy = PosTableOrder | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | Masa/sipariş akışı |

### Payment (detay)

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| PaymentController | GetMethods | api/Payment, api/pos/payment | GET | PosSales | Cashier, Manager, Admin, Administrator, SuperAdmin | Payment path evet | |
| PaymentController | Post (create) | api/Payment, api/pos/payment | POST | PosSales | (aynı) | Payment path evet | |
| PaymentController | Get(id), GetReceipt, GetStatistics, GetDateRange, GetQr | api/Payment, api/pos/payment | GET | PosSales | (aynı) | Payment path evet | |
| PaymentController | PostCancel | api/Payment/{id}/cancel, api/pos/payment/{id}/cancel | POST | PosSales | (aynı) | Payment path evet | Kritik: iptal |
| PaymentController | PostRefund | api/Payment/{id}/refund, api/pos/payment/{id}/refund | POST | PosSales | (aynı) | Payment path evet | Kritik: iade |
| PaymentController | PostTseSignature | api/Payment/{id}/tse-signature, api/pos/payment/… | POST | PosTse | Cashier, Manager, Admin, Administrator, SuperAdmin | Payment path evet | |
| PaymentController | GetSignatureDebug | api/Payment/{id}/signature-debug, api/pos/… | GET | PosTseDiagnostics | SuperAdmin, Admin, Administrator | Payment path evet | Kritik: tanılama |
| PaymentController | PostVerifySignature | api/Payment/verify-signature, api/pos/… | POST | PosTseDiagnostics | (aynı) | Payment path evet | Kritik |

### Invoice

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| InvoiceController | GetList, GetPosList, Get, Get(id), Export, Search, GetByStatus | api/Invoice | GET | [Authorize] | Authenticated | Hayır | |
| InvoiceController | Post, Put, Delete, Duplicate, CreditNote, GetPdf | api/Invoice | POST/PUT/DELETE/GET | [Authorize] | (aynı) | Hayır | |
| InvoiceController | BackfillFromPayments | api/Invoice/backfill-from-payments | POST | Policy = SystemCritical | SuperAdmin, Admin, Administrator | Hayır | Kritik |

### Inventory

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| InventoryController | GetInventory, Get(id), GetLowStock, GetTransactions | api/Inventory | GET | [Authorize] | Authenticated | Hayır | Class-level Authorize |
| InventoryController | Create | api/Inventory | POST | Policy = InventoryManage | SuperAdmin, Admin, Administrator, Manager | Hayır | |
| InventoryController | Update, Restock, Adjust | api/Inventory/{id}, …/restock, …/adjust | PUT/POST | Policy = InventoryManage | (aynı) | Hayır | |
| InventoryController | Delete | api/Inventory/{id} | DELETE | Policy = InventoryDelete | SuperAdmin, Admin, Administrator | Hayır | |

### CompanySettings

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| CompanySettingsController | Get | api/CompanySettings | GET | [Authorize] | Authenticated | Hayır | |
| CompanySettingsController | Put (main) | api/CompanySettings | PUT | Policy = BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | Kritik |
| CompanySettingsController | Get/Put business-hours, banking, localization, billing | api/CompanySettings/… | GET/PUT | BackofficeSettings veya BackofficeManagement (GET banking/billing) | (aynı) / Manager (GET) | Hayır | |
| CompanySettingsController | GetExport | api/CompanySettings/export | GET | BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | |

### Localization

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| LocalizationController | Get, GetLanguages, GetCurrencies, … (GET’ler) | api/Localization | GET | [Authorize] | Authenticated | Hayır | |
| LocalizationController | Put, AddLanguage, AddCurrency, RemoveLanguage, RemoveCurrency, Export | api/Localization | PUT/POST/DELETE/GET | Policy = BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | |

### AuditLog

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| AuditLogController | Get (list) | api/AuditLog | GET | Policy = UsersView | SuperAdmin, Admin, Administrator, BranchManager, Auditor | Hayır | |
| AuditLogController | Get(id) | api/AuditLog/{id} | GET | Policy = AuditView | SuperAdmin, Admin, Administrator, Manager | Hayır | |
| AuditLogController | GetPayment | api/AuditLog/payment/{paymentId} | GET | Policy = AuditViewWithCashier | + Cashier | Hayır | |
| AuditLogController | GetUser, GetCorrelation, GetSuspicious, GetTransaction, GetStatistics | api/AuditLog/… | GET | UsersView veya AuditView | (yukarıdaki setler) | Hayır | |
| AuditLogController | DeleteCleanup | api/AuditLog/cleanup | DELETE | Policy = AuditAdmin | SuperAdmin, Admin, Administrator | Hayır | Kritik |
| AuditLogController | GetExport | api/AuditLog/export | GET | Policy = AuditAdmin | (aynı) | Hayır | Kritik |

### FinanzOnline

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| FinanzOnlineController | GetConfig, PutConfig, GetStatus, GetErrors, GetHistory | api/FinanzOnline | GET/PUT | Policy = BackofficeSettings | SuperAdmin, Admin, Administrator | Hayır | Config kritik |
| FinanzOnlineController | SubmitInvoice | api/FinanzOnline/submit-invoice | POST | (class) BackofficeSettings | (aynı) | Hayır | Kritik |
| FinanzOnlineController | TestConnection | api/FinanzOnline/test-connection | POST | (class) BackofficeSettings | (aynı) | Hayır | |

### CashRegister

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| CashRegisterController | Get, Get(id), GetTransactions | api/CashRegister | GET | [Authorize] | Authenticated | Hayır | |
| CashRegisterController | Post (create) | api/CashRegister | POST | Policy = CashRegisterManage | SuperAdmin, Admin, Administrator | Hayır | |
| CashRegisterController | Open, Close | api/CashRegister/{id}/open, close | POST | (class) [Authorize] | Authenticated | Hayır | POS operasyon |

### Diğer (Reports, Tse, Tagesabschluss, Receipts, Table, Customer, ModifierGroups)

| Controller | Action | Route | HttpMethod | ExistingAuthorizeAttribute | ExistingRoles | DeprecatedMi | Notes |
|------------|--------|-------|------------|----------------------------|---------------|--------------|--------|
| ReportsController | GetSales, GetProducts, GetCustomers, GetInventory, GetPayments, ExportSales | api/Reports | GET | Policy = BackofficeManagement | SuperAdmin, Admin, Administrator, Manager | Hayır | |
| TseController | GetStatus, Connect, Signature, Disconnect, GetDevices | api/Tse | GET/POST | Policy = PosTse | Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | |
| TagesabschlussController | PostDaily, PostMonthly, PostYearly, GetHistory, GetCanClose, GetStatistics | api/Tagesabschluss | GET/POST | Policy = PosTse | (aynı) | Hayır | |
| ReceiptsController | Get, CreateFromPayment, GetSignatureDebug | api/Receipts | GET/POST | Policy = PosSales | PosSales set | Hayır | |
| TableController | Get, Get(id), PostStatus | api/Table | GET/POST | Policy = PosTableOrder | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Hayır | |
| CustomerController | Get, Post, Put, Search | api/Customer | GET/POST/PUT | Policy = PosTableOrder | (aynı) | Hayır | EntityController türevi |
| ModifierGroupsController | GET/POST/PUT/DELETE + products | api/modifier-groups | GET/POST/PUT/DELETE | Class: PosCatalogRead; write: CatalogManage | (yukarıdaki setler) | Hayır | |
| UserManagementController | Get, Get(id), Post, Put, Deactivate, Reactivate, ResetPassword, Delete, GetRoles, PostRoles | api/UserManagement | GET/POST/PUT/DELETE | UsersView / UsersManage | UsersView: + BranchManager, Auditor; UsersManage: + BranchManager | Hayır | |
| SettingsController | Get, Put, TaxRates, Backup, Notifications, Export | api/Settings | GET/POST/PUT | BackofficeSettings (yazma) | SuperAdmin, Admin, Administrator | Hayır | |
| MultilingualReceiptController | Get, Post, Put, Delete, Export | api/MultilingualReceipt | GET/POST/PUT/DELETE | BackofficeSettings (yazma) | (aynı) | Hayır | |
| EntityController (base) | Get, Get(id), Post, Put, Delete, DeletePermanent | api/{controller} | GET/POST/PUT/DELETE | [Authorize]; DeletePermanent: SystemCritical | Kalıcı silme: SuperAdmin, Admin, Administrator | Hayır | Customer vb. türevleri kullanır |

---

## 2) Legacy /api/Payment ile yeni /api/pos/payment ayrımı

### Tek controller, iki route

`PaymentController` hem **api/[controller]** hem **api/pos/payment** ile kayıtlı:

```csharp
[Route("api/[controller]")]   // → api/Payment
[Route("api/pos/payment")]    // → api/pos/payment
[Authorize(Policy = "PosSales")]
public class PaymentController
```

Aynı action’lar her iki prefix’ten de erişilebilir; örn. `POST api/Payment` ve `POST api/pos/payment` aynı metoda düşer.

### Neden “legacy” vs “yeni”?

- **Swagger / dokümantasyon:** Eski yüzey `api/Cart`, `api/Payment`; yeni POS yüzeyi `api/pos/cart`, `api/pos/payment` olarak tanımlanıyor. Client’ların yeni prefix’e geçmesi hedefleniyor.
- **Kod tarafı:** Şu an tek controller, tek yetkilendirme (policy). Route değişince yetki değişmez; sadece URL farkı.
- **Deprecated anlamı:** `api/Payment` ve `api/Cart` ileride kaldırılabilir; client’lar `api/pos/payment` ve `api/pos/cart` kullanacak. Migration sırasında yetki davranışı aynı kalır; sadece tercih edilen route değişir.

### Özet

| Yüzey | Route prefix | Durum | Not |
|-------|--------------|--------|-----|
| Legacy | api/Payment, api/Cart | Deprecated (dokümanda) | Mevcut client’lar kullanıyor; kaldırma client geçişine bağlı |
| Yeni POS | api/pos/payment, api/pos/cart | Tercih edilen | Yeni entegrasyonlar bu prefix’i kullanmalı |

Authorization migration’da iki route’u ayırmaya gerek yok; policy controller seviyesinde (ve action seviyesinde) tanımlı. Route’a göre farklı policy uygulanmıyor.

---

## 3) Kısa analiz

### Kritik endpoint’ler (yüksek risk – önce permission ile korunmalı)

| Endpoint / aksiyon | Sebep |
|--------------------|--------|
| POST api/Payment/{id}/refund, api/pos/payment/{id}/refund | İade; mali/fiscal etki |
| POST api/Payment/{id}/cancel, api/pos/payment/{id}/cancel | Ödeme iptali |
| DELETE api/AuditLog/cleanup | Denetim verisi silme |
| GET api/AuditLog/export | Denetim verisi dışa aktarma |
| PUT api/CompanySettings (tüm yazma) | Şirket ayarları değişimi |
| PUT api/FinanzOnline/config, POST api/FinanzOnline/submit-invoice | FinanzOnline config ve fatura gönderimi |
| POST api/Invoice/backfill-from-payments | Toplu backfill |
| DELETE api/*/{id}/permanent (EntityController) | Kalıcı silme |
| GET api/Payment/{id}/signature-debug, POST api/Payment/verify-signature | TSE tanılama (admin) |

### Permission migration için ilk aday endpoint’ler

1. **Zaten policy kullanıyor; permission’a 1:1 eşlenebilir (policy adı → permission):**
   - Payment refund → `refund.create`
   - Payment cancel → `payment.cancel` (sabit eklenmeli)
   - AuditLog cleanup/export → `audit.admin`
   - CompanySettings PUT → `settings.manage`
   - FinanzOnline config/submit → `settings.manage` (veya `finanzonline.submit`)
   - Invoice backfill → `system.critical` veya `settings.manage`
   - EntityController permanent delete → `system.critical`
   - Payment signature-debug/verify → `system.critical` veya `settings.manage`

2. **Class-level [Authorize] veya tek policy; action bazlı permission’a bölünebilir:**
   - Inventory: GET → `inventory.view`; POST/PUT/restock/adjust → `inventory.manage`; DELETE → `inventory.delete`
   - Invoice: list/get/export → `sale.view` veya `invoice.view`; backfill zaten SystemCritical
   - CashRegister: POST (create) → `cashregister.manage` veya `settings.manage`; open/close → `cashdrawer.open` / shift

3. **Backoffice admin/* ve Reports/Audit/UserManagement:** Zaten anlamlı policy (CatalogManage, BackofficeManagement, UsersView, UsersManage, AuditView, AuditAdmin). Bunlar permission sabitleriyle eşlenip `[RequirePermission(Permissions.X)]` ile değiştirilebilir; rol seti matristen gelsin.

### Özet

- **Kritik:** Refund, cancel, audit cleanup/export, CompanySettings/FinanzOnline yazma, Invoice backfill, kalıcı silme, TSE tanılama.
- **İlk sprint adayları:** Yukarıdaki kritik aksiyonlar + Inventory/Invoice/CashRegister’da action bazlı permission ayrımı.
- **Legacy route:** api/Payment ve api/Cart deprecated; yetki davranışı api/pos/* ile aynı; migration’da route’a özel ek işlem gerekmez.
