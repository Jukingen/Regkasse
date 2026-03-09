# POS Authorization – Phase 1: Tasarım (Kodlamadan Önce)

**Tarih:** 2025-03-09  
**Amaç:** Operasyonel POS rollerini ve domain’leri yansıtan, bakımı kolay bir authorization yapısı için önce tasarımı netleştirmek. Kod refactor Phase 2’de bu tasarıma göre yapılacak.

**Mevcut sorun:** `[Authorize(Roles = "SuperAdmin,Administrator,Manager,Admin")]` backoffice ağırlıklı; POS rollerleri (Cashier, Waiter) ve domain ayrımı yok; aynı rol listesinin her yerde tekrarlanması.

---

## 1) Domain bazlı rol ayrımı

Yetkileri **domain**’e göre gruplayıp, hangi rollerin hangi domain’e erişeceğini tanımlıyoruz.

| Domain | Açıklama | Kapsam |
|--------|----------|--------|
| **Backoffice** | Kullanıcı ve organizasyon yönetimi | Kullanıcı listesi, oluşturma, düzenleme, devre dışı bırakma, rol atama, şifre sıfırlama; kullanıcı aktivite/denetim görüntüleme |
| **POS Sales** | Satış noktası operasyonu | Sepet, ödeme alma, fiş kesme, TSE imza, iade, indirim/fiyat düzenleme, kasa çekmecesi, vardiya aç/kapa, fiş listesi |
| **Restaurant / Table Service** | Masa ve sipariş akışı | Masa listesi, masa durumu, sipariş oluşturma/güncelleme, müşteri bilgisi; katalog okuma (ürün/kategori) |
| **Reporting** | Raporlama ve denetim | Rapor ekranları, rapor dışa aktarma, denetim kayıtları (audit log) |
| **Settings / System** | Sistem ve şirket ayarları | Şirket bilgisi, vergi oranları, yedekleme, yerelleştirme, fiş şablonları, kasa/Fiskaly config, FinanzOnline, katalog yazma (ürün/kategori/stok), kritik sistem işlemleri (backfill, kalıcı silme) |

**Domain → rol mantığı (özet):**

- **Backoffice:** Sadece yönetim rolleri (Admin, SuperAdmin; isteğe bağlı Manager view-only).
- **POS Sales:** Cashier + Manager + Admin + SuperAdmin (operasyonel satış).
- **Restaurant / Table Service:** Waiter + Cashier + Manager + Admin + SuperAdmin (masa/sipariş).
- **Reporting:** Manager + Admin + SuperAdmin (rapor/audit); Cashier sadece basit satış özeti (isteğe bağlı).
- **Settings / System:** Sadece Admin + SuperAdmin (sistem ayarları ve kritik işlemler).

---

## 2) Önerilen role seti

| Rol | Açıklama | Domain erişimi (kavramsal) |
|-----|----------|----------------------------|
| **SuperAdmin** | Tüm yetkiler; sistem genelinde tam kontrol | Tüm domain’ler, tam yetki |
| **Admin** | Şube/sistem yönetimi; backoffice + ayarlar | Backoffice, POS Sales, Restaurant, Reporting, Settings/System (tam) |
| **Manager** | Operasyon + kısıtlı yönetim | Backoffice (view), POS Sales (tam), Restaurant (tam), Reporting (tam), Settings/System (yok); katalog yazma ve stok (tam) |
| **Cashier** | Kasa operasyonu | POS Sales (tam), Restaurant (tam), Reporting (sadece view/özet); Backoffice ve Settings yok |
| **Waiter** | Masa ve sipariş | Restaurant (tam); POS Sales’ta sınırlı (ödeme/indirim olabilir; iade/kasa çekmecesi/vardiya aç yok); Reporting ve diğerleri yok |

**Legacy:** **Administrator** rolü Identity’de kalabilir; yetki olarak **Admin** ile aynı kabul edilir (policy’lerde her ikisi de listelenir). Yeni atamalarda **Admin** tercih edilir.

**Opsiyonel roller (ileride):** Auditor (sadece report/audit view), BranchManager (şube kapsamlı), Demo (test).

---

## 3) Permission listesi

Format: **`resource.action`** (küçük harf). Backend ve frontend’de aynı string kullanılır.

### 3.1 Domain: Backoffice

| Permission | Açıklama |
|------------|----------|
| user.view | Kullanıcı listesi, detay, aktivite görüntüleme |
| user.manage | Kullanıcı oluşturma, güncelleme, devre dışı bırakma, rol atama, şifre sıfırlama |

### 3.2 Domain: POS Sales

| Permission | Açıklama |
|------------|----------|
| sale.create | Satış oluşturma (sepet → ödeme → fiş) |
| sale.view | Fiş listesi, fiş detayı görüntüleme |
| payment.take | Ödeme alma, fiş kesme, TSE imzası |
| refund.create | İade işlemi oluşturma |
| price.override | İndirim veya fiyat değiştirme (satır/fiş) |
| cashdrawer.open | Kasa çekmecesi açma |
| shift.open | Vardiya açma |
| shift.close | Vardiya kapatma (gün sonu) |
| receipt.reprint | Fiş yeniden yazdırma |
| receipt.void | Fiş iptali (fiscal; dikkatli kullanım) |

### 3.3 Domain: Restaurant / Table Service

| Permission | Açıklama |
|------------|----------|
| order.create | Sipariş oluşturma |
| order.update | Sipariş güncelleme / iptal |
| order.view | Sipariş listesi ve detay |
| table.view | Masa listesi ve durumu |
| table.manage | Masa atama, birleştirme, bölme |
| customer.view | Müşteri bilgisi görüntüleme |
| customer.manage | Müşteri ekleme / düzenleme |
| product.view | Ürün, kategori, modifier okuma |
| category.view | Kategori okuma (product.view ile örtüşebilir) |

### 3.4 Domain: Reporting

| Permission | Açıklama |
|------------|----------|
| report.view | Rapor ekranları, grafikler |
| report.export | Rapor dışa aktarma (CSV, PDF) |
| audit.view | Denetim kayıtları görüntüleme |

### 3.5 Domain: Settings / System

| Permission | Açıklama |
|------------|----------|
| settings.manage | Sistem/şirket ayarları, vergi, yedekleme, yerelleştirme, fiş şablonu, kasa/Fiskaly/FinanzOnline config |
| product.manage | Ürün ve modifier grubu CRUD |
| category.manage | Kategori CRUD |
| inventory.view | Stok görüntüleme |
| inventory.manage | Stok girişi, düzeltme |
| system.critical | Backfill, kalıcı silme, TSE tanılama gibi kritik işlemler |

### 3.6 Özet liste (alfabetik)

```
audit.view
cashdrawer.open
category.manage
category.view
customer.manage
customer.view
inventory.manage
inventory.view
order.create
order.update
order.view
payment.take
price.override
product.manage
product.view
receipt.reprint
receipt.void
refund.create
report.export
report.view
sale.create
sale.view
settings.manage
shift.close
shift.open
system.critical
table.manage
table.view
user.manage
user.view
```

---

## 4) Role–permission matrisi

**✅** = Rol bu permission’a sahip. **❌** = Sahip değil.

| Permission | SuperAdmin | Admin | Manager | Cashier | Waiter |
|------------|:----------:|:-----:|:-------:|:-------:|:------:|
| user.view | ✅ | ✅ | ✅ | ❌ | ❌ |
| user.manage | ✅ | ✅ | ❌ | ❌ | ❌ |
| sale.create | ✅ | ✅ | ✅ | ✅ | ✅ |
| sale.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| payment.take | ✅ | ✅ | ✅ | ✅ | ✅ |
| refund.create | ✅ | ✅ | ✅ | ✅ | ❌ |
| price.override | ✅ | ✅ | ✅ | ✅ | ✅ |
| cashdrawer.open | ✅ | ✅ | ✅ | ✅ | ❌ |
| shift.open | ✅ | ✅ | ✅ | ✅ | ❌ |
| shift.close | ✅ | ✅ | ✅ | ✅ | ✅ |
| receipt.reprint | ✅ | ✅ | ✅ | ✅ | ❌ |
| receipt.void | ✅ | ✅ | ❌ | ❌ | ❌ |
| order.create | ✅ | ✅ | ✅ | ✅ | ✅ |
| order.update | ✅ | ✅ | ✅ | ✅ | ✅ |
| order.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| table.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| table.manage | ✅ | ✅ | ✅ | ✅ | ✅ |
| customer.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| customer.manage | ✅ | ✅ | ✅ | ✅ | ✅ |
| product.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| category.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| report.view | ✅ | ✅ | ✅ | ✅ | ❌ |
| report.export | ✅ | ✅ | ✅ | ❌ | ❌ |
| audit.view | ✅ | ✅ | ✅ | ❌ | ❌ |
| settings.manage | ✅ | ✅ | ❌ | ❌ | ❌ |
| product.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| category.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| inventory.view | ✅ | ✅ | ✅ | ❌ | ❌ |
| inventory.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| system.critical | ✅ | ✅ | ❌ | ❌ | ❌ |

**Not:** Waiter için `payment.take` ve `price.override` iş kuralına göre kısıtlanabilir (ör. sadece kendi masaları). Matris varsayılan olarak tam verildi.

---

## 5) Controller gruplama stratejisi

Her controller veya endpoint grubu **tek bir domain** veya **tek bir permission seti** ile eşleşir. Rol listesini controller’a yazmak yerine **policy adı** kullanılır; policy tanımı Program.cs (veya permission matrisi) içinde tek yerde tutulur.

### 5.1 Domain → controller eşlemesi

| Domain | Controller’lar / endpoint’ler | Önerilen policy (kavramsal) |
|--------|------------------------------|-----------------------------|
| **Backoffice** | UserManagementController, AdminUsersController | UsersView, UsersManage (mevcut) |
| **POS Sales** | CartController, PaymentController (process, TSE sign), ReceiptsController | PosSales |
| **Restaurant / Table** | OrdersController, TableController, CustomerController; ProductController GET, Categories GET, ModifierGroups GET | PosTableOrder, PosCatalogRead |
| **Reporting** | ReportsController, AuditLogController (view/export) | ReportView, ReportExport, AuditView, AuditAdmin |
| **Settings / System** | SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, FinanzOnlineController, CashRegisterController (config); Categories POST/PUT/DELETE, AdminProductsController, ModifierGroupsController (write); InventoryController; InvoiceController backfill; EntityController kalıcı silme; PaymentController signature-debug/verify | BackofficeSettings, CatalogManage, InventoryManage, SystemCritical |

### 5.2 Strateji kuralları

1. **Tek kaynak:** Hangi rollerin hangi endpoint’e gireceği yalnızca policy tanımında (veya permission matrisinde) olsun; controller’da sadece `[Authorize(Policy = "PolicyName")]`.
2. **Domain odaklı isimlendirme:** Policy isimleri domain veya permission’ı yansıtsın (PosSales, PosTableOrder, BackofficeSettings, Permission:report.view).
3. **Action bazlı ayrım:** Aynı controller’da farklı yetkiler gerekiyorsa (ör. GET vs DELETE) farklı policy veya farklı permission kullanılsın (örn. CatalogManage sadece yazma; okuma PosCatalogRead).
4. **Mevcut policy’lerle uyum:** Refactor sırasında mevcut policy isimleri (PosSales, BackofficeManagement, …) korunabilir; içerideki rol setleri bu tasarımdaki domain/role setleri ile güncellenir.

### 5.3 Controller → domain özet tablosu

| Controller | Domain | Not |
|------------|--------|-----|
| UserManagementController, AdminUsersController | Backoffice | UsersView / UsersManage |
| CartController, PaymentController (akış), ReceiptsController | POS Sales | PosSales |
| TseController, TagesabschlussController | POS Sales (vardiya/TSE) | PosTse |
| OrdersController, TableController, CustomerController | Restaurant / Table | PosTableOrder |
| ProductController GET, Categories GET, ModifierGroups GET | Restaurant / Table (katalog okuma) | PosCatalogRead |
| Categories POST/PUT/DELETE, AdminProductsController, ModifierGroups write | Settings/System (katalog yazma) | CatalogManage |
| ReportsController | Reporting | ReportView / ReportExport |
| AuditLogController | Reporting / Backoffice | AuditView, AuditAdmin |
| SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, FinanzOnlineController, CashRegisterController | Settings/System | BackofficeSettings, CashRegisterManage |
| InventoryController | Settings/System | InventoryManage, InventoryDelete |
| InvoiceController backfill, EntityController kalıcı silme, PaymentController signature-debug | Settings/System | SystemCritical |

---

## 6) Frontend görünürlük stratejisi

### 6.1 Yetki kaynağı

- **Tercih:** Backend’den `GET /api/me` (veya login cevabı) ile `{ roles: string[], permissions: string[] }` dönülsün. Frontend bunu cache’ler (React Query, Context).
- **Alternatif:** JWT’te sadece `role` (veya `roles`) olsun; frontend’de **rol → permission** matrisi (bu dokümandaki matrisle senkron) statik tutulsun; `hasPermission(perm)` rol üzerinden hesaplansın.

### 6.2 Menü görünürlüğü

| Menü / Sayfa | Gerekli permission (veya rol) |
|---------------|------------------------------|
| FE-Admin: Users | user.view |
| FE-Admin: Create/Edit user, Deactivate, Role | user.manage |
| FE-Admin: Products / Categories (admin) | product.manage veya category.manage |
| FE-Admin: Reports | report.view |
| FE-Admin: Audit log | audit.view |
| FE-Admin: Settings | settings.manage |
| POS: Sales / Cart / Payment / Receipts | sale.create, sale.view, payment.take |
| POS: Refund | refund.create |
| POS: Discount / Price override | price.override |
| POS: Tables / Orders | order.*, table.* |
| POS: Shift open/close | shift.open, shift.close |
| POS: Cash drawer | cashdrawer.open |
| POS: Reports (özet) | report.view |
| POS: Product/Category (read) | product.view, category.view |

Kural: `hasPermission('report.view')` true ise menü öğesi gösterilir; route guard aynı permission ile korunur.

### 6.3 Buton görünürlüğü

- Her aksiyon için tek bir permission (veya rol) tanımla. Örn. "İade" → refund.create; "Kasa çekmecesi aç" → cashdrawer.open; "Vardiya kapat" → shift.close.
- Butonu gizlemek veya disabled yapmak için `hasPermission(...)` kullan. Backend zaten 403 döneceği için görünürlük sadece UX içindir.

### 6.4 Route guard

- Protected layout veya route wrapper’da: sayfa için gerekli permission (veya rol) yoksa 403 sayfası veya ana sayfaya yönlendir.
- FE-Admin ve POS (Expo) için aynı mantık: tek bir `usePermissions()` veya `useAuth()` ile `permissions`/`roles` ve `hasPermission(name)`.

### 6.5 Senkron

- Permission string’leri backend’deki sabitlerle **bire bir aynı** olsun (frontend’de sabit veya generated type). Rol ve domain ayrımı bu dokümandaki matris ile uyumlu kalsın.

---

## 7) Kademeli migration planı

Kod değişikliği yapmadan önce tasarım; aşağıdaki adımlar **Phase 2 (kod refactor)** için sıra ve kapsamı belirler.

### Faz 1 – Tasarım ve sabitler (kırılım yok)

1. Bu dokümanı onayla; domain, rol seti, permission listesi ve matris nihai hale getirilsin.
2. Backend’de Roles ve Permissions sabit sınıfları (ve isteğe bağlı RolePermissionMatrix) eklenir; controller’lar henüz değişmez.
3. Frontend’de permission string’leri ve (isteğe bağlı) rol → permission matrisi sabit olarak tanımlanır.

### Faz 2 – Policy’leri domain/role setine göre güncelleme

4. Program.cs’teki mevcut policy’lerin rol setleri bu dokümandaki matris ve domain ayrımına göre güncellenir. Controller’daki `[Authorize(Policy = "X")]` aynen kalır; sadece policy tanımları değişir.
5. Eksik policy varsa eklenir (örn. ReportView, ReportExport); gereksiz tekrarlayan rol listesi controller’dan kaldırılmış olmalı (zaten önceki refactor’da policy’e geçildi).

### Faz 3 – Permission tabanlı policy (isteğe bağlı)

6. PermissionRequirement + Handler + `Permission:xxx` policy’leri eklenir; controller’lar kademeli olarak `[Authorize(Policy = "Permission:report.view")]` veya `[RequirePermission(Permissions.ReportView)]` kullanacak şekilde taşınır.
7. Eski policy isimleri alias olarak bırakılabilir (arka planda aynı permission’ı kontrol eder).

### Faz 4 – Frontend

8. GET /me (veya login) cevabına `permissions` eklenir; frontend menü/buton/route guard bu listeye (veya rol → permission matrisine) göre güncellenir.

### Faz 5 – İsteğe bağlı DB

9. Permissions ve RolePermissions tabloları; matrisin DB’den okunması. Bu tasarım dokümanı değişmez; sadece yetki kaynağı statikten DB’ye taşınır.

---

## 8) Sonraki adım: Phase 2 – Kod refactor planı

Bu tasarım onaylandıktan sonra çıkarılacak:

- Hangi dosyaların (Program.cs, controller’lar, frontend) nasıl değişeceği
- Her controller için hangi policy (veya permission) kullanılacağı satır satır
- Test ve geri alım notları
- Migration adımlarının kod karşılığı

**Şimdilik hiçbir kod dosyası bu tasarıma göre değiştirilmedi;** sadece tasarım dokümanı oluşturuldu.
