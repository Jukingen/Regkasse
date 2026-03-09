# Role-Based → Permission-Based: Veritabanı Migration Planı

**Tarih:** 2025-03-09

**Bağlam:** POS + backoffice hibrit; hedef role + permission; Administrator legacy alias; minimum kırılımlı geçiş. Mevcut: AspNetUsers (user.role kolonu), AspNetRoles, AspNetUserRoles.

---

## 1) Mevcut durum

| Tablo / Alan | Açıklama |
|--------------|----------|
| **AspNetUsers** | Identity; ApplicationUser. **role** kolonu (legacy, tekil rol) – `[Column("role")]`, max 20 char, default "Cashier". |
| **AspNetRoles** | Identity; IdentityRole. Rol adları: Administrator, Admin, Cashier, Waiter, Manager, SuperAdmin, vb. |
| **AspNetUserRoles** | Identity; UserId + RoleId. Kullanıcı–rol çoklu ilişki. |
| **Rol çözümleme (kod)** | Önce `UserManager.GetRolesAsync(user)` (AspNetUserRoles), yoksa `user.Role` fallback. |

Migration sırasında hem `user.role` hem AspNetUserRoles kullanılmaya devam edebilir; hedefte tek kaynak (AspNetUserRoles + yeni RolePermissions) olur.

---

## 2) Hedef veri yapısı

Identity tabloları **kaldırılmaz**; üzerine permission tabloları eklenir.

| Tablo | Açıklama |
|-------|----------|
| **AspNetRoles** (mevcut) | Rol adları. Roller burada kalır; RolePermissions `role_name` ile AspNetRoles.Name’e mantıksal eşlenir. |
| **AspNetUserRoles** (mevcut) | User–Role ilişkisi. UserRoles = bu tablo. |
| **Permissions** (yeni) | Tüm permission string’leri (örn. payment.take). Tek kaynak. |
| **RolePermissions** (yeni) | Rol adı → permission eşlemesi. AspNetRoles.Name ile tutarlı. |
| **UserPermissions** (opsiyonel) | Kullanıcı bazlı permission override/ek. İleride eklenebilir. |

`user.role` kolonu **ilk aşamada silinmez**; sadece okuma fallback veya raporlama için bırakılabilir. İleride kaldırılabilir.

---

## 3) Şema önerisi

### 3.1 Permissions

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | int (PK, identity) | Surrogate key. |
| name | varchar(128) NOT NULL UNIQUE | Örn. "payment.take", "order.update". |

Tablo adı: `permissions` (snake_case, proje convention).

### 3.2 RolePermissions

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| role_name | varchar(256) NOT NULL | AspNetRoles.Name ile aynı (SuperAdmin, Admin, Administrator, Manager, …). |
| permission_id | int NOT NULL | FK → permissions(id). |
| (PK) | (role_name, permission_id) | Tekil çift. |

Tablo adı: `role_permissions`.  
**Not:** AspNetRoles.Id kullanmak yerine role_name kullanmak, seed ve RolePermissionMatrix ile uyumu kolaylaştırır; Identity’de rol silinip yeniden oluşturulursa Id değişir, name sabit kalır.

### 3.3 UserPermissions (opsiyonel)

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| user_id | varchar(450) NOT NULL | FK → AspNetUsers(Id). |
| permission_id | int NOT NULL | FK → permissions(id). |
| (PK) | (user_id, permission_id) | Kullanıcıya özel ek/override. |

Tablo adı: `user_permissions`. Faz 2+ için.

---

## 4) Fazlı migration sırası (minimum kırılım)

| Faz | İş | Kırılma | Rollback |
|-----|----|---------|----------|
| **Faz 1** | `permissions` tablosu ekle; PermissionCatalog.All ile seed et. | Yok. | Migration geri alınır; tablo silinir. |
| **Faz 2** | `role_permissions` tablosu ekle; RolePermissionMatrix ile seed et (SuperAdmin, Admin, Administrator, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). | Yok. | Migration geri alınır. |
| **Faz 3** | Uygulama: TokenClaimsService / permission servisi önce DB’den RolePermissions okuyacak şekilde genişlet; cache veya fallback olarak mevcut RolePermissionMatrix (in-memory) kullan. DB’de kayıt yoksa in-memory matrix kullan. | Düşük. | Feature flag veya config ile DB okumayı kapat; eski matrix’e dön. |
| **Faz 4** | (İsteğe bağlı) user.role kolonunu “deprecated” ilan et; tüm rol bilgisini AspNetUserRoles’tan al. Sonra bir migration ile kolonu null yapılabilir veya kaldırılır. | Orta. | Kolonu geri ekleyen migration; kodda fallback tekrar açılır. |
| **Faz 5** | (İsteğe bağlı) user_permissions tablosu ekle; kullanıcı bazlı override/ek permission. | Düşük. | Tabloyu kaldıran migration. |

Öneri: Önce Faz 1 + Faz 2 (tablolar + seed) uygulansın; kod tarafı Faz 3’te DB’yi kullanacak şekilde güncellenir.

---

## 5) Seed stratejisi

### 5.1 Permissions seed

`PermissionCatalog.All` (veya AppPermissions sabitleri) ile aynı liste. Her `resource.action` bir satır.

Örnek (kısaltılmış):

| name |
|------|
| user.view |
| user.manage |
| order.view |
| order.update |
| order.cancel |
| payment.view |
| payment.take |
| payment.cancel |
| refund.create |
| audit.view |
| audit.export |
| audit.cleanup |
| settings.view |
| settings.manage |
| finanzonline.view |
| finanzonline.manage |
| finanzonline.submit |
| inventory.view |
| inventory.manage |
| inventory.adjust |
| … |

### 5.2 RolePermissions seed (rol → permission seti)

| Rol | Açıklama | Permission seti |
|-----|----------|-------------------|
| **SuperAdmin** | Tüm yetkiler | Tüm permissions (PermissionCatalog.All). |
| **Admin** | Tam işletme yöneticisi | Tüm permissions. |
| **Administrator** | Legacy alias | Admin ile **aynı** set (tüm permissions). |
| **Manager** | Operasyon yöneticisi | user.view, role.view, product.*, category.*, order.*, table.*, cart.*, sale.*, payment.view/take/cancel, refund.create, cashregister.view, cashdrawer.*, shift.*, inventory.view/manage, customer.*, invoice.view/manage/export, report.*, audit.view/export, settings.view, finanzonline.view, kitchen.*, price.override, receipt.reprint, discount.apply; **yok:** user.manage, role.manage, settings.manage, audit.cleanup, finanzonline.manage/submit, creditnote.create, inventory.adjust. |
| **Cashier** | Ödeme/kasa/vardiya | product.view, category.view, modifier.view, order.view/create/update, table.*, cart.*, sale.*, payment.*, refund.create, cashregister.view, cashdrawer.*, shift.*, inventory.view, customer.*, invoice.view, kitchen.view, price.override, receipt.reprint, discount.apply. |
| **Waiter** | Sipariş/masa/cart | product.view, category.view, modifier.view, order.*, table.*, cart.*, sale.*, payment.view/take, shift.view/close, customer.*, kitchen.view; **yok:** payment.cancel, refund.create, price.override. |
| **Kitchen** | Mutfak ekranı | order.view/update, product.view, category.view, kitchen.view/update. |
| **ReportViewer** | Salt rapor/export | report.view/export, audit.view/export, invoice.view/export, payment.view, settings.view. |
| **Accountant** | Muhasebe rapor | ReportViewer ile aynı. |

Seed script/migration içinde bu setler RolePermissionMatrix (in-memory) ile tutarlı olmalı; tek kaynak olarak önce RolePermissionMatrix’ten okuyup DB’ye yazmak önerilir.

---

## 6) Seed tablosu (özet)

**permissions:** Yukarıdaki name listesi; ID 1..N atanır.

**role_permissions:** Her rol için ilgili permission_id’ler. Örnek (role_name, permission_id):

- (SuperAdmin, 1), (SuperAdmin, 2), … (tümü)
- (Admin, 1), (Admin, 2), …
- (Administrator, 1), (Administrator, 2), … (Admin ile aynı)
- (Manager, …), (Cashier, …), (Waiter, …), (Kitchen, …), (ReportViewer, …), (Accountant, …)

Seed’in idempotent olması için: INSERT … ON CONFLICT DO NOTHING (PostgreSQL) veya “if not exists” mantığı.

---

## 7) EF Core migration yaklaşımı

### 7.1 Entity taslağı

```csharp
// Models/Permission.cs
namespace KasseAPI_Final.Models;

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;  // "payment.take", etc.
}

// Models/RolePermission.cs (join entity; role name stored, no FK to AspNetRoles to avoid coupling)
namespace KasseAPI_Final.Models;

public class RolePermission
{
    public string RoleName { get; set; } = string.Empty;  // AspNetRoles.Name
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
```

### 7.2 Configuration (OnModelCreating veya IEntityTypeConfiguration)

```csharp
// Permissions
builder.Entity<Permission>(e =>
{
    e.ToTable("permissions");
    e.HasKey(x => x.Id);
    e.Property(x => x.Name).IsRequired().HasMaxLength(128);
    e.HasIndex(x => x.Name).IsUnique();
});

// RolePermissions
builder.Entity<RolePermission>(e =>
{
    e.ToTable("role_permissions");
    e.HasKey(x => new { x.RoleName, x.PermissionId });
    e.Property(x => x.RoleName).HasMaxLength(256);
    e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
});
```

### 7.3 Migration adımları

1. Entity ve configuration ekle; `DbSet<Permission>`, `DbSet<RolePermission>` ekle.
2. `dotnet ef migrations add AddPermissionsAndRolePermissions` çalıştır.
3. Up içinde: `CreateTable("permissions", ...)`, `CreateTable("role_permissions", ...)`.
4. Seed: Data migration veya ayrı bir `SeedPermissionsAndRolePermissions()` metodu (Program veya startup’ta). Seed’de PermissionCatalog.All ve RolePermissionMatrix kullanılabilir; böylece tek kaynak kod olur.
5. Down: `DropTable("role_permissions")`, `DropTable("permissions")`.

### 7.4 Seed’i migration’da çalıştırma (opsiyonel)

EF Core 5+ `migrationBuilder.Sql()` veya `InsertData` ile seed eklenebilir. Daha bakımı kolay yol: uygulama başlarken (Program.cs veya hosted service) `SeedPermissionsAndRolePermissions()` çağrısı; PermissionCatalog ve RolePermissionMatrix’ten okuyup DB’ye yazar. Böylece permission/rol listesi değişince sadece C# güncellenir, migration’da büyük SQL tutulmaz.

---

## 8) Rollback notları

| Durum | Rollback |
|-------|----------|
| Sadece Faz 1+2 (tablolar + seed) uygulandı, kod henüz DB kullanmıyor | `dotnet ef database update <ÖncekiMigration>` veya Down migration ile `permissions` ve `role_permissions` tabloları silinir. Uygulama davranışı değişmez (zaten in-memory matrix kullanılıyor). |
| Faz 3: Kod DB’den okuyor | Config/feature flag ile “permission source = in-memory” yapılır; TokenClaimsService veya ilgili servis tekrar RolePermissionMatrix (statik) kullanır. Gerekirse RolePermissions/Permissions tablolarını drop eden migration çalıştırılır. |
| user.role kolonu kaldırıldı (Faz 4) | Yeni migration ile `role` kolonu tekrar eklenir; kodda GetRolesAsync başarısızsa user.Role fallback’i geri konur. |
| user_permissions kullanıma alındı | Servisler önce UserPermissions’a bakmayı bırakır; tablo drop edilebilir. |

Veri yedekleme: Migration öncesi (özellikle Faz 4’te kolon siliniyorsa) AspNetUsers’ın ilgili yedeği alınmalı. Faz 1–2 sadece yeni tablo eklediği için mevcut kullanıcı/rol verisi değişmez.

---

## 9) Opsiyonel: UserPermissions entity taslağı

```csharp
// Models/UserPermission.cs (Faz 5 / ihtiyaç halinde)
public class UserPermission
{
    public string UserId { get; set; } = string.Empty;  // AspNetUsers.Id
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
// PK (UserId, PermissionId); FK UserId -> AspNetUsers(Id), PermissionId -> permissions(Id)
```

---

## 10) Seed verisini koddan üretme (tek kaynak)

Permission ve RolePermissions seed’i, kod tarafında **PermissionCatalog.All** ve **RolePermissionMatrix** kullanılarak üretilebilir; böylece liste sadece C# sabitlerinde tutulur:

- `PermissionCatalog.All` → her biri için `permissions` tablosuna INSERT (name).
- Her rol için `RolePermissionMatrix.GetPermissionsForRoles(new[] { roleName })` → dönen her permission için `role_permissions(role_name, permission_id)` INSERT.

Rol listesi: Roles.Canonical + Roles.Administrator (legacy). Bu sayede DB seed’i ile in-memory matrix her zaman uyumlu kalır.
