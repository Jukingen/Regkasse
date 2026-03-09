# POS Authorization: Production-Ready Role + Permission Design

**Tarih:** 2025-03-09  
**Amaç:** ASP.NET Core POS için production’a uygun, esnek ve bakımı kolay role + permission authorization tasarımı.

---

## 1) Role enum / constants önerisi

**Öneri:** String tabanlı **sabitler** kullan; enum yerine. Sebep: Identity ve JWT’te rol adı string (AspNetRoles.Name). Enum’a çevirmek her yerde `ToString()` veya mapping gerektirir; sabitler doğrudan kullanılır.

```csharp
// backend/Authorization/Roles.cs
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Waiter = "Waiter";

    /// <summary>Legacy; treat as Admin in policies. Keep in Identity for backward compatibility.</summary>
    public const string Administrator = "Administrator";

    /// <summary>All canonical roles used in policy definitions (excluding legacy/optional).</summary>
    public static readonly IReadOnlyList<string> Canonical = new[] { SuperAdmin, Admin, Manager, Cashier, Waiter };
}
```

**Administrator vs Admin – değerlendirme:**

| Seçenek | Artı | Eksi |
|--------|------|------|
| **Merge (Admin only)** | Tek isim; karışıklık yok | Mevcut token’lar ve DB’de "Administrator" olan kullanıcılar 403 alır; migration gerekir |
| **Kalmalı (alias)** | Sıfır kırılım; mevcut token ve Identity rolleri çalışır | İki isim; dokümantasyonda "Administrator = Admin" yazılmalı |

**Öneri:** **Administrator rolü kalsın**, policy tarafında **Admin ile aynı** kabul edilsin (her policy’de `Admin` ve `Administrator` birlikte listelenir). Yeni atamalarda mümkünse **Admin** kullanılsın; token/claim’de hangisi gelirse gelsin yetki aynı olsun. İleride tüm kullanıcılar Admin’e taşınıp Administrator rolü deprecated edilebilir.

---

## 2) Permission constants önerisi

Format: `resource.action` (küçük harf, nokta ile ayrılmış). API ve frontend’de aynı string.

```csharp
// backend/Authorization/Permissions.cs
public static class Permissions
{
    // Users
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";

    // Products & Categories
    public const string ProductView = "product.view";
    public const string ProductManage = "product.manage";
    public const string CategoryView = "category.view";
    public const string CategoryManage = "category.manage";

    // Reports & Audit
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";

    // Sales & Payment
    public const string SaleCreate = "sale.create";
    public const string SaleView = "sale.view";
    public const string PaymentTake = "payment.take";
    public const string RefundCreate = "refund.create";
    public const string PriceOverride = "price.override";

    // Table & Orders
    public const string OrderCreate = "order.create";
    public const string OrderUpdate = "order.update";
    public const string OrderView = "order.view";
    public const string TableView = "table.view";
    public const string TableManage = "table.manage";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";

    // Shift & Cash
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    public const string CashdrawerOpen = "cashdrawer.open";

    // Settings & System
    public const string SettingsManage = "settings.manage";
    public const string ReceiptReprint = "receipt.reprint";
    public const string ReceiptVoid = "receipt.void";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";

    /// <summary>All permission names for validation and UI.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        UserView, UserManage, ProductView, ProductManage, CategoryView, CategoryManage,
        ReportView, ReportExport, AuditView, SaleCreate, SaleView, PaymentTake, RefundCreate, PriceOverride,
        OrderCreate, OrderUpdate, OrderView, TableView, TableManage, CustomerView, CustomerManage,
        ShiftOpen, ShiftClose, CashdrawerOpen, SettingsManage, ReceiptReprint, ReceiptVoid,
        InventoryView, InventoryManage
    };
}
```

---

## 3) Policy registration örneği

**A) Rol tabanlı (mevcut yapı – kırılım yok):**

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PosSales", policy =>
        policy.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Administrator, Roles.Manager, Roles.Cashier));
});
```

**B) Permission tabanlı (tek permission):**

```csharp
options.AddPolicy("Permission:report.view", policy =>
    policy.Requirements.Add(new PermissionRequirement(Permissions.ReportView)));
```

**C) Tüm permission’lar için policy otomatik:**

```csharp
foreach (var permission in Permissions.All)
    options.AddPolicy("Permission:" + permission, policy =>
        policy.Requirements.Add(new PermissionRequirement(permission)));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

**D) Bileşik policy (birden fazla permission – OR):**

```csharp
options.AddPolicy("ReportsAccess", policy =>
{
    policy.Requirements.Add(new PermissionRequirement(Permissions.ReportView));
    policy.Requirements.Add(new PermissionRequirement(Permissions.ReportExport));
    // Handler'da herhangi biri varsa succeed (OR) veya ayrı CompositeRequirement.
});
```

Öneri: Önce **C** ile her permission için `Permission:xxx` policy’si tanımla; controller’da `[Authorize(Policy = "Permission:report.view")]` kullan. Rol matrisi handler içinde (statik veya DB) tutulur.

---

## 4) Custom permission authorize yaklaşımı

**Seçenek 1 – Policy adı ile (önerilen):**

```csharp
[Authorize(Policy = "Permission:report.view")]
public async Task<IActionResult> GetReports() => ...
```

**Seçenek 2 – Custom attribute (okunabilir):**

```csharp
[RequirePermission(Permissions.ReportView)]
public async Task<IActionResult> GetReports() => ...
```

Bunun için:

- `RequirePermissionAttribute : TypeFilterAttribute` veya `AuthorizeAttribute` türevi.
- Endpoint metadata’da permission bilgisini taşı; authorization filter veya middleware’de `IAuthorizationService.AuthorizeAsync(user, resource, "Permission:" + permission)` çağır.

**Örnek custom attribute:**

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base("Permission:" + permission) { }
}
```

Kullanım: `[RequirePermission(Permissions.ReportView)]`. Policy adı `Permission:report.view` olur; handler permission’ı kontrol eder.

**PermissionAuthorizationHandler:** Kullanıcının claim’lerinden rollerini al (veya Identity’den); rol → permission matrisine göre (statik sınıf veya DB) bu rolün istenen permission’a sahip olup olmadığını kontrol et. SuperAdmin ise her zaman başarılı.

---

## 5) JWT claim yapısı

**Mevcut (tahmini):** `sub`, `email`, `role` (veya `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`), `exp`, `iss`, `aud`.

**Önerilen (minimum kırılım):**

| Claim | Açıklama | Örnek |
|-------|----------|--------|
| `sub` | User ID | Guid |
| `email` | Email | user@example.com |
| `role` | Tek rol (mevcut) | "Cashier" |
| `roles` (opsiyonel) | Birden fazla rol | ["Cashier","Manager"] |
| `permissions` (opsiyonel) | Yetki listesi | ["sale.create","payment.take",...] |
| `exp`, `iss`, `aud` | Standart JWT | |

**Öneri:**

- **Faz 1:** JWT’te sadece **role** (veya **roles**) kalsın; permission’lar **sunucu tarafında** rol → permission matrisinden türetilsin. Token değişmez; backend handler matrise bakar.
- **Faz 2:** Performans için JWT’e **permissions** array’i eklenebilir (login/refresh’te hesaplanıp konur). Böylece her istekte DB/Identity’e gitmeye gerek kalmaz. Rol değişince token yenilenmeli.

**Claim isimleri:** ASP.NET Core Identity ve `[Authorize(Roles="x")]` için `RoleClaimType` JWT’te ne ise o kullanılır (Program.cs’te `RoleClaimType = "role"`). Permission’lar için custom claim: `"permissions"` (array) veya `"perm"` (string, virgülle ayrılmış).

**Login cevabı (alternatif):** JWT’e permission koymak istemezsen, frontend `GET /api/me` ile `{ roles: string[], permissions: string[] }` alıp cache’leyebilir; menü/buton görünürlüğü buna göre yapılır. API çağrıları yine JWT ile gider; backend policy/handler rol veya (varsa) JWT’teki permission’a göre karar verir.

---

## 6) Kullanıcı–rol–permission veritabanı şeması önerisi

**Mevcut:** ASP.NET Identity → `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`. ApplicationUser + IdentityRole.

**Genişletme (isteğe bağlı – permission’ları DB’den yönetmek için):**

```
AspNetUsers          (mevcut)
AspNetRoles          (mevcut)   – Name: SuperAdmin, Admin, Cashier, ...
AspNetUserRoles      (mevcut)

Permissions          (yeni)
  Id (PK, int veya GUID)
  Name (unique)      – "report.view", "user.manage", ...
  Description        – optional
  Resource           – "report", "user", ...
  Action             – "view", "manage", ...

RolePermissions      (yeni)
  RoleId (FK → AspNetRoles.Id)
  PermissionId (FK → Permissions.Id)
  PK(RoleId, PermissionId)

UserPermissions      (opsiyonel – kullanıcı bazlı istisna)
  UserId (FK → AspNetUsers.Id)
  PermissionId (FK → Permissions.Id)
  PK(UserId, PermissionId)
```

**Not:** Identity’nin `AspNetRoles` tablosu string Id (GUID) kullanıyorsa `RolePermissions.RoleId` ona bağlanır. Permission’lar seed ile doldurulur; RolePermissions rol başına hangi permission’ların verileceğini tutar. Handler önce UserPermissions’a bakar (varsa), sonra kullanıcının rollerine göre RolePermissions’tan permission setini çıkarır.

**Başlangıç:** Permission ve RolePermissions **kod içi statik matris** ile de tutulabilir; migration sonrası DB’ye taşınır.

---

## 7) Eski role bazlı controller’lardan yeni policy tabanlı yapıya geçiş planı

**Hedef:** `[Authorize(Roles = "SuperAdmin,Administrator,Manager,Admin")]` → anlamlı policy veya permission policy; tek yerde rol seti.

### Faz 1 – Kırılım yok (şu anki duruma yakın)

1. **Sabitler:** `Roles.cs` ve `Permissions.cs` ekle; controller’larda string yerine `Roles.Admin` kullanma zorunlu değil, policy isimleri aynen kalsın.
2. **Policy’ler rol tabanlı kalsın;** Program.cs’teki mevcut policy’ler (PosSales, BackofficeSettings, …) aynen kullanılmaya devam etsin. Controller’larda zaten `[Authorize(Policy = "PosSales")]` kullanılıyor; **Roles = "..."** kaldırıldı.
3. **Administrator:** Tüm policy’lerde `Admin` ve `Administrator` birlikte kalsın; mevcut kullanıcılar etkilenmez.

### Faz 2 – Permission altyapısı

4. **PermissionRequirement** ve **PermissionAuthorizationHandler** ekle; rol → permission matrisi **statik** (C# dictionary) olsun.
5. **Policy kaydı:** Her permission için `Permission:xxx` policy’si ekle (Program.cs’te döngü ile). Mevcut rol tabanlı policy’leri **silme**; birlikte çalışsın.
6. **Test:** Bir controller’da (örn. Reports) `[Authorize(Policy = "Permission:report.view")]` dene; matriste Manager/Cashier vb. doğru tanımlı mı kontrol et.

### Faz 3 – Controller’ları permission’a taşıma (kademeli)

7. Modül modül geç: Reports → `Permission:report.view` / `Permission:report.export`; Users → user.view / user.manage; Settings → settings.manage; Sales → sale.create, payment.take; vb.
8. Eski policy isimlerini **alias** yapmak istersen: `BackofficeManagement` policy’si, arka planda `Permission:report.view` OR `Permission:user.view` … gibi bileşik kurala çevrilebilir; böylece eski attribute’lar çalışmaya devam eder.
9. Tüm controller’lar permission policy’ye geçince rol tabanlı policy’leri kaldırabilir veya sadece alias olarak bırakabilirsin.

### Faz 4 – İsteğe bağlı DB

10. Permissions ve RolePermissions tabloları + migration; seed ile doldur. Handler matrisi DB’den okusun; yeni roller/permission’lar kod deploy etmeden eklenebilir.

**Minimum kırılım özeti:** Faz 1 zaten uygulandı (policy’ler var). Faz 2’de sadece yeni sınıflar ve ek policy’ler gelir; mevcut `[Authorize(Policy = "PosSales")]` değişmez. Faz 3’te sadece değiştirmek istediğin endpoint’leri tek tek permission’a taşırsın; diğerleri aynen kalır.

---

## 8) Frontend’de menu / button / page authorization yaklaşımı

**Kaynak:** Backend’den gelen yetki bilgisi. İki yol:

- **A)** JWT’te `permissions: string[]` (veya `role` + istemcide statik rol→permission matrisi).
- **B)** Login sonrası veya sayfa yüklenirken `GET /api/me` → `{ roles, permissions }`; frontend bunu cache’ler (React Query, Context, vb.).

**Menü görünürlüğü:**

- Her menü öğesi için gerekli permission (veya rol) tanımla. Örn. "Reports" menüsü → `report.view`.
- `hasPermission('report.view')` veya `hasRole('Manager')` ile menü öğesini render et veya gizle.
- Route guard: Sayfa için gerekli permission yoksa 403 sayfasına veya ana sayfaya yönlendir.

**Buton görünürlüğü:**

- "İade" butonu → `refund.create`. "Kasa çekmecesi aç" → `cashdrawer.open`. "Vardiya kapat" → `shift.close`.
- Aynı `hasPermission(...)` ile butonu göster/gizle veya disabled yap. Backend zaten 403 döneceği için gizleme sadece UX içindir.

**Önerilen yapı (React/Next/Expo):**

- `useAuth()` veya `usePermissions()`: `{ user, roles, permissions }` dönsün. `permissions` backend’den (GET /me veya JWT) veya role’den türetilmiş statik matristen.
- `hasPermission(perm: string): boolean` ve `hasAnyPermission(perms: string[]): boolean`.
- `<PermissionGuard permission="user.manage">` veya `<RequirePermission permission="report.export">` ile sadece yetkisi olanlara içerik göster.
- Route seviyesinde: Protected layout’ta `permissions` veya `roles` kontrolü; yetki yoksa redirect.

**Backend ile senkron:** Permission string’leri backend’deki `Permissions.cs` ile **bire bir aynı** olsun; frontend’de sabit veya generated type kullan.

---

## 9) Özet

| Konu | Öneri |
|------|--------|
| **Roller** | `Roles.cs` sabitleri; SuperAdmin, Admin, Manager, Cashier, Waiter (+ Administrator legacy) |
| **Administrator** | Kalsın; policy’lerde Admin ile birlikte kullanılsın; ileride merge edilebilir |
| **Permission** | `Permissions.cs` sabitleri; `resource.action` formatı |
| **Policy** | Rol tabanlı mevcut policy’ler + Permission tabanlı `Permission:xxx` (handler ile) |
| **Custom authorize** | `[RequirePermission(Permissions.ReportView)]` → policy `Permission:report.view` |
| **JWT** | En azından `role` (veya `roles`); opsiyonel `permissions` array |
| **DB şeması** | Identity aynen; opsiyonel Permissions + RolePermissions (+ UserPermissions) |
| **Geçiş** | Faz 1 tamam; Faz 2 handler + permission policy; Faz 3 kademeli controller; Faz 4 opsiyonel DB |
| **Frontend** | GET /me veya JWT’ten permissions; hasPermission + menü/buton/route guard |

Bu tasarım, mevcut policy tabanlı controller’larla uyumludur; permission’a geçiş kademeli ve minimum kırılımla yapılabilir.
