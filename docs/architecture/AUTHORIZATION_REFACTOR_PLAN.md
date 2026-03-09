# Authorization Refactor Plan: Controller Groups & Role Sets

**Tarih:** 2025-03-09  
**Amaç:** Mevcut role bazlı `[Authorize(Roles = "...")]` kullanımını POS’a uygun, policy tabanlı ve okunabilir bir yapıya dönüştürme planı.

---

## 1) Mevcut kullanım taraması

### 1.1 Nerede kullanılıyor?

| Controller | Kullanım | Not |
|-------------|----------|-----|
| **CategoriesController** | `[Authorize]` + POST/PUT: `SuperAdmin,Administrator,Manager,Admin`; DELETE: `SuperAdmin,Administrator,Admin` | Uzun rol listesi; DELETE daha kısıtlı |
| **AdminUsersController** | `[Authorize(Policy = "AdminUsers")]` | Zaten policy |
| **UserManagementController** | `[Authorize]` + `UsersView` / `UsersManage` | Zaten policy |
| **AuditLogController** | Karışık: `UsersView`, `Administrator,Manager`, `Administrator,Manager,Cashier`, `Administrator` | Tutarsız |
| **AdminProductsController** | `[Authorize]` | Sadece authenticated |
| **ModifierGroupsController** | `[Authorize]` | Sadece authenticated |
| **ProductController** | `[Authorize]` | Sadece authenticated |
| **CartController** | `[Authorize]` | Sadece authenticated |
| **PaymentController** | `[Authorize]` + TSE: `Administrator,Kasiyer`; debug/verify: `Administrator` | **Kasiyer typo** (Identity’de Cashier) |
| **SettingsController** | `[Authorize]` + tüm yazma: `Administrator` | Sadece Administrator |
| **ReceiptsController** | `[Authorize]` | Sadece authenticated |
| **OrdersController** | `[Authorize]` | Sadece authenticated |
| **LocalizationController** | `[Authorize]` + yazma: `Administrator` | Sadece Administrator |
| **MultilingualReceiptController** | `[Authorize]` + yazma/export: `Administrator` | Sadece Administrator |
| **InvoiceController** | `[Authorize]` + backfill: `Admin` | Tek yerde sadece Admin |
| **InventoryController** | `[Authorize]` + yazma: `Administrator,Manager`; DELETE: `Administrator` | |
| **CustomerController** | `[Authorize]` | Sadece authenticated |
| **CompanySettingsController** | `[Authorize]` + çoğu: `Administrator`; banking/billing GET: `Administrator,Manager` | |
| **CashRegisterController** | `[Authorize]` + POST: `Administrator` | |
| **FinanzOnlineController** | `[Authorize]` | Sadece authenticated |
| **TseController** | `[Authorize]` | Sadece authenticated |
| **TagesabschlussController** | `[Authorize]` | Sadece authenticated |
| **ReportsController** | `[Authorize]` | Sadece authenticated |
| **TableController** | Yok | **Yetkisiz** – eklenmeli |
| **BaseController** | `[Authorize]` | Tüm türeyenler authenticated |
| **EntityController** | Kalıcı silme: `Administrator` | |

### 1.2 Administrator vs Admin çakışması

- **Program.cs** yorumu: “Administrator = legacy alias for Admin (backward compatibility)”.
- **Identity / RoleSeedData:** Hem `Administrator` hem `Admin` ayrı roller olarak seed’leniyor.
- **JWT:** Hangisinin token’da gönderildiği login/claim mantığına bağlı; genelde tek rol dönüyor.
- **Sonuç:** Anlam olarak aynı kabul ediliyor; **her policy’de ikisini de kullanmak** güvenli (token’da Administrator veya Admin gelirse ikisi de çalışır). Yeni atamalarda tek isim (örn. Admin) tercih edilebilir; backend’de her zaman `Admin` + `Administrator` birlikte kullanılacak.

---

## 2) Controller grupları ve role setleri

### 2.1 Grup tanımları

| Grup | Açıklama | Örnek controller’lar |
|------|----------|------------------------|
| **Backoffice – Yönetim** | Kullanıcı, rapor, denetim, katalog/ürün/stok yönetimi | UserManagement, AdminUsers, AuditLog, Reports, Categories (yazma), AdminProducts, ModifierGroups, Inventory |
| **Backoffice – Sistem ayarları** | Şirket, vergi, yedekleme, yerelleştirme, fiş şablonu, kasa config, FinanzOnline | Settings, CompanySettings, Localization, MultilingualReceipt, CashRegister, FinanzOnline |
| **POS – Satış / ödeme** | Sepet, ödeme, fiş, TSE imza | Cart, Payment (process + TSE sign), Receipts |
| **POS – Masa / sipariş** | Sipariş, masa, müşteri | Orders, Table, Customer |
| **POS – Katalog okuma** | Ürün/kategori/modifier sadece okuma | Categories GET, Product GET, ModifierGroups GET |
| **POS – Vardiya / TSE** | Tagesabschluss, TSE yönetim | TseController, TagesabschlussController |
| **Sistem – Tehlikeli / admin-only** | Fiş backfill, TSE signature debug, kalıcı silme | Invoice backfill, Payment signature-debug/verify, EntityController hard delete |

### 2.2 Hangi role seti hangi gruba?

| Grup | Policy adı | Roller | Kullanım |
|------|------------|--------|----------|
| Backoffice – Yönetim | **BackofficeManagement** | SuperAdmin, Admin, Administrator, Manager | User list/view, Audit view, Reports, Categories POST/PUT, Products/Modifiers, Inventory (Manager dahil) |
| Backoffice – Sistem ayarları | **BackofficeSettings** | SuperAdmin, Admin, Administrator | Settings, Company, Localization, MultilingualReceipt, CashRegister, FinanzOnline |
| Backoffice – Kullanıcı yönetimi (mevcut) | **UsersView** / **UsersManage** / **AdminUsers** | Mevcut tanım korunur | UserManagement, AdminUsers |
| POS – Satış / ödeme | **PosSales** | Cashier, Manager, Admin, Administrator, SuperAdmin | Cart, Payment (process, get receipt, TSE sign), Receipts list/detail |
| POS – Masa / sipariş | **PosTableOrder** | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Orders, Table, Customer |
| POS – Katalog okuma | **PosCatalogRead** | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin | Categories GET, Product GET, ModifierGroups GET |
| Katalog yazma | **CatalogManage** | SuperAdmin, Admin, Administrator, Manager | Categories POST/PUT/DELETE, AdminProducts, ModifierGroups yazma |
| Stok yazma | **InventoryManage** | SuperAdmin, Admin, Administrator, Manager | InventoryController yazma; DELETE sadece Admin → **InventoryDelete** (SuperAdmin, Admin, Administrator) |
| Denetim görüntüleme | **AuditView** | SuperAdmin, Admin, Administrator, Manager, Cashier (sadece kendi ödemesi) | AuditLog GET’ler; Cashier sadece payment/{id} için |
| Denetim admin (cleanup/export) | **AuditAdmin** | SuperAdmin, Admin, Administrator | AuditLog cleanup, export |
| TSE / vardiya | **PosTse** | Cashier, Manager, Admin, Administrator, SuperAdmin | TseController, TagesabschlussController |
| TSE tanılama | **PosTseDiagnostics** | SuperAdmin, Admin, Administrator | Payment signature-debug, verify-signature |
| Sistem – Kritik | **SystemCritical** | SuperAdmin, Admin, Administrator | Invoice backfill, EntityController kalıcı silme |
| Kasa kaydı oluşturma | **CashRegisterManage** | SuperAdmin, Admin, Administrator | CashRegisterController POST |

### 2.3 Özet tablo: Policy → Roller

| Policy | Roller |
|--------|--------|
| BackofficeManagement | SuperAdmin, Admin, Administrator, Manager |
| BackofficeSettings | SuperAdmin, Admin, Administrator |
| PosSales | Cashier, Manager, Admin, Administrator, SuperAdmin |
| PosTableOrder | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin |
| PosCatalogRead | Waiter, Cashier, Manager, Admin, Administrator, SuperAdmin |
| CatalogManage | SuperAdmin, Admin, Administrator, Manager |
| InventoryManage | SuperAdmin, Admin, Administrator, Manager |
| InventoryDelete | SuperAdmin, Admin, Administrator |
| AuditView | SuperAdmin, Admin, Administrator, Manager |
| AuditViewWithCashier | SuperAdmin, Admin, Administrator, Manager, Cashier |
| AuditAdmin | SuperAdmin, Admin, Administrator |
| PosTse | Cashier, Manager, Admin, Administrator, SuperAdmin |
| PosTseDiagnostics | SuperAdmin, Admin, Administrator |
| SystemCritical | SuperAdmin, Admin, Administrator |
| CashRegisterManage | SuperAdmin, Admin, Administrator |
| UsersView / UsersManage / AdminUsers | (mevcut – değişmez) |

---

## 3) Refactor önerisi

### 3.1 Güvenli ve okunabilir yaklaşım

- **Tekrarlayan rol listelerini kaldır:** Controller’larda `[Authorize(Roles = "SuperAdmin,Administrator,Manager,Admin")]` yerine tek bir policy adı kullan: `[Authorize(Policy = "BackofficeManagement")]`.
- **Tek kaynak:** Tüm rol setleri yalnızca **Program.cs** (veya ileride permission matrisi) içinde tanımlansın. Değişiklik tek yerde yapılır.
- **Anlamlı isimler:** Policy adı amacı anlatsın (BackofficeSettings, PosSales, PosTableOrder) böylece hangi endpoint’in hangi iş için olduğu belli olsun.
- **Kasiyer düzeltmesi:** PaymentController’da `Kasiyer` → policy **PosSales** veya **PosTse** (Cashier dahil); böylece Identity’deki `Cashier` rolü çalışır.

### 3.2 Controller bazlı değişiklik listesi

| Controller | Mevcut | Yapılacak |
|------------|--------|-----------|
| CategoriesController | Roles POST/PUT/DELETE | GET: `PosCatalogRead` (veya sadece `[Authorize]` + alt action’da policy); POST/PUT: `CatalogManage`; DELETE: `CatalogManage` (veya Admin-only policy – şu an Admin + SuperAdmin) |
| AuditLogController | UsersView, Roles karışık | GET list/user/suspicious: `UsersView` veya `AuditView`; GET by id/correlation/transaction/statistics: `AuditView`; GET payment: `AuditViewWithCashier`; cleanup/export: `AuditAdmin` |
| PaymentController | Roles Administrator,Kasiyer / Administrator | TSE sign: `PosTse` (Cashier dahil); signature-debug, verify: `PosTseDiagnostics` |
| SettingsController | Roles Administrator | Tüm yazma/export: `BackofficeSettings` |
| LocalizationController | Roles Administrator | Tüm yazma/export: `BackofficeSettings` |
| MultilingualReceiptController | Roles Administrator | Tüm yazma/export: `BackofficeSettings` |
| CompanySettingsController | Roles Administrator / Administrator,Manager | Yazma/export: `BackofficeSettings`; banking/billing GET: `BackofficeManagement` veya ayrı policy (Manager görebilsin) |
| CashRegisterController | Roles Administrator | POST: `CashRegisterManage`; GET: `BackofficeSettings` veya `PosSales` (kasa bilgisi POS’ta da gerekebilir) |
| InventoryController | Roles Administrator,Manager / Administrator | POST/PUT/restock/adjust: `InventoryManage`; DELETE: `InventoryDelete` veya mevcut Administrator |
| InvoiceController | Roles Admin | backfill: `SystemCritical` |
| EntityController | Roles Administrator | Kalıcı silme: `SystemCritical` |
| CartController | Authorize | `PosSales` |
| OrdersController | Authorize | `PosTableOrder` |
| ReceiptsController | Authorize | `PosSales` |
| ProductController | Authorize | GET: `PosCatalogRead` veya `[Authorize]` |
| AdminProductsController | Authorize | Tümü: `CatalogManage` |
| ModifierGroupsController | Authorize | GET: `PosCatalogRead`; POST/PUT/DELETE: `CatalogManage` |
| CustomerController | Authorize | `PosTableOrder` |
| TableController | Yok | `[Authorize]` + `PosTableOrder` |
| TseController | Authorize | `PosTse` |
| TagesabschlussController | Authorize | `PosTse` |
| ReportsController | Authorize | `BackofficeManagement` (veya ayrı ReportsView: Manager, Admin, SuperAdmin) |
| FinanzOnlineController | Authorize | `BackofficeSettings` |

### 3.3 Uzun vade: Permission bazlı policy

- **Hedef:** Policy’ler rol listesi yerine **permission** kontrolüne geçsin (örn. `report.view`, `settings.manage`). Rol → permission matrisi `AUTHORIZATION_ROLE_PERMISSION_DESIGN.md` içinde.
- **Adımlar:**  
  1. Permission sabitleri + statik RolePermission matrix (backend).  
  2. `PermissionRequirement` + `PermissionAuthorizationHandler` ekle.  
  3. Her permission için bir policy (örn. `Permission:report.view`).  
  4. Controller’larda `[Authorize(Policy = "Permission:report.view")]` kullan.  
  5. İsteğe bağlı: Permission ve RolePermission tabloları + DB’den okuma.
- **Mevcut refactor ile uyum:** Şu an yapılan policy isimleri (BackofficeSettings, PosSales, …) ileride “bu policy şu permission’ları gerektirir” şeklinde eşlenebilir; controller’da sadece policy adı değişmez, arka planda permission kontrolüne geçilir.

---

## 4) Çıktı özeti

1. **Controller grupları:** Backoffice (Yönetim, Sistem ayarları), POS (Satış/ödeme, Masa/sipariş, Katalog okuma, TSE/vardiya), Sistem (Kritik), mevcut Users (UsersView/UsersManage/AdminUsers).
2. **Role setleri:** Her grup için yukarıdaki policy adı ve rol listesi (Bölüm 2.2–2.3) kullanılacak; Administrator ve Admin her ikisi de birlikte tanımlı.
3. **Refactor:** Tüm rol listelerini Program.cs’teki policy’lere taşı; controller’larda yalnızca `[Authorize(Policy = "PolicyName")]` kullan; Kasiyer → Cashier (policy ile); TableController’a `[Authorize]` + `PosTableOrder` ekle. Uzun vadede permission tabanlı policy’ye geçiş planı Bölüm 3.3’te.
