# Login: Role → Permission Claim Üretim Akışı Tasarımı

**Tarih:** 2025-03-09

**Bağlam:** POS + backoffice hibrit; hedef role + permission; Administrator legacy alias; JWT/cookie’da hem role hem permission; ileride tenant/branch scope.

---

## 1) Login akışı

1. **Client** `POST /api/Auth/login` ile `email` + `password` gönderir.
2. **AuthController** kullanıcıyı bulur, aktiflik ve şifre kontrolü yapar.
3. **LastLoginAt / LoginCount** güncellenir (audit için).
4. **Rol çözümleme:** `UserManager.GetRolesAsync(user)` → Identity’deki roller. Boşsa **legacy** `user.Role` kullanılır.
5. **Claim üretimi:** `ITokenClaimsService.BuildClaimsAsync(user, roles)` çağrılır:
   - `sub` (NameIdentifier), `email`, `name`, `user_id`
   - `role` (canonical, tek ana rol): Administrator → Admin (RoleCanonicalization)
   - `roles` (tüm roller, canonical)
   - Her bir permission için ayrı `permission` claim: RolePermissionMatrix.GetPermissionsForRoles(roles)
   - (İleride) `tenant_id`, `branch_id` — user veya user-branch tablosundan
6. **JWT** bu claim’lerle üretilir; access token client’a dönülür.
7. **Yanıt** içinde `user.role`, `user.roles`, `user.permissions` da gönderilir (UI için).

```
[Client] --> POST /api/Auth/login
[AuthController] --> FindByEmailAsync, CheckPasswordAsync
[AuthController] --> GetRolesAsync(user)  --> roles
[AuthController] --> BuildClaimsAsync(user, roles)  --> claims
[TokenClaimsService] --> RoleCanonicalization.GetCanonicalRole(primary)
[TokenClaimsService] --> RolePermissionMatrix.GetPermissionsForRoles(roles)
[AuthController] --> GenerateJwtToken(claims)
[AuthController] --> Ok({ token, user: { role, roles, permissions } })
```

---

## 2) Refresh token akışı

1. **Client** süresi dolmak üzere olan access token ile `POST /api/Auth/refresh` ve `refresh_token` gönderir.
2. **Sunucu** refresh token’ı doğrular (gelecekte RefreshToken tablosu / blacklist ile).
3. Refresh token geçerliyse **kullanıcı kimliği** (sub) çıkarılır; kullanıcı ve roller yeniden yüklenir.
4. **Aynı claim üretimi:** `BuildClaimsAsync(user, roles)`; isteğe bağlı `tenant_id`/`branch_id` eklenir.
5. Yeni **access token** (ve isteğe bağlı yeni refresh token) dönülür.

**Mevcut durum:** `POST /api/Auth/refresh` henüz tam implemente değil (TODO). Tam tasarım:

- Refresh token store (DB veya distributed cache); rotation ve süre politikası.
- Refresh isteğinde: validate refresh token → userId → GetUser + GetRolesAsync → BuildClaimsAsync → yeni JWT.

---

## 3) Administrator → Admin permission set eşlemesi

- **Identity’de** kullanıcıya "Administrator" rolü atanmış olabilir (legacy).
- **RoleCanonicalization:** `GetCanonicalRole("Administrator")` → `"Admin"`. Token’daki `role` claim’i **Admin** yazılır.
- **RolePermissionMatrix:** `Administrator` anahtarı Admin ile **aynı set** (tüm permission’lar). `GetPermissionsForRoles(["Administrator"])` = Admin’in permission set’i.
- Sonuç: Token’da `role=Admin` ve aynı permission listesi; policy’lerde de Admin ile aynı yetki.

```csharp
// RolePermissionMatrix içinde
[Roles.Administrator] = all;  // Admin ile aynı set; legacy alias
```

---

## 4) Token’a yazılacak claim’ler

| Claim tipi   | Kaynak | Açıklama |
|-------------|--------|----------|
| `sub`       | user.Id | NameIdentifier; standart JWT. |
| `name`      | user.Email veya FirstName+LastName | Görünen ad. |
| `email`     | user.Email | |
| `user_id`   | user.Id | Uygulama tarafı kullanımı. |
| `role`      | RoleCanonicalization.GetCanonicalRole(primaryRole) | Tek canonical rol (Administrator→Admin). |
| `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` | Aynı canonical role | ASP.NET Core IsInRole için. |
| `roles`     | Tüm roller (canonical) | Çoklu rol; her biri ayrı claim. |
| `permission`| RolePermissionMatrix.GetPermissionsForRoles(roles) | Her permission ayrı claim; PermissionAuthorizationHandler bunu kullanır. |
| `tenant_id` | (İleride) user.TenantId veya user-tenant tablosu | Çok tenant için. |
| `branch_id` | (İleride) user.BranchId veya user-branch ataması | Şube kısıtı; scope kontrolünde kullanılır. |

---

## 5) C# örnek kod

### ITokenClaimsService (mevcut + tenant/branch hazırlığı)

```csharp
public interface ITokenClaimsService
{
    Task<IReadOnlyList<Claim>> BuildClaimsAsync(
        ApplicationUser user,
        IList<string> roles,
        string? tenantId = null,
        string? branchId = null,
        CancellationToken cancellationToken = default);
}
```

### TokenClaimsService.BuildClaimsAsync (özet)

```csharp
public Task<IReadOnlyList<Claim>> BuildClaimsAsync(
    ApplicationUser user,
    IList<string> roles,
    string? tenantId = null,
    string? branchId = null,
    CancellationToken cancellationToken = default)
{
    var list = new List<Claim>();

    list.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
    list.Add(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
    list.Add(new Claim(ClaimTypes.Name, user.Name)); // veya Email
    list.Add(new Claim("user_id", user.Id));

    var primaryRole = roles?.FirstOrDefault() ?? user.Role ?? "User";
    var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);
    list.Add(new Claim("role", canonicalRole));
    list.Add(new Claim(ClaimTypes.Role, canonicalRole));

    if (roles != null)
        foreach (var r in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            if (!string.IsNullOrEmpty(RoleCanonicalization.GetCanonicalRole(r)))
                list.Add(new Claim("roles", RoleCanonicalization.GetCanonicalRole(r)!));

    var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles ?? Array.Empty<string>());
    foreach (var p in permissions)
        list.Add(new Claim(PermissionCatalog.PermissionClaimType, p));

    if (!string.IsNullOrEmpty(tenantId))
        list.Add(new Claim(ScopeCheckService.TenantIdClaim, tenantId));
    if (!string.IsNullOrEmpty(branchId))
        list.Add(new Claim(ScopeCheckService.BranchIdClaim, branchId));

    return Task.FromResult<IReadOnlyList<Claim>>(list);
}
```

### Login (AuthController) – claim kullanımı

```csharp
var roles = await _userManager.GetRolesAsync(user);
var claims = await _tokenClaimsService.BuildClaimsAsync(user, roles); // ileride: tenantId, branchId
var token = GenerateJwtToken(claims);
```

### Refresh (taslak)

```csharp
// Validate refresh token → userId
var user = await _userManager.FindByIdAsync(userId);
var roles = await _userManager.GetRolesAsync(user);
var claims = await _tokenClaimsService.BuildClaimsAsync(user, roles);
var newAccessToken = GenerateJwtToken(claims);
return Ok(new { token = newAccessToken, expiresIn = 3600 });
```

---

## 6) PermissionClaimsFactory benzeri servis önerisi

Mevcut **ITokenClaimsService / TokenClaimsService** zaten bu işi yapıyor: login ve refresh sırasında role → permission claim üretimi. İsimlendirme olarak:

- **TokenClaimsService** = “JWT için claim listesi üreten servis” (role + permission + ileride scope). Ayrı bir “PermissionClaimsFactory” sınıfı eklemek gerekmez; tek sorumluluk TokenClaimsService’te kalabilir.
- İleride **user-role** ve **role-permission** DB tablolarına geçilirse: permission’lar yine RolePermissionMatrix benzeri bir kaynaktan (DB veya cache) alınır; TokenClaimsService bu kaynağı kullanır. Factory pattern isterseniz `IClaimsFactory` arayüzü TokenClaimsService’e uygulanabilir; şu an tek implementasyon olduğu için ayrı interface zorunlu değil.

---

## 7) Scope kontrolü için service layer önerisi

**Amaç:** Waiter sadece kendi siparişini, cashier kendi aktif kasasını, manager kendi branch verisini görsün/güncellesin.

### IScopeCheckService (mevcut + kaynak bazlı)

Mevcut: `IsInScope(user, requiredTenantId, requiredBranchId)`, `GetCurrentTenantId`, `GetCurrentBranchId`.

Önerilen ek metodlar (kaynak sahipliği / branch eşleşmesi):

```csharp
bool CanAccessOrder(ClaimsPrincipal user, string? orderAssignedUserId, string? orderBranchId);
bool CanAccessCashRegister(ClaimsPrincipal user, string? registerAssignedUserId, string? registerBranchId);
bool CanAccessBranch(ClaimsPrincipal user, string branchId);
```

**Mantık (özet):**

- **tenant_id:** Varsa, kaynağın tenant’ı ile eşleşmeli.
- **branch_id:** Varsa, kaynağın branch’i ile eşleşmeli. SuperAdmin/Admin’de branch claim yoksa tüm branch’lere izin verilebilir.
- **Waiter – kendi siparişi:** `orderAssignedUserId == user.Id` veya kullanıcının branch’i sipariş branch’i ile aynı ve rol Manager+ (politikaya göre).
- **Cashier – kendi aktif kasası:** `registerAssignedUserId == user.Id` veya branch eşleşmesi + yetki.
- **Manager – kendi branch:** `GetCurrentBranchId(user) == branchId` veya branch claim yok (tüm branch).

Controller kullanımı:

- Sipariş güncellemeden önce: siparişi yükle, `_scopeCheck.CanAccessOrder(User, order.AssignedWaiterId, order.BranchId)` kontrol et.
- Kasa işleminden önce: `_scopeCheck.CanAccessCashRegister(User, register.CurrentUserId, register.BranchId)`.
- Branch’e özel liste: `_scopeCheck.CanAccessBranch(User, branchId)` veya listeyi GetCurrentBranchId’ye göre filtrele.

Bu metodlar **IScopeCheckService** ve **ScopeCheckService** içinde implemente edilmiştir; tenant_id/branch_id claim’leri TokenClaimsService ile doldurulduğunda anlamlı olur.

---

## 8) Yapılan kod güncellemeleri (özet)

| Dosya | Değişiklik |
|-------|------------|
| `ITokenClaimsService` | `BuildClaimsAsync` imzasına opsiyonel `tenantId`, `branchId` eklendi; `name` claim için `user.Name` kullanılır. |
| `TokenClaimsService` | `tenant_id` ve `branch_id` claim’leri (değer verilmişse) ekleniyor; `ClaimTypes.Name` = `user.Name`. |
| `IScopeCheckService` | `CanAccessOrder`, `CanAccessCashRegister`, `CanAccessBranch` metodları eklendi. |
| `ScopeCheckService` | Yukarıdaki üç metod implemente edildi; branch/user eşleşmesi ve Admin/Manager/SuperAdmin için tam erişim. |
| `AuthController` | Mevcut `BuildClaimsAsync(user, roles)` çağrısı aynı kaldı (opsiyonel parametreler null). İleride tenant/branch kullanıcı veya atama tablosundan alınıp geçirilebilir. |
