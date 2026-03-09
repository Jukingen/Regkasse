# POS Authorization: Geriye Dönük Uyumlu Geçiş Planı

**Tarih:** 2025-03-09  
**Amaç:** Sistemi kırmadan POS rollerini (Cashier, Waiter) eklemek; Admin/backoffice akışlarını korumak; ileride permission modeline temiz geçişi mümkün kılmak.

**Mevcut roller (kullanımda):** SuperAdmin, Administrator, Manager, Admin  
**Hedefte eklenecek:** Cashier, Waiter  

---

## 1) Backward-compatible geçiş planı (özet)

| Adım | Ne yapılır | Kırılım |
|------|------------|--------|
| 1 | Administrator ve Admin’i **policy’lerde birlikte** tut; hiçbir policy’den Administrator veya Admin’i çıkarma | Yok |
| 2 | Cashier ve Waiter rollerini Identity seed’de **zaten var**; yoksa ekle. Mevcut kullanıcıları değiştirme | Yok |
| 3 | POS policy’lerine (PosSales, PosTableOrder, PosCatalogRead, PosTse) Cashier ve Waiter **ekle**; backoffice policy’lere (UsersView, UsersManage, BackofficeSettings, SystemCritical) **ekleme** | Yok – sadece yeni roller daha fazla endpoint’e girebilir |
| 4 | FE-Admin’de atanabilir roller listesine Cashier, Waiter, Manager ekle (backend’den geliyorsa listeyi backend’e bırak) | Yok – sadece yeni seçenekler |
| 5 | POS frontend’de Waiter rolü ve ekran erişimini ekle (PermissionHelper / SCREEN_ACCESS) | Yok – sadece genişletme |
| 6 | İleride: controller’larda rol listesi yerine **policy** kullanımı (zaten büyük ölçüde yapılmış) | Yok |
| 7 | Permission modeli: **ayrı faz**; önce rol genişletmesi tamamlansın, sonra permission tabanlı policy’lere geçiş | İki aşamalı |

**Geriye dönük uyumluluk kuralları:**

- Mevcut token’da `role: "Administrator"` veya `role: "Admin"` olan kullanıcılar **aynı** endpoint’lere erişmeye devam etmeli.
- Hiçbir policy’den SuperAdmin, Admin, Administrator, Manager kaldırılmamalı; sadece **yeni roller eklenebilir** (Cashier, Waiter).
- Backoffice-only policy’ler (BackofficeSettings, SystemCritical, AdminUsers, UsersManage) **sadece** SuperAdmin, Admin, Administrator (ve varsa BranchManager) kalmalı; Cashier/Waiter eklenmemeli.

---

## 2) Administrator ile Admin çakışması – analiz

### 2.1 Nerede kullanılıyor?

| Yer | Administrator | Admin |
|-----|----------------|--------|
| **Identity (RoleSeedData)** | Ayrı rol olarak seed’leniyor | Ayrı rol olarak seed’leniyor |
| **Program.cs yorum** | “Legacy alias for Admin” | – |
| **Policy tanımları** | Tüm admin policy’lerde **ikisi birlikte** listeleniyor | Aynı |
| **JWT / Login** | Token’da hangi rol dönüyor backend’e bağlı (genelde kullanıcının tek rolü) | Aynı |

### 2.2 Çakışma var mı?

- **Anlam:** İkisi de “şube/sistem yöneticisi” kapsamında kullanılıyor; **aynı yetki seti** kabul ediliyor.
- **Teknik çakışma yok:** İki ayrı rol adı var; policy’lerde ikisi de geçerli rol olarak yazıldığı için token’da `Administrator` veya `Admin` gelirse ikisi de çalışır.
- **Karışıklık riski:** Yeni geliştiriciler “hangisini kullanmalı” diye sorabilir; dokümantasyonda “Administrator = Admin (legacy)” yazılması yeterli.

### 2.3 Öneri

| Seçenek | Öneri | Açıklama |
|---------|--------|----------|
| Administrator’ı kaldır | **Hayır** | Mevcut kullanıcılar ve token’lar 403 alır; kırılım olur. |
| Admin’i kaldır | **Hayır** | Aynı kırılım. |
| İkisini birleştir (merge) | **Uzun vadede isteğe bağlı** | Tüm kullanıcıları tek role (örn. Admin) taşıyıp Administrator’ı deprecated yapılabilir; ayrı migration ve iletişim gerekir. |
| **İkisini birlikte tut** | **Evet** | Tüm policy’lerde `"Admin", "Administrator"` birlikte kalsın. Sıfır kırılım. Yeni atamalarda tercihen **Admin** kullanılabilir. |

**Sonuç:** Çakışma “yetki farkı” değil, “iki isim aynı yetki” durumu. **İkisini de policy’lerde tutmak** backward-compatible ve en düşük riskli seçenek.

---

## 3) Cashier ve Waiter eklenince ayrılacak endpoint’ler ve ekranlar

Mevcut durumda birçok controller **zaten policy** kullanıyor (PosSales, PosTableOrder, BackofficeSettings, vb.). Cashier ve Waiter **sadece o policy’lerin rol listesine eklenmiş** olmalı; “ayrılacak” derken: hangi endpoint/ekran **sadece backoffice** (Cashier/Waiter giremez), hangisi **POS** (Cashier/Waiter girebilir) net olsun.

### 3.1 Backoffice-only (Cashier / Waiter erişemez)

Bu endpoint’ler ve ekranlar **mevcut** policy’lerle korunmalı; Cashier ve Waiter **eklenmemeli**.

| Policy | Endpoint / Controller | Ekran (FE-Admin) |
|--------|------------------------|-------------------|
| AdminUsers | AdminUsersController (api/admin/users) | – |
| UsersView / UsersManage | UserManagementController (api/UserManagement) | Users sayfası (liste, detay, create, edit, deactivate, role) |
| BackofficeSettings | SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, FinanzOnlineController; CashRegisterController (POST) | Settings, Company, Localization, Receipt templates, FinanzOnline, Cash register config |
| SystemCritical | InvoiceController backfill, EntityController kalıcı silme, PaymentController signature-debug/verify | Backfill, Hard delete, TSE diagnostics |
| AuditAdmin | AuditLogController cleanup, export | Audit cleanup/export |
| CashRegisterManage | CashRegisterController POST (kasa kaydı oluşturma) | Kasa kaydı oluşturma |
| CatalogManage | CategoriesController POST/PUT/DELETE, AdminProductsController, ModifierGroupsController (yazma) | Admin: Categories, Products, Modifier groups (CRUD) |
| InventoryManage / InventoryDelete | InventoryController | Inventory (stok girişi, silme) |

### 3.2 POS – Cashier ve Waiter erişebilir

Bu endpoint’lerde ilgili policy’lerin rol listesinde **Cashier ve Waiter** olmalı (zaten Program.cs’te ekli olabilir; kontrol edilmeli).

| Policy | Endpoint / Controller | Ekran (POS) |
|--------|------------------------|-------------|
| PosSales | CartController, PaymentController (akış, TSE imza), ReceiptsController | Cart, Payment, Receipts |
| PosTableOrder | OrdersController, TableController, CustomerController | Orders, Tables, Customer |
| PosCatalogRead | ProductController GET, Categories GET, ModifierGroups GET | Ürün/kategori listesi (okuma) |
| PosTse | TseController, TagesabschlussController | TSE, Vardiya aç/kapa |
| AuditViewWithCashier | AuditLogController GET payment/{id} | – (Cashier kendi ödemesinin audit’ini görebilir) |

### 3.3 Ayrım özeti

| Erişim | Roller | Örnek |
|--------|--------|--------|
| **Sadece backoffice** | SuperAdmin, Admin, Administrator (+, BranchManager, Auditor policy’ye göre) | Users, Settings, System critical, Catalog write, Inventory |
| **Backoffice + Manager** | Yukarı + Manager | Reports, Audit view, Catalog manage, Inventory manage |
| **POS (Cashier, Waiter dahil)** | + Cashier, Waiter | Sales, Table/Order, Catalog read, TSE/Shift |

Cashier ve Waiter eklendikten sonra **ayrılacak** şey: Bu iki rolün **giremeyeceği** endpoint’lerin policy’lerinden emin olmak (yani backoffice policy’lere Cashier/Waiter **eklenmemeli**). “Hangi endpoint’ler ayrılmalı” = backoffice policy’lere Cashier/Waiter eklenmemeli; POS policy’lere eklenmeli.

---

## 4) İlk aşama: Sadece rol genişletmesi mi, permission sistemi de birlikte mi?

### 4.1 Seçenek A: Sadece rol genişletmesi (önerilen ilk adım)

| Artı | Eksi |
|------|------|
| Değişiklik küçük: policy’lere Cashier/Waiter eklemek, seed ve FE listesini güncellemek | İleride permission’a geçerken ikinci bir refactor gerekir |
| Mevcut policy yapısı aynen kalır; test ve rollback basit | – |
| Backoffice kesinlikle kırılmaz | – |
| Permission modeli için hazırlık (doküman, sabitler) ayrı fazda yapılabilir | – |

### 4.2 Seçenek B: Rol + permission aynı anda

| Artı | Eksi |
|------|------|
| Tek seferde hedef mimariye yaklaşılır | Değişiklik büyük: handler, permission sabitleri, policy’lerin permission’a bağlanması, controller’ların bir kısmının Permission:xxx kullanması |
| İki refactor yerine bir refactor | Risk artar; test ve geri alım daha zor |
| | Backoffice’i bozmama hedefi ile daha fazla dikkat gerekir |

### 4.3 Değerlendirme

**Öneri: İlk aşamada sadece rol genişletmesi.**

1. **Hedef:** “Mevcut admin/backoffice akışları bozulmasın” ve “POS operasyon rollerini ekleyebilelim” → bunlar **rol setinin** genişletilmesi ile sağlanır; permission zorunlu değil.
2. **Risk:** Permission’ı aynı anda getirmek daha fazla dosya ve davranış değişikliği demek; hata payı artar.
3. **Sonrasında temiz geçiş:** Policy isimleri (PosSales, BackofficeSettings, …) zaten anlamlı; ileride her policy “şu permission’ları gerektirir” diye eşlenebilir. Önce rol setini netleştirip, Cashier/Waiter’ı doğru policy’lere bağlamak, permission fazını daha anlaşılır kılar.

**Sıra önerisi:**  
1) Rol genişletmesi (Cashier, Waiter; policy’lerde ve FE’de) → test → production.  
2) Ayrı fazda: permission sabitleri + handler + controller’ların kademeli permission policy’ye taşınması.

---

## 5) En düşük riskli migration sırası

### 5.1 Sıra (önerilen)

| Sıra | Adım | Risk | Geri alım |
|------|------|------|-----------|
| 1 | **Doğrulama:** Program.cs’te backoffice policy’lerde (BackofficeSettings, SystemCritical, AdminUsers, UsersView, UsersManage, AuditAdmin, CashRegisterManage, CatalogManage, InventoryDelete) Cashier ve Waiter **yok**; POS policy’lerde (PosSales, PosTableOrder, PosCatalogRead, PosTse) **var** mı kontrol et. Varsa değişiklik yok. | Çok düşük | – |
| 2 | **Seed:** RoleSeedData’da Cashier ve Waiter zaten var mı kontrol et. Yoksa ekle; var olan kullanıcılara dokunma. | Çok düşük | Seed’den rol silinmez; yeni rol eklemek mevcut kullanıcıları etkilemez |
| 3 | **FE-Admin:** Kullanıcı atama/filtre için rol listesini backend’den alıyorsa (useRoles) listeyi kullan; sabit ROLE_OPTIONS kullanıyorsa ROLE_OPTIONS’a Manager, Cashier, Waiter ekle (SuperAdmin, Admin, Auditor, BranchManager kalır). Böylece yeni atamalarda Cashier/Waiter seçilebilir. | Düşük | ROLE_OPTIONS’tan tekrar çıkarılabilir |
| 4 | **POS frontend:** PermissionHelper / auth tiplerinde Waiter ekle; SCREEN_ACCESS ve PERMISSIONS’ta Waiter için uygun ekranlar/aksiyonlar tanımla. Backend’deki PosTableOrder, PosCatalogRead, PosSales ile uyumlu. | Düşük | Waiter’ı enum’dan veya matristen çıkarmak geri alım |
| 5 | **Dokümantasyon:** Administrator = Admin (legacy) ve “hangi rol hangi policy’ye girer” tablosunu dokümante et. | Yok | – |
| 6 | **Test:** Admin, Administrator, Manager ile mevcut backoffice akışları; Cashier ve Waiter ile POS akışları (sepet, ödeme, masa, sipariş) test edilir. | – | – |

**Yapılmayacak (bu aşamada):**

- Controller’da `[Authorize(Roles = "...")]` geri getirmek veya policy isimlerini değiştirmek.
- Backoffice policy’lerden SuperAdmin, Admin, Administrator, Manager çıkarmak.
- Permission tabanlı handler’ı zorunlu kılmak veya tüm endpoint’leri Permission:xxx’e taşımak (bu, izleyen fazda yapılabilir).

### 5.2 Kontrol listesi (mevcut kod tabanı)

Projede **zaten** yapılmış olanlar (referans):

- Controller’lar policy kullanıyor; `[Authorize(Roles = "...")]` tekrarlanmıyor.
- Program.cs’te PosSales, PosTableOrder, PosCatalogRead, PosTse policy’lerinde Cashier ve Waiter **tanımlı**.
- RoleSeedData’da Cashier ve Waiter **mevcut**.

Buna göre “migration” büyük ölçüde **doğrulama ve frontend uyumu**:

- Backoffice policy’lerde Cashier/Waiter olmadığını doğrula.
- FE-Admin’de atanabilir rollerde Cashier, Waiter, Manager’ın görünmesini sağla (backend’den geliyorsa listeyi kullan).
- POS’ta Waiter rolü ve ekran erişimini ekle; backend policy’lerle tutarlı ol.

---

## 6) Hedeflerle eşleme

| Hedef | Nasıl sağlanır |
|-------|-----------------|
| **Mevcut admin/backoffice akışları bozulmasın** | Backoffice policy’lerde rol seti değiştirilmez; Administrator ve Admin ikisi de kalır; Cashier/Waiter backoffice policy’lere eklenmez. |
| **POS operasyon rollerini ekleyebilelim** | Cashier ve Waiter Identity’de ve POS policy’lerde (PosSales, PosTableOrder, PosCatalogRead, PosTse) tanımlı; FE-Admin’de atanabilir; POS’ta Waiter için ekran/aksiyon tanımlı. |
| **Sonrasında permission modeline temiz geçiş mümkün olsun** | İlk aşamada sadece rol genişletmesi; policy isimleri ve domain ayrımı korunur. İkinci fazda permission sabitleri + handler + controller’ların Permission:xxx kullanması; mevcut policy isimleri alias olarak bırakılabilir. |

Bu doküman, sistemi kırmadan POS authorization’ı rol genişletmesi ile güvenli şekilde ilerletmek ve izleyen permission geçişini planlamak için kullanılabilir.
