# Authorization: Mevcut Durum Analizi ve POS Hedef Mimarisi

**Tarih:** 2025-03-09  
**Amaç:** Backend authorization yapısını POS ihtiyaçlarına göre analiz etmek, hedef rol mimarisini ve migration planını ortaya koymak.

---

## 1) Mevcut Sorunlar

### 1.1 Rol dağınıklığı ve tutarsızlık

| Sorun | Detay | Etkilenen yerler |
|-------|--------|------------------|
| **İsim karışıklığı** | Backend’de hem `Administrator` hem `Admin` kullanılıyor; Program.cs’te “Administrator = legacy alias” deniyor ama controller’larda ikisi de virgülle listeleniyor. | Tüm `[Authorize(Roles = "…")]` kullanan controller’lar |
| **Kasiyer typo** | `PaymentController.cs` satır 490: `Roles = "Administrator,Kasiyer"` — Identity’de rol adı `Cashier`, `Kasiyer` yok. Bu endpoint Cashier kullanıcıya kapalı kalıyor. | `backend/Controllers/PaymentController.cs` |
| **Kellner vs Waiter** | Seed’de rol `Kellner` (Almanca); dokümantasyon ve hedef listede `Waiter` isteniyor. Backend’de Kellner hiçbir endpoint’te kullanılmıyor. | `RoleSeedData.cs`, POS senaryoları |
| **Policy vs Roles karışımı** | UserManagement ve AuditLog policy kullanıyor; Categories, Payment, Settings, CompanySettings, Inventory, AuditLog’un bir kısmı doğrudan `Roles = "…"` kullanıyor. Tekil policy isimleri yok, bakım zor. | Tüm backend controller’lar |

### 1.2 Backoffice odaklı, POS rollerinin eksik kullanımı

- **CategoriesController:** Sadece `SuperAdmin, Administrator, Manager, Admin` — Cashier/Waiter yok (katalog sadece okuma için POS’a açılabilir).
- **CartController, ProductController, OrdersController, ReceiptsController, TseController, TagesabschlussController:** Sadece `[Authorize]` (authenticated). Hangi operasyonel rolün ne yapabileceği tanımlı değil.
- **PaymentController:** TSE imza `Administrator,Kasiyer` (Kasiyer typo); signature-debug ve başka admin-only endpoint’ler sadece `Administrator`.
- **AuditLogController:** Bazı endpoint’ler `Administrator,Manager,Cashier` (örn. satır 114), çoğu `Administrator,Manager`; Cashier sadece tek yerde.
- **InventoryController:** Tüm kısıtlamalar `Administrator,Manager` veya sadece `Administrator` — Cashier/Waiter yok.
- **ReportsController:** Sadece `[Authorize]` — raporları kimin göreceği rol bazlı net değil.

### 1.3 Frontend – backend rol uyumsuzluğu

| Katman | Roller | Not |
|--------|--------|-----|
| **Backend RoleSeedData** | Administrator, Admin, Cashier, Kellner, Auditor, Demo, Manager, BranchManager, SuperAdmin | Kellner var, Waiter yok |
| **Backend Policies (Program.cs)** | SuperAdmin, Admin, Administrator, BranchManager, Auditor | UsersView / UsersManage |
| **FE-Admin users sayfası ROLE_OPTIONS** | SuperAdmin, Admin, BranchManager, Auditor | Cashier, Manager, Kellner/Waiter yok; atanabilir roller eksik |
| **POS frontend (auth.ts, PermissionHelper)** | Admin, Cashier, Manager | Waiter/Kitchen/Accountant yok; resource/action matrisi sadece bu 3 rol için |

### 1.4 Özet: Kritik hatalar

1. **PaymentController TSE imza:** `Kasiyer` → `Cashier` olmalı (aksi halde Cashier bu endpoint’e hiç erişemiyor).
2. **POS operasyonel roller:** Waiter (Kellner), isteğe bağlı Kitchen/Accountant backend’de policy/role kullanımında yok.
3. **Tekil policy isimleri yok:** “CatalogEdit”, “PosSales”, “ReportsView”, “AuditView” gibi anlamlı policy’ler yok; her yerde uzun rol listesi tekrarlanıyor.
4. **FE-Admin’de atanabilir roller:** Sadece 4 rol görünüyor; Cashier/Manager/Waiter atanamıyor (veya bilinçli kısıt mı net değil).

---

## 2) Modül sınıflandırması: Backoffice vs POS operasyonel

### 2.1 Backoffice (Admin / raporlama / yönetim)

Sadece Admin/Manager/SuperAdmin (ve gerekiyorsa Auditor/BranchManager) erişsin:

| Modül | Controller | Önerilen erişim |
|-------|------------|------------------|
| User management | UserManagementController, AdminUsersController | Policy: UsersView, UsersManage (mevcut) |
| Audit log (tam) | AuditLogController | Policy: AuditView / AuditExport (Admin, Manager, Auditor) |
| Company / System settings | SettingsController, CompanySettingsController | Admin (veya Policy: SettingsManage) |
| Localization CRUD | LocalizationController | Admin |
| Multilingual receipt templates | MultilingualReceiptController | Admin |
| FinanzOnline | FinanzOnlineController | Admin |
| Cash register config | CashRegisterController | Admin |
| Inventory (stok girişi / düzeltme) | InventoryController | Admin, Manager (mevcut mantık korunabilir) |
| Invoice backfill | InvoiceController.BackfillInvoicesFromPayments | Admin (mevcut) |
| TSE signature debug | PaymentController.GetSignatureDebug | Admin |
| Entity soft-delete / kalıcı silme | EntityController, BaseController | Admin (mevcut) |
| Reports (tam raporlar) | ReportsController | Admin, Manager; isteğe bağlı Accountant/ReportViewer |

### 2.2 POS operasyonel (Cart → Order → Payment → Receipt → Tagesabschluss)

Authenticated + operasyonel roller (Cashier, Waiter, Manager, Admin):

| Modül | Controller | Mevcut | Önerilen |
|-------|------------|--------|----------|
| Katalog okuma | CategoriesController GET, ProductController GET, ModifierGroupsController GET | Çoğu [Authorize] veya admin rolleri | Policy: `PosCatalogView` → Cashier, Waiter, Manager, Admin, SuperAdmin |
| Katalog yazma | CategoriesController POST/PUT/DELETE, AdminProductsController, ModifierGroupsController | Admin/Manager rolleri | Policy: `CatalogManage` → Manager, Admin, SuperAdmin |
| Sepet / Sipariş | CartController, OrdersController | [Authorize] | Policy: `PosSales` → Cashier, Waiter, Manager, Admin, SuperAdmin |
| Ödeme / Fiş | PaymentController (process, get receipt, TSE imza) | [Authorize] + bir endpoint Kasiyer (typo) | Policy: `PosPayment` → Cashier, Waiter, Manager, Admin, SuperAdmin; TSE imza aynı |
| Fiş listesi / detay | ReceiptsController | [Authorize] | Policy: `PosReceiptsView` → Cashier, Waiter, Manager, Admin, SuperAdmin |
| TSE / Tagesabschluss | TseController, TagesabschlussController | [Authorize] | Policy: `PosTse` / `PosDailyClosing` → Cashier, Manager, Admin, SuperAdmin (Waiter sadece okuma/yardım için açılabilir) |
| Müşteri (POS) | CustomerController | [Authorize] | Policy: `PosSales` veya `PosCustomerView` |
| Tablolar | TableController | [Authorize] | Policy: `PosSales` (Waiter özellikle) |

### 2.3 Hibrit (hem backoffice hem POS’a göre endpoint ayır)

| Controller | Backoffice endpoint’ler | POS endpoint’ler |
|------------|-------------------------|-------------------|
| AuditLogController | UsersView, AuditExport, çoğu admin-only | Kendi işlemlerini görme: Cashier (zaten bir yerde var) |
| InventoryController | Stok girişi: Admin, Manager | Stok görüntüleme (varsa): Cashier, Manager |
| ReportsController | Tüm raporlar: Admin, Manager, ReportViewer | Basit satış özeti (varsa): Cashier, Manager |

---

## 3) Önerilen hedef mimari

### 3.1 Hedef roller (canonical – İngilizce, kod/jwt)

| Rol | Açıklama | Kullanım |
|-----|----------|----------|
| **SuperAdmin** | Tüm yetkiler, sistem ayarları, kullanıcı/rol yönetimi | Backoffice + POS |
| **Admin** | Şube/sistem yönetimi, kullanıcı yönetimi, raporlar, ayarlar | Backoffice + POS (Administrator ile eşleştirilebilir) |
| **Manager** | Raporlar, denetim, stok, personel; katalog düzenleme | Backoffice + POS |
| **Cashier** | Satış, ödeme, fiş, günlük kapanış (TSE dahil) | POS |
| **Waiter** | Sipariş, masa, ödeme (fiş alma); fiş imzalama yetkisi iş kuralına bırakılabilir | POS (restoran) |
| **Kitchen** (opsiyonel) | Sipariş görüntüleme / mutfak ekranı | POS |
| **Accountant** / **ReportViewer** (opsiyonel) | Rapor görüntüleme, denetim; değişiklik yok | Backoffice |

**Geriye uyumluluk:** Identity’de `Administrator` rolü kalsın; JWT’te veya policy’de “Admin” ile eşleştir (zaten Program.cs’te ikisi birlikte kullanılıyor). İsterseniz login/token tarafında Administrator → Admin normalizasyonu yapılabilir.

**Kellner → Waiter:** Hedef rol adı **Waiter**. Seed’de `Kellner` kalabilir (display name “Kellner” / “Waiter”) veya yeni kullanıcılar için `Waiter` seed’lenir, mevcut Kellner kullanıcılar migration’da Waiter’a taşınır.

### 3.2 Role-only mı, Role + Permission mı?

**Öneri: Kısa–orta vadede Role + Policy (permission’ı policy adıyla temsil), uzun vadede isteğe bağlı permission tablosu.**

- **Şu an:** Backend’de permission tablosu yok; tüm yetki rol ve policy ile. Bu yapı POS için yeterli:
  - Policy = “izin kümesi” (örn. `PosPayment`, `CatalogManage`, `UsersView`).
  - Her policy, sabit roller listesi ile tanımlı (RequireRole).
- **Avantaj:** Minimum kırılım, mevcut Identity rolleriyle uyumlu, test ve dokümantasyon kolay.
- **İleride (opsiyonel):** Kullanıcı bazlı “şu rol + ekstra permission” veya “branch bazlı kısıtlama” gerekirse:
  - Permission tablosu (Resource, Action) + RolePermission + UserPermission
  - Policy’ler `IAuthorizationHandler` ile veritabanından okur
  - Migration planında “Fazla 2” olarak bırakılabilir.

**Sonuç:** Önce **role + named policy** ile tüm endpoint’leri netleştir; permission tablosu ihtiyaç doğdukça eklenir.

### 3.3 Hedef policy seti (backend)

Aşağıdaki policy’ler `Program.cs` içinde tanımlanır; controller’lar `[Authorize(Policy = "…")]` kullanır.

| Policy adı | Roller | Kullanım yeri |
|------------|--------|-----------------|
| **SuperAdminOnly** | SuperAdmin | Kritik sistem ayarları (isteğe bağlı) |
| **AdminUsers** | SuperAdmin, Admin, Administrator | Mevcut; AdminUsersController |
| **UsersView** | SuperAdmin, Admin, Administrator, BranchManager, Auditor | Mevcut; UserManagement, AuditLog (user activity) |
| **UsersManage** | SuperAdmin, Admin, Administrator, BranchManager | Mevcut |
| **CatalogView** | SuperAdmin, Admin, Manager, Cashier, Waiter | Kategori/ürün/modifier GET |
| **CatalogManage** | SuperAdmin, Admin, Manager | Kategori/ürün/modifier POST/PUT/DELETE |
| **PosSales** | SuperAdmin, Admin, Manager, Cashier, Waiter | Cart, Orders, Table, Customer (POS) |
| **PosPayment** | SuperAdmin, Admin, Manager, Cashier, Waiter | Payment process, get receipt, TSE imza (fiş) |
| **PosReceiptsView** | SuperAdmin, Admin, Manager, Cashier, Waiter | Receipts list/detail |
| **PosTse** | SuperAdmin, Admin, Manager, Cashier | TSE işlemleri, Tagesabschluss (Waiter iş kuralına göre çıkarılabilir) |
| **AuditView** | SuperAdmin, Admin, Manager, Auditor, (Cashier kendi logları) | AuditLog endpoint’leri |
| **ReportsView** | SuperAdmin, Admin, Manager, Accountant? | ReportsController |
| **SettingsManage** | SuperAdmin, Admin, Administrator | Settings, CompanySettings, Localization, MultilingualReceipt, CashRegister, FinanzOnline |
| **InventoryManage** | SuperAdmin, Admin, Manager | InventoryController yazma |
| **InventoryView** | SuperAdmin, Admin, Manager, Cashier? | InventoryController okuma (isteğe bağlı) |

Yeni policy’ler eklendikçe `ForbiddenResponseAuthorizationHandler` otomatik olarak policy adını 403’te döner (mevcut yapı yeterli).

---

## 4) Migration planı (minimum kırılım)

### Faz 1 – Acil düzeltmeler (1–2 gün)

1. **PaymentController – Kasiyer typo**
   - `[Authorize(Roles = "Administrator,Kasiyer")]` → `[Authorize(Roles = "Administrator,Admin,Cashier")]` veya hemen policy’e geç: `[Authorize(Policy = "PosPayment")]`.
   - Dosya: `backend/Controllers/PaymentController.cs` (satır 490).

2. **Rol listesi dokümantasyonu**
   - `docs/architecture/` altında “Canonical role names” kısa tablo: Backend/JWT’de kullanılacak tek liste (SuperAdmin, Admin, Manager, Cashier, Waiter, Auditor, BranchManager, Demo; opsiyonel Kitchen, Accountant).
   - FE-Admin ROLE_OPTIONS ile backend’in döndüğü rol listesini senkronize et (backend’den roles endpoint’i varsa kullan).

### Faz 2 – Policy’leri genişlet ve isimlendir (3–5 gün)

3. **Program.cs – Yeni policy’ler**
   - Yukarıdaki tabloya göre `CatalogView`, `CatalogManage`, `PosSales`, `PosPayment`, `PosReceiptsView`, `PosTse`, `AuditView`, `ReportsView`, `SettingsManage`, `InventoryManage` ekle.
   - Mevcut `AdminUsers`, `UsersView`, `UsersManage` aynen kalsın.

4. **Controller’ları policy’e taşı**
   - **CategoriesController:** GET için `CatalogView`, POST/PUT için `CatalogManage`, DELETE için `CatalogManage` (veya Admin-only policy).
   - **PaymentController:** Ödeme akışı + TSE imza → `PosPayment`; signature-debug → `SettingsManage` veya Admin.
   - **AuditLogController:** Tüm endpoint’leri `AuditView` (ve gerekirse alt kısıtlama) ile değiştir.
   - **SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, CashRegisterController:** `SettingsManage`.
   - **InventoryController:** Mevcut Administrator/Manager eşlemesini `InventoryManage` / `InventoryView` ile yap.
   - **ReportsController:** `[Authorize]` → `[Authorize(Policy = "ReportsView")]`.
   - **CartController, OrdersController, ReceiptsController, TseController, TagesabschlussController, TableController, CustomerController:** Uygun policy’e geç (`PosSales`, `PosPayment`, `PosReceiptsView`, `PosTse`).

5. **Waiter rolü**
   - `RoleSeedData.cs`: `Waiter` rolünü ekle (Kellner’ı kaldırma; hem Kellner hem Waiter seed’lenebilir, sonra Kellner deprecated denir).
   - Policy’lerde Waiter’ı yukarıdaki tabloya göre ekle (PosSales, PosPayment, PosReceiptsView; PosTse isteğe bağlı).

### Faz 3 – Frontend ve seed uyumu (2–3 gün)

6. **FE-Admin – Rol listesi**
   - Users sayfasındaki ROLE_OPTIONS’ı backend ile aynı yap: SuperAdmin, Admin, Manager, Cashier, Waiter, Auditor, BranchManager (+ opsiyonel Kitchen, Accountant). Backend’den liste geliyorsa onu kullan.

7. **POS frontend (Expo)**
   - `frontend/types/auth.ts`: `UserRole` tipine Waiter (ve isteğe bağlı Kitchen, Accountant) ekle.
   - `frontend/shared/utils/PermissionHelper.ts`: PERMISSIONS ve SCREEN_ACCESS’e Waiter (ve opsiyonel roller) ekle; backend policy’lerle hizalı olacak şekilde resource/action matrisini güncelle.

8. **Test ve seed**
   - `UserManagementAuthorizationPolicyTests`: Gerekirse yeni policy’ler ve Waiter için test ekle.
   - Demo/seed kullanıcı: Bir “Waiter” örnek kullanıcı eklenebilir (UserSeedData veya manuel).

### Faz 4 (Opsiyonel) – Permission tablosu

9. Sadece ihtiyaç olursa: Permission, RolePermission, UserPermission tabloları + migration; policy handler’ların DB’den okuması. Bu dokümanda detaya girilmedi.

---

## 5) Etkilenen dosyalar / katmanlar

### 5.1 Backend

| Dosya / klasör | Değişiklik |
|----------------|------------|
| `backend/Program.cs` | Yeni authorization policy’ler (CatalogView, CatalogManage, PosSales, PosPayment, …). |
| `backend/Data/RoleSeedData.cs` | Waiter rolü ekleme; Kellner/Waiter kararı. |
| `backend/Data/UserSeedData.cs` | İsteğe bağlı Waiter demo kullanıcı. |
| `backend/Controllers/CategoriesController.cs` | [Authorize(Roles=…)] → [Authorize(Policy = "CatalogView")] / CatalogManage. |
| `backend/Controllers/PaymentController.cs` | Kasiyer→Cashier veya PosPayment; admin-only policy. |
| `backend/Controllers/AuditLogController.cs` | Roles → AuditView (ve gerekiyorsa alt policy). |
| `backend/Controllers/SettingsController.cs` | Roles → SettingsManage. |
| `backend/Controllers/CompanySettingsController.cs` | Roles → SettingsManage. |
| `backend/Controllers/LocalizationController.cs` | Roles → SettingsManage. |
| `backend/Controllers/MultilingualReceiptController.cs` | Roles → SettingsManage. |
| `backend/Controllers/CashRegisterController.cs` | Roles → SettingsManage. |
| `backend/Controllers/InventoryController.cs` | Roles → InventoryManage / InventoryView. |
| `backend/Controllers/ReportsController.cs` | [Authorize] → ReportsView. |
| `backend/Controllers/CartController.cs` | [Authorize] → PosSales. |
| `backend/Controllers/OrdersController.cs` | [Authorize] → PosSales. |
| `backend/Controllers/ReceiptsController.cs` | [Authorize] → PosReceiptsView. |
| `backend/Controllers/TseController.cs` | [Authorize] → PosTse. |
| `backend/Controllers/TagesabschlussController.cs` | [Authorize] → PosTse (veya ayrı policy). |
| `backend/Controllers/TableController.cs` | [Authorize] → PosSales. |
| `backend/Controllers/CustomerController.cs` | [Authorize] → PosSales. |
| `backend/Controllers/ProductController.cs` | [Authorize] → CatalogView (GET); yazma varsa CatalogManage. |
| `backend/Controllers/AdminProductsController.cs` | [Authorize] → CatalogManage. |
| `backend/Controllers/ModifierGroupsController.cs` | [Authorize] → CatalogView / CatalogManage ayrımı. |
| `backend/Controllers/InvoiceController.cs` | Backfill zaten Admin; diğer endpoint’ler policy ile. |
| `backend/Controllers/Base/EntityController.cs` | Administrator → SettingsManage veya AdminUsers. |
| `backend/KasseAPI_Final.Tests/UserManagementAuthorizationPolicyTests.cs` | Yeni policy’ler + Waiter testleri. |
| `backend/Middleware/ForbiddenResponseAuthorizationHandler.cs` | Değişiklik gerekmez; policy adı zaten 403’te gidiyor. |

### 5.2 Frontend Admin (Next.js)

| Dosya | Değişiklik |
|-------|------------|
| `frontend-admin/src/app/(protected)/users/page.tsx` | ROLE_OPTIONS’ı hedef rollere göre güncelle veya backend’den al. |

### 5.3 Frontend POS (Expo)

| Dosya | Değişiklik |
|-------|------------|
| `frontend/types/auth.ts` | UserRole: Waiter (ve opsiyonel Kitchen, Accountant) ekle. |
| `frontend/shared/utils/PermissionHelper.ts` | PERMISSIONS, SCREEN_ACCESS, UserRole enum; Waiter ve opsiyonel roller. |
| `frontend/contexts/AuthContext.tsx` | Rol listesi backend’den geliyorsa tip ve hasRole/hasAnyRole uyumu. |
| `frontend/components/OrderManager.tsx` | currentUserRole === 'waiter' vb. gerekirse. |

### 5.4 Dokümantasyon

| Dosya | Değişiklik |
|-------|------------|
| `docs/architecture/USERS_MODULE_PERMISSION_MATRIX.md` | Hedef policy ve rol listesine göre güncelle; Waiter ekle. |
| `docs/architecture/AUTHORIZATION_POS_ANALYSIS_AND_TARGET.md` | Bu dosya. |
| `ai/` altı ilgili özetler | Yetki değişikliği özeti eklenebilir. |

---

## 6) Özet

- **Mevcut sorunlar:** Rol isim tutarsızlığı (Administrator/Admin, Kasiyer typo), POS rollerinin (Cashier/Waiter) birçok endpoint’te kullanılmaması, policy yerine uzun rol listeleri, FE–BE rol listesi uyumsuzluğu.
- **Hedef mimari:** Sabit roller (SuperAdmin, Admin, Manager, Cashier, Waiter; opsiyonel Kitchen, Accountant) + anlamlı policy isimleri (CatalogView, CatalogManage, PosSales, PosPayment, PosTse, AuditView, ReportsView, SettingsManage, InventoryManage vb.).
- **Role vs Role+Permission:** Önce role + policy ile ilerlenmeli; permission tablosu ileride ihtiyaç halinde eklenir.
- **Migration:** Önce PaymentController Kasiyer düzeltmesi ve policy’lerin tanımı; sonra controller’ların policy’e geçmesi; ardından Waiter seed, FE-Admin rol listesi ve POS PermissionHelper güncellemesi.

Bu plan, RKSV ve dokunulmaması gereken modüller (TSE, Tagesabschluss, FinanzOnline, AuditLog kalıcılığı) ile uyumlu, sadece yetki katmanını netleştirir; fiş numaralama, imza zinciri ve vergi hesaplama mantığına dokunmaz.
