# POS Authorization: Role + Permission Tabanlı Sürdürülebilir Mimari

**Tarih:** 2025-03-09  
**Amaç:** Salt role bazlı kontrolden, role + permission tabanlı esnek bir yapıya geçiş için net model, matrix ve uygulama planı.

**İlişkili dokümanlar:** `AUTHORIZATION_POS_ANALYSIS_AND_TARGET.md`, `USERS_MODULE_PERMISSION_MATRIX.md`

---

## 1) Rol listesi

Tüm yetkiler **roller** üzerinden tanımlanır; her rol bir **permission seti** ile eşlenir. Kod ve JWT’te İngilizce canonical isimler kullanılır.

| Rol | Açıklama | Kapsam |
|-----|----------|--------|
| **SuperAdmin** | Tüm yetkiler; sistem ve tenant ayarları; kullanıcı/rol atama. | Backoffice + POS (full) |
| **Admin** | Şube/sistem yönetimi, kullanıcı yönetimi, raporlar, ayarlar, katalog, envanter. | Backoffice + POS (full) |
| **Manager** | Raporlar, denetim, stok, katalog düzenleme; vardiya kapanışı; POS satış/ödeme. | Backoffice (kısıtlı) + POS (full) |
| **Cashier** | Satış, ödeme, iade, indirim, sipariş, masa, kasa çekmecesi, vardiya aç/kapa, rapor görüntüleme. | POS only |
| **Waiter** | Sipariş oluşturma/güncelleme, masa yönetimi, indirim; ödeme alma ve fiş (iş kuralına göre). | POS only |

**Not:** `Administrator` Identity’de geriye uyumluluk için kalabilir; yetki olarak **Admin** ile aynı kabul edilir (policy/claim tarafında Admin ile eşleştirilir).

---

## 2) Permission listesi

Format: **`resource.action`** (küçük harf, nokta ile ayrılmış). API ve frontend’de aynı string kullanılır.

### 2.1 POS operasyonel

| Permission | Açıklama | Örnek endpoint / akış |
|------------|----------|------------------------|
| **sale.create** | Satış (fiş) oluşturma | Cart → Payment → Receipt |
| **sale.view** | Satış / fiş listesi görüntüleme | GET Receipts, GET Payment |
| **payment.take** | Ödeme alma, fiş kesme, TSE imzası | POST Payment, TSE sign |
| **refund.create** | İade işlemi oluşturma | Refund endpoint |
| **discount.apply** | İndirim uygulama (satır / fiş) | Cart/Order discount |
| **order.create** | Sipariş oluşturma | POST Order, Cart submit |
| **order.update** | Sipariş güncelleme / iptal | PUT Order, cancel |
| **order.view** | Sipariş listesi / detay görüntüleme | GET Orders |
| **table.manage** | Masa atama, birleştirme, bölme | TableController |
| **table.view** | Masa durumu görüntüleme | GET Tables |
| **cashdrawer.open** | Kasa çekmecesi açma | CashDrawer open |
| **shift.open** | Vardiya açma | Tagesabschluss / Shift open |
| **shift.close** | Vardiya kapatma (gün sonu) | Tagesabschluss close |
| **product.view** | Ürün / katalog okuma | GET Products, Categories, Modifiers |
| **customer.view** | Müşteri bilgisi görüntüleme | GET Customer |
| **customer.manage** | Müşteri ekleme / düzenleme | POST/PUT Customer |

### 2.2 Raporlama ve denetim

| Permission | Açıklama | Örnek endpoint |
|------------|----------|------------------|
| **report.view** | Rapor görüntüleme (satış, vardiya, stok özeti) | ReportsController |
| **report.export** | Rapor dışa aktarma (CSV, PDF) | Reports export |
| **audit.view** | Denetim kayıtları görüntüleme | AuditLogController |

### 2.3 Backoffice – yönetim

| Permission | Açıklama | Örnek endpoint |
|------------|----------|------------------|
| **user.view** | Kullanıcı listesi / detay görüntüleme | UserManagement GET |
| **user.manage** | Kullanıcı oluşturma, güncelleme, devre dışı bırakma, rol atama | UserManagement POST, PUT, deactivate |
| **product.manage** | Ürün / modifier oluşturma, düzenleme, silme | AdminProducts, ModifierGroups |
| **category.manage** | Kategori oluşturma, düzenleme, silme | CategoriesController POST, PUT, DELETE |
| **inventory.manage** | Stok girişi, düzeltme | InventoryController |
| **inventory.view** | Stok görüntüleme | InventoryController GET |
| **settings.manage** | Sistem / şirket / yerel ayarlar, FinanzOnline, TSE config, kasa | Settings, CompanySettings, Localization, CashRegister, FinanzOnline |
| **receipt.reprint** | Fiş yeniden yazdırma (admin) | Receipts reprint |
| **receipt.void** | Fiş iptali (fiscal işlem – dikkatli) | Receipt void (RKSV uyumlu ise) |

### 2.4 Özet permission listesi (alfabetik)

```
audit.view
cashdrawer.open
category.manage
customer.manage
customer.view
discount.apply
inventory.manage
inventory.view
order.create
order.update
order.view
payment.take
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
table.manage
table.view
user.manage
user.view
```

---

## 3) Role–Permission matrix

**✅** = Rol bu permission’a sahip. **❌** = Sahip değil.

| Permission | SuperAdmin | Admin | Manager | Cashier | Waiter |
|------------|:----------:|:-----:|:-------:|:-------:|:------:|
| **POS operasyonel** |
| sale.create | ✅ | ✅ | ✅ | ✅ | ✅ |
| sale.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| payment.take | ✅ | ✅ | ✅ | ✅ | ✅* |
| refund.create | ✅ | ✅ | ✅ | ✅ | ❌ |
| discount.apply | ✅ | ✅ | ✅ | ✅ | ✅ |
| order.create | ✅ | ✅ | ✅ | ✅ | ✅ |
| order.update | ✅ | ✅ | ✅ | ✅ | ✅ |
| order.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| table.manage | ✅ | ✅ | ✅ | ✅ | ✅ |
| table.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| cashdrawer.open | ✅ | ✅ | ✅ | ✅ | ❌ |
| shift.open | ✅ | ✅ | ✅ | ✅ | ❌ |
| shift.close | ✅ | ✅ | ✅ | ✅ | ✅ |
| product.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| customer.view | ✅ | ✅ | ✅ | ✅ | ✅ |
| customer.manage | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Raporlama / denetim** |
| report.view | ✅ | ✅ | ✅ | ✅ | ❌ |
| report.export | ✅ | ✅ | ✅ | ✅ | ❌ |
| audit.view | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Backoffice** |
| user.view | ✅ | ✅ | ✅ | ❌ | ❌ |
| user.manage | ✅ | ✅ | ❌ | ❌ | ❌ |
| product.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| category.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| inventory.manage | ✅ | ✅ | ✅ | ❌ | ❌ |
| inventory.view | ✅ | ✅ | ✅ | ✅ | ❌ |
| settings.manage | ✅ | ✅ | ❌ | ❌ | ❌ |
| receipt.reprint | ✅ | ✅ | ✅ | ✅ | ❌ |
| receipt.void | ✅ | ✅ | ❌ | ❌ | ❌ |

\* **Waiter + payment.take:** İş kuralına bırakılabilir (ör. sadece kendi masaları, veya sadece fiş yazdırma). Matrix’te varsayılan olarak **✅** bırakıldı; kısıtlamak isterseniz **❌** yapıp sadece Cashier/Manager/Admin’e verilebilir.

---

## 4) Backoffice vs POS endpoint ayrımı

### 4.1 POS endpoint’leri (permission ile korunacak)

| Alan | İhtiyaç duyulan permission(lar) | Controller / akış |
|------|----------------------------------|--------------------|
| Sepet / sipariş | order.create, order.update, order.view, sale.create | CartController, OrdersController |
| Ödeme / fiş | payment.take, sale.view | PaymentController (process, get receipt, TSE sign) |
| İade | refund.create | Refund endpoint |
| İndirim | discount.apply | Cart/Order modifier/discount |
| Masa | table.manage, table.view | TableController |
| Kasa çekmecesi | cashdrawer.open | CashRegisterController (open) veya ayrı endpoint |
| Vardiya | shift.open, shift.close | TagesabschlussController |
| Katalog okuma | product.view | CategoriesController GET, ProductController GET, ModifierGroupsController GET |
| Müşteri | customer.view, customer.manage | CustomerController |
| Fiş listesi | sale.view | ReceiptsController |
| Basit rapor | report.view | ReportsController (POS özet) |

### 4.2 Backoffice endpoint’leri (permission ile korunacak)

| Alan | İhtiyaç duyulan permission(lar) | Controller / akış |
|------|----------------------------------|--------------------|
| Kullanıcı yönetimi | user.view, user.manage | UserManagementController, AdminUsersController |
| Denetim | audit.view | AuditLogController |
| Rapor / export | report.view, report.export | ReportsController |
| Kategori yönetimi | category.manage | CategoriesController POST, PUT, DELETE |
| Ürün / modifier | product.manage | AdminProductsController, ModifierGroupsController |
| Stok | inventory.manage, inventory.view | InventoryController |
| Ayarlar | settings.manage | SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, CashRegisterController, FinanzOnlineController |
| Fiş iptal / reprint | receipt.void, receipt.reprint | ReceiptsController (admin actions) |
| TSE debug / backfill | settings.manage (veya ayrı permission) | PaymentController signature-debug, InvoiceController backfill |

### 4.3 Hızlı referans: Endpoint → Permission

| Controller / Akış | Önerilen permission |
|-------------------|---------------------|
| CartController (tümü) | order.create, order.update |
| OrdersController (tümü) | order.create, order.update, order.view |
| PaymentController (process, get receipt, TSE sign) | payment.take, sale.view |
| PaymentController (signature-debug) | settings.manage |
| Refund endpoint | refund.create |
| TableController | table.manage, table.view |
| TagesabschlussController (open/close) | shift.open, shift.close |
| CashRegisterController (open drawer) | cashdrawer.open |
| CategoriesController GET | product.view |
| CategoriesController POST/PUT/DELETE | category.manage |
| ProductController GET, ModifierGroupsController GET | product.view |
| AdminProductsController, ModifierGroupsController yazma | product.manage |
| CustomerController | customer.view, customer.manage |
| ReceiptsController list/detail | sale.view |
| ReceiptsController reprint/void | receipt.reprint, receipt.void |
| ReportsController | report.view, report.export |
| AuditLogController | audit.view |
| UserManagementController GET | user.view |
| UserManagementController POST/PUT/deactivate/roles | user.manage |
| InventoryController | inventory.view, inventory.manage (action’a göre) |
| SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, CashRegisterController, FinanzOnlineController | settings.manage |
| InvoiceController backfill | settings.manage (veya user.manage) |

---

## 5) Migration stratejisi (minimum kırılım)

### 5.1 Faz 0 – Hazırlık (kırılım yok)

1. **Permission sabitleri**
   - Backend’de `Permissions.cs` (veya benzeri) içinde tüm permission string’leri sabit olarak tanımla.
   - Frontend’de (admin + POS) aynı permission listesini sabit veya generated type olarak tut.

2. **Rol–permission eşlemesi (statik)**
   - Backend’de tek bir yerde (ör. `RolePermissionMatrix.cs` veya config) “hangi rol hangi permission’lara sahip” matrisini tut.
   - İlk aşamada veritabanı tablosu zorunlu değil; kod içi dictionary/static config yeterli.

3. **Mevcut roller**
   - Identity’de SuperAdmin, Admin, Administrator, Manager, Cashier, Waiter rolleri seed’de olsun (Waiter eklenmemişse ekle; Kellner alias isteğe bağlı).

### 5.2 Faz 1 – Policy’leri permission’a bağla (geriye uyumlu)

4. **Permission requirement + handler**
   - `PermissionRequirement : IAuthorizationRequirement` (permission string alır).
   - `PermissionAuthorizationHandler` : Kullanıcının rollerini alır; her rol için statik matristen permission setini çıkarır; istenen permission sette varsa başarılı.
   - SuperAdmin için: tüm permission’ları otomatik ver (veya matriste “*” kuralı).

5. **Policy’leri permission ile eşle**
   - Her “anlamlı” permission için bir policy: `options.AddPolicy("Permission:report.view", p => p.Requirements.Add(new PermissionRequirement("report.view")));`
   - Veya tek generic policy + attribute: `[Authorize(Policy = "Permission"), RequirePermission("report.view")]` şeklinde custom attribute ile permission parametre verilir.

6. **Controller’larda kademeli geçiş**
   - Önce sadece **yeni** veya **kritik** endpoint’lerde `[Authorize(Policy = "Permission:report.view")]` gibi kullan.
   - Mevcut `[Authorize(Roles = "…")]` aynen kalsın; aynı endpoint’e hem role hem permission policy’yi **OR** ile bağlamayın (tek tip seçin). Tercih: Önce bir modülü (örn. Reports) tamamen permission’a geçir; diğerleri sonra.

### 5.3 Faz 2 – Tüm endpoint’leri permission’a taşı

7. **Endpoint–permission haritası**
   - Her controller action için “hangi permission gerekli” tablosunu dokümante et (bu dokümandaki tablolar kullanılabilir).

8. **Role-based attribute’ları kaldır**
   - `[Authorize(Roles = "SuperAdmin,Administrator,...")]` → `[Authorize(Policy = "Permission:sale.create")]` (veya gerekli permission) ile değiştir.
   - Bir action birden fazla permission gerektiriyorsa: tek composite policy (RequirePermission A **and** B) veya “en kısıtlayıcı” tek permission kullan.

9. **Eski policy isimlerini koruma (opsiyonel)**
   - `UsersView` → arka planda `user.view` permission’ı kontrol eden policy’ye yönlendir; böylece mevcut `[Authorize(Policy = "UsersView")]` çalışmaya devam eder.

### 5.4 Faz 3 – Veritabanı (opsiyonel, uzun vade)

10. **Permission ve RolePermission tabloları**
    - `Permissions` (Id, Name, Description, Resource, Action).
    - `RolePermissions` (RoleName, PermissionId). Seed ile doldur.
    - Handler, matrisi DB’den okusun; böylece rol–permission ilişkisi kod deploy etmeden değiştirilebilir.

11. **UserPermission (opsiyonel)**
    - Kullanıcı bazlı istisna: “Bu kullanıcı Cashier ama ekstra report.view verildi.” `UserPermissions` (UserId, PermissionId) ile yönetilir; handler önce user’a özel permission’a bakar.

---

## 6) Uygulama önerisi

### 6.1 .NET Backend

#### 6.1.1 Permission sabitleri

```csharp
// Örnek: backend/Authorization/Permissions.cs
public static class Permissions
{
    public const string SaleCreate = "sale.create";
    public const string SaleView = "sale.view";
    public const string PaymentTake = "payment.take";
    public const string RefundCreate = "refund.create";
    public const string DiscountApply = "discount.apply";
    public const string OrderCreate = "order.create";
    public const string OrderUpdate = "order.update";
    public const string OrderView = "order.view";
    public const string TableManage = "table.manage";
    public const string TableView = "table.view";
    public const string CashdrawerOpen = "cashdrawer.open";
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    public const string ProductView = "product.view";
    public const string ProductManage = "product.manage";
    public const string CategoryManage = "category.manage";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryView = "inventory.view";
    public const string SettingsManage = "settings.manage";
    public const string ReceiptReprint = "receipt.reprint";
    public const string ReceiptVoid = "receipt.void";
}
```

#### 6.1.2 Rol–permission matrisi (statik, ilk aşama)

```csharp
// Örnek: backend/Authorization/RolePermissionMatrix.cs
public static class RolePermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> RolePermissions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["SuperAdmin"] = AllPermissions(),
        ["Admin"] = AllPermissions(),
        ["Administrator"] = AllPermissions(), // legacy = Admin
        ["Manager"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.SaleCreate, Permissions.SaleView, Permissions.PaymentTake, Permissions.RefundCreate,
            Permissions.DiscountApply, Permissions.OrderCreate, Permissions.OrderUpdate, Permissions.OrderView,
            Permissions.TableManage, Permissions.TableView, Permissions.CashdrawerOpen, Permissions.ShiftOpen, Permissions.ShiftClose,
            Permissions.ProductView, Permissions.ProductManage, Permissions.CategoryManage, Permissions.CustomerView, Permissions.CustomerManage,
            Permissions.ReportView, Permissions.ReportExport, Permissions.AuditView, Permissions.UserView,
            Permissions.InventoryManage, Permissions.InventoryView, Permissions.ReceiptReprint
            // user.manage, settings.manage, receipt.void yok
        },
        ["Cashier"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.SaleCreate, Permissions.SaleView, Permissions.PaymentTake, Permissions.RefundCreate,
            Permissions.DiscountApply, Permissions.OrderCreate, Permissions.OrderUpdate, Permissions.OrderView,
            Permissions.TableManage, Permissions.TableView, Permissions.CashdrawerOpen, Permissions.ShiftOpen, Permissions.ShiftClose,
            Permissions.ProductView, Permissions.CustomerView, Permissions.CustomerManage,
            Permissions.ReportView, Permissions.ReceiptReprint
        },
        ["Waiter"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Permissions.SaleCreate, Permissions.SaleView, Permissions.PaymentTake, Permissions.DiscountApply,
            Permissions.OrderCreate, Permissions.OrderUpdate, Permissions.OrderView,
            Permissions.TableManage, Permissions.TableView, Permissions.ShiftClose,
            Permissions.ProductView, Permissions.CustomerView, Permissions.CustomerManage
            // refund, cashdrawer, shift.open, report, receipt.reprint yok
        }
    };

    public static bool RoleHasPermission(string roleName, string permission) { ... }
    public static IReadOnlySet<string> GetPermissionsForUser(IEnumerable<string> roles) { ... }
    private static HashSet<string> AllPermissions() { ... }
}
```

#### 6.1.3 Permission requirement + handler

```csharp
// PermissionRequirement.cs
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

// PermissionAuthorizationHandler.cs
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
        if (roles.Count == 0) return Task.CompletedTask;
        if (RolePermissionMatrix.GetPermissionsForUser(roles).Contains(requirement.Permission))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

#### 6.1.4 Policy kaydı

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permission:report.view", p => p.Requirements.Add(new PermissionRequirement(Permissions.ReportView)));
    options.AddPolicy("Permission:user.manage", p => p.Requirements.Add(new PermissionRequirement(Permissions.UserManage)));
    // ... her permission için bir policy VEYA tek "Permission" policy + resource filter / custom attribute
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

**Alternatif:** Tek policy + custom attribute ile action bazında permission ver:

```csharp
[RequirePermission(Permissions.ReportView)]
public async Task<IActionResult> GetReports() { ... }
```

Bunun için `RequirePermissionAttribute` ve bir `IAuthorizationFilter` (veya endpoint metadata’dan permission okuyup `IAuthorizationService.AuthorizeAsync(user, resource, "Permission:report.view")` çağıran filter) gerekir.

#### 6.1.5 Endpoint örnekleri

- Backoffice: `[Authorize(Policy = "Permission:user.manage")]` veya `[RequirePermission(Permissions.UserManage)]`
- POS: `[Authorize(Policy = "Permission:payment.take")]` PaymentController için; Cart/Order için `Permission:order.create` vb.

### 6.2 Frontend Admin (Next.js)

- **Rol listesi:** Backend’den gelen roller (UserManagement / roles endpoint) veya sabit liste: SuperAdmin, Admin, Manager, Cashier, Waiter. Atama sayfasında sadece Admin/SuperAdmin user.manage’a sahip roller kullanıcı atayabilsin.
- **Sayfa/route guard:** Backend’den kullanıcının permission listesini al (JWT claim veya `GET /me` ile permissions array). Her sayfa için “bu sayfa için gerekli permission” tanımla; yoksa 403 veya yönlendirme.
- **UI gizleme:** Buton/menü görünürlüğü `hasPermission('report.export')` gibi bir hook ile; backend zaten 403 döneceği için sadece UX için.

### 6.3 Frontend POS (Expo)

- **Auth cevabı:** Login sonrası JWT’te `permissions: string[]` claim’i (veya `GET /me` ile permissions). Mevcut `role` claim’i kalabilir; POS tarafında hem role hem permissions kullanılabilir.
- **PermissionHelper:** `PERMISSIONS` listesini backend’deki permission isimleriyle aynı yap; `hasPermission(resource, action)` backend’den gelen listeyle veya role’den türetilmiş statik matrisle hesaplansın.
- **Ekran erişimi:** `SCREEN_ACCESS` artık “hangi ekran hangi permission’ı gerektirir” olabilir; girişte kontrol edilir, yetkisi yoksa ilgili ekran gösterilmez veya 403 benzeri mesaj.

### 6.4 JWT / Me endpoint

- **Seçenek A:** JWT’e `permissions` claim’i ekle (array of strings). Token büyür; güncelleme için yeniden login gerekir.
- **Seçenek B:** `GET /api/me` veya `GET /api/user/permissions` döner: `{ roles: string[], permissions: string[] }`. Frontend bunu cache’ler (React Query vb.). Rol değişince endpoint tekrar çağrılır.
- Öneri: Başlangıçta **Seçenek B** (me/permissions endpoint); token’da sadece role(s) kalsın. İleride performans için JWT’e permissions da sıkıştırılabilir.

---

## 7) Özet

| Çıktı | Konum |
|-------|--------|
| **Rol listesi** | Bölüm 1 |
| **Permission listesi** | Bölüm 2 |
| **Role–permission matrix** | Bölüm 3 |
| **Backoffice vs POS endpoint ayrımı** | Bölüm 4 |
| **Migration stratejisi** | Bölüm 5 (Faz 0–3) |
| **.NET uygulama önerisi** | Bölüm 6.1 (sabitler, matris, requirement/handler, policy) |
| **Frontend Admin / POS önerisi** | Bölüm 6.2, 6.3, 6.4 |

Bu mimari, mevcut `[Authorize(Roles = "…")]` yapısından kademeli olarak permission tabanlı kontrole geçişi tanımlar; RKSV/TSE/fiş numaralama gibi dokunulmaması gereken alanlara müdahale etmez, sadece yetkilendirme katmanını sürdürülebilir hale getirir.
