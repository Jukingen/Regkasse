# POS Authorization: Swagger Endpoint Yapısına Göre Role + Permission Migration Tasarımı

**Tarih:** 2025-03-09  
**Amaç:** Mevcut role bazlı yapıyı minimum kırılımla permission tabanlı yapıya evirmek; Administrator’ı Admin altında legacy alias yapmak; backoffice ile POS’u net ayırmak; kritik endpoint’leri önce permission policy ile korumak; frontend guard/menu stratejisini tanımlamak.

**Referans:** Swagger’da `/api/admin/*` backoffice, `/api/pos/*` yeni POS yüzeyi; `/api/Cart`, `/api/Payment` legacy/deprecated.

---

## A) Endpoint bazlı permission mapping tablosu

### A.1 Backoffice (admin/products, admin/categories, settings, FinanzOnline, AuditLog cleanup/export, CompanySettings update)

| Endpoint / Route | HTTP | Önerilen permission | Not |
|------------------|------|----------------------|-----|
| /api/admin/categories | GET, GET {id}, GET {id}/products, GET search | category.view (veya product.view) | Katalog okuma |
| /api/admin/categories | POST, PUT {id}, DELETE {id} | category.manage | Katalog yazma |
| /api/admin/products | GET, GET {id}, GET search, GET {id}/modifier-groups | product.view | Ürün okuma |
| /api/admin/products | POST, PUT {id}, PUT stock/{id}, DELETE {id}, POST {id}/modifier-groups | product.manage | Ürün yazma |
| /api/modifier-groups | GET, GET {id} | product.view | Modifier okuma |
| /api/modifier-groups | POST, PUT {id}, DELETE {id}, POST {id}/products, DELETE {groupId}/products/{productId} | product.manage | Modifier yazma |
| /api/CompanySettings | PUT, PUT business-hours, PUT banking, PUT localization, PUT billing, GET export | settings.manage | Şirket ayarları |
| /api/Settings | PUT, PUT tax-rates, GET backup, POST backup/now, PUT notifications, GET export | settings.manage | Sistem ayarları |
| /api/Localization | PUT, POST add-language, POST add-currency, DELETE remove-language, DELETE remove-currency, GET export | settings.manage | Yerelleştirme |
| /api/MultilingualReceipt | POST, PUT {id}, DELETE {id}, GET export | settings.manage | Fiş şablonu |
| /api/FinanzOnline | GET config, PUT config, POST submit-invoice, GET errors, POST test-connection, GET history/{id} | settings.manage (config/test), finanzonline.submit (submit-invoice) | Yüksek risk: config + submit |
| /api/CashRegister | POST | settings.manage (veya cashregister.manage) | Kasa kaydı oluşturma |
| /api/AuditLog | GET, GET {id}, GET user/{id}, GET correlation/{id}, GET suspicious-admin-actions, GET transaction/{id}, GET statistics, GET payment/{id} | audit.view (payment için audit.view veya audit.view_own) | Denetim görüntüleme |
| /api/AuditLog | DELETE cleanup, GET export | audit.admin | **Yüksek risk** |
| /api/UserManagement | GET, GET {id}, GET roles | user.view | Kullanıcı görüntüleme |
| /api/UserManagement | POST, PUT {id}, PUT deactivate, PUT reactivate, PUT reset-password, DELETE {id}, POST roles | user.manage | Kullanıcı yönetimi |
| /api/admin/users | (tümü) | user.manage | AdminUsersController |
| /api/Inventory | POST, PUT {id}, POST {id}/restock, POST {id}/adjust | inventory.manage | Stok yazma |
| /api/Inventory | DELETE {id} | inventory.delete | Sadece Admin |
| /api/Invoice | POST backfill-from-payments | system.critical | **Yüksek risk** |
| /api/* (EntityController) | Kalıcı silme | system.critical | **Yüksek risk** |

### A.2 POS (api/pos/cart, api/pos/payment, Cart, Payment, Orders, Table, Receipts, TSE, Tagesabschluss)

| Endpoint / Route | HTTP | Önerilen permission | Not |
|------------------|------|----------------------|-----|
| /api/pos/cart, /api/Cart | GET current, GET {cartId}, POST, POST add-item, POST {cartId}/items, PUT items/{itemId}, PUT {cartId}/items/{itemId} | sale.create | Sepet (legacy + pos) |
| /api/pos/payment, /api/Payment | GET methods, POST, GET {id}, GET customer/{id}, GET method/{method}, GET date-range, GET {id}/receipt, GET {id}/qr.* | sale.view, payment.take | Ödeme akışı |
| /api/pos/payment, /api/Payment | POST {id}/cancel | payment.cancel | **Yüksek risk** |
| /api/pos/payment, /api/Payment | POST {id}/refund | refund.create | **Yüksek risk** |
| /api/pos/payment, /api/Payment | POST {id}/tse-signature | payment.take | TSE imza |
| /api/pos/payment, /api/Payment | GET {id}/signature-debug, POST verify-signature | system.critical (veya settings.manage) | TSE tanılama |
| /api/Receipts | GET {receiptId}, POST create-from-payment/{id}, GET {id}/signature-debug | sale.view, payment.take | Fiş |
| /api/Orders | GET, GET {id}, POST, PUT {id}/status, DELETE {id}, GET status/{status} | order.view, order.create, order.update | Sipariş |
| /api/Table | GET, GET {id}, POST {id}/status | table.view, table.manage | Masa |
| /api/Customer | GET number/{nr}, GET email/{e}, GET tax/{t}, POST, PUT {id}, GET search | customer.view, customer.manage | Müşteri |
| /api/Product (api/pos), /api/Product | GET list, GET all, GET catalog, GET active, GET categories, GET category/{name}, GET search, GET {id}/modifier-groups | product.view | Katalog okuma (POS) |
| /api/Product | POST {id}/modifier-groups, POST, PUT {id}, PUT stock/{id} | product.manage | Ürün yazma (karma route) |
| /api/Tse | GET status, POST connect, POST signature, POST disconnect, GET devices | shift.open, shift.close (veya payment.take) | TSE |
| /api/Tagesabschluss | POST daily, POST monthly, POST yearly, GET history, GET can-close/{id}, GET statistics | shift.close, shift.open | Vardiya |

### A.3 Reporting

| Endpoint | HTTP | Önerilen permission |
|----------|------|----------------------|
| /api/Reports | GET sales, GET products, GET customers, GET inventory, GET payments | report.view |
| /api/Reports | GET export/sales | report.export |

### A.4 Ek (yüksek riskli) permission önerisi

Mevcut `Permissions.cs` ile uyumlu; eksik olanlar eklenebilir:

| Permission | Kullanım |
|------------|----------|
| payment.cancel | Payment POST {id}/cancel |
| audit.admin | AuditLog DELETE cleanup, GET export |
| finanzonline.submit | FinanzOnline POST submit-invoice (isteğe bağlı; yoksa settings.manage) |
| system.critical | Invoice backfill, EntityController hard delete, Payment signature-debug/verify |

---

## B) Backend policy tasarımı

### B.1 ASP.NET Core Permissions sabit sınıfı (mevcut + ekler)

Mevcut `backend/Authorization/Permissions.cs` aynen kullanılır; gerekirse şu sabitler eklenir:

```csharp
// Mevcut Permissions.cs içine eklenecek (opsiyonel)
public const string PaymentCancel = "payment.cancel";
public const string AuditAdmin = "audit.admin";
public const string FinanzOnlineSubmit = "finanzonline.submit";
public const string SystemCritical = "system.critical";
```

`system.critical` zaten Settings/System kapsamında dokümanlarda geçiyor; kodda ayrı bir permission yerine `settings.manage` ile birleştirilebilir veya `SystemCritical` policy adıyla rol seti (SuperAdmin, Admin) tutulabilir.

### B.2 AddAuthorization policy registration kodu

**Strateji:** Hem **legacy role policy’ler** (mevcut controller’lar kırılmasın) hem **permission policy’ler** (Permission:xxx) kayıtlı olsun. Permission handler rol → permission matrisinden bakar; JWT’te `role` (ve isteğe bağlı `permissions`) claim’i kullanılır.

```csharp
// Program.cs – Permission policy'ler (zaten mevcut)
foreach (var permission in Permissions.All)
    options.AddPolicy(Permissions.PolicyPrefix + permission, policy =>
        policy.Requirements.Add(new PermissionRequirement(permission)));

// Legacy fallback: Permission policy başarısız olursa kullanıcı hâlâ role policy ile erişebilir.
// Bunun için controller'da tek policy kullanılır: önce Permission:xxx, legacy kullanıcılar için
// aynı endpoint'te RequireRole ile bir policy daha tanımlanmaz; bunun yerine PermissionAuthorizationHandler
// içinde Administrator rolünü Admin ile eşleyen matris kullanılır (RolePermissionMatrix'te Administrator = Admin yetkisi).
```

**Legacy role policy’ler (mevcut – silinmez):** AdminUsers, UsersView, UsersManage, BackofficeManagement, BackofficeSettings, PosSales, PosTableOrder, PosCatalogRead, CatalogManage, InventoryManage, InventoryDelete, AuditView, AuditViewWithCashier, AuditAdmin, PosTse, PosTseDiagnostics, SystemCritical, CashRegisterManage.

**Permission policy’ler:** `Permission:user.view`, `Permission:report.view`, … (Permissions.All üzerinden).

### B.3 HasPermission attribute örneği

Mevcut `RequirePermissionAttribute` kullanılır; isim olarak "HasPermission" da verilebilir:

```csharp
// backend/Authorization/RequirePermissionAttribute.cs (mevcut)
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(Permissions.PolicyPrefix + permission)
    {
        if (string.IsNullOrEmpty(permission))
            throw new ArgumentNullException(nameof(permission));
    }
}

// Kullanım (controller'da)
[RequirePermission(Permissions.ReportView)]
public async Task<IActionResult> GetSalesReport() => ...

// Veya policy adı ile
[Authorize(Policy = "Permission:report.view")]
public async Task<IActionResult> GetSalesReport() => ...
```

---

## C) Migration planı

### C.1 Login sırasında role → permission claim üretim akışı

**Mevcut:** AuthController Login’de `GetRolesAsync(user)` → `primaryRole` → `GenerateJwtToken(user, primaryRole)`; token’da `user_role` ve `ClaimTypes.Role` (veya `role`) claim’i var. RoleCanonicalization ile Administrator → Admin dönüşümü yapılıyor.

**Önerilen (minimum kırılım):**

1. **Seçenek A (önerilen – token’a permission ekleme):** Login cevabında **body** içinde `permissions: string[]` döndür; JWT’e ekleme. Frontend bu listeyi cache’ler; menü/buton guard’da kullanır. API yetkisi backend’de PermissionAuthorizationHandler ile rol → permission matristen hesaplansın (mevcut).
2. **Seçenek B (JWT’e permissions ekleme):** `GenerateJwtToken` içinde kullanıcı rollerini al (Identity’den); RolePermissionMatrix.GetPermissionsForRoles(roles) ile permission listesini üret; her permission için bir claim ekle (örn. `Claim("perm", "report.view")`). JWT büyür; rol değişince token yenilenene kadar eski kalır.

**Akış (Seçenek A – önerilen):**

```
Login → Validate user → GetRolesAsync(user)
  → RoleCanonicalization: Administrator → Admin (zaten var)
  → GenerateJwtToken(user, primaryRole)  // token'da "role" = canonical
  → permissions = RolePermissionMatrix.GetPermissionsForRoles(roles)
  → Response: { token, user: { ..., role, roles, permissions } }
```

Frontend `user.permissions` kullanır; backend API çağrılarında yine JWT + PermissionAuthorizationHandler (role claim’den matris ile permission kontrolü) kullanılır.

**Akış (Seçenek B – JWT’e permissions):**

```
Login → GetRolesAsync(user)
  → permissions = RolePermissionMatrix.GetPermissionsForRoles(roles)
  → Token claims: sub, email, role, ... + [ Claim("permissions", "report.view"), ... ]
  (veya tek claim "permissions" = JSON array string)
```

Handler’da önce JWT’teki `permissions` claim’ine bak; yoksa role’den matris ile türet (legacy fallback).

### C.2 İlk sprintte dönüştürülecek kritik endpoint listesi

Yüksek riskli ve öncelikli korunacak endpoint’ler permission policy ile korunmalı (aynı anda legacy role policy kaldırılmaz; controller’da `[Authorize(Policy = "Permission:xxx")]` veya `[RequirePermission(Permissions.X)]` eklenir; gerekirse composite policy ile hem permission hem legacy role kabul edilebilir).

| Öncelik | Endpoint | Permission | Not |
|---------|----------|------------|-----|
| 1 | POST /api/Payment/{id}/refund | refund.create | İade |
| 2 | POST /api/Payment/{id}/cancel | payment.cancel | Ödeme iptal |
| 3 | DELETE /api/AuditLog/cleanup | audit.admin | Audit cleanup |
| 4 | GET /api/AuditLog/export | audit.admin | Audit export |
| 5 | PUT /api/CompanySettings/* (tüm yazma) | settings.manage | Şirket ayarları |
| 6 | PUT /api/FinanzOnline/config, POST submit-invoice | settings.manage, finanzonline.submit | FinanzOnline config + submit |
| 7 | POST /api/Invoice/backfill-from-payments | system.critical | Backfill |
| 8 | EntityController kalıcı silme | system.critical | Hard delete |
| 9 | GET /api/Payment/{id}/signature-debug, POST verify-signature | system.critical | TSE tanılama |
| 10 | POST/PUT/DELETE /api/admin/categories | category.manage | Kategori yazma |
| 11 | POST/PUT/DELETE /api/admin/products, modifier-groups yazma | product.manage | Ürün/modifier yazma |

### C.3 Legacy role fallback stratejisi

- **Policy seviyesinde:** Kritik endpoint’lerde sadece `[Authorize(Policy = "Permission:refund.create")]` kullanılırsa, token’da sadece `role` olan kullanıcı için PermissionAuthorizationHandler rol → permission matrisinden bakar; Administrator matriste Admin ile aynı yetkiye sahip olduğu için çalışır. Ek bir “legacy” policy yazmaya gerek yok.
- **Matris:** RolePermissionMatrix’te `Administrator` rolü, Admin ile aynı permission setine sahip olsun (zaten öyle). Böylece eski Administrator token’ları permission policy’den geçer.
- **JWT claim:** Token’da `role` = canonical (Admin) veya `role` = Administrator geliyorsa, handler’da rol listesine hem gelen değer hem RoleCanonicalization.GetCanonicalRole(role) ile türetilen değer eklenebilir; veya matriste hem "Admin" hem "Administrator" aynı sete sahip olur (mevcut yapı).
- **Kademeli geçiş:** Önce kritik endpoint’lere `[RequirePermission(...)]` ekle; tüm controller’lar permission’a taşınana kadar mevcut role policy’leri Program.cs’te tut; controller’da sadece tek attribute olsun (Permission veya eski Policy, ikisi birden değil).

---

## D) Frontend guard / menu stratejisi

### D.1 hasPermission / route guard / menu config örneği

**Kaynak:** Login cevabında `user.permissions: string[]` (veya JWT’ten decode). Yoksa frontend’de rol → permission matrisi (backend ile senkron) kullanılır.

**hasPermission:**

```typescript
// constants/permissions.ts – backend Permissions.All ile senkron
export const PERMISSIONS = {
  USER_VIEW: 'user.view',
  USER_MANAGE: 'user.manage',
  REPORT_VIEW: 'report.view',
  REPORT_EXPORT: 'report.export',
  SETTINGS_MANAGE: 'settings.manage',
  SALE_CREATE: 'sale.create',
  SALE_VIEW: 'sale.view',
  PAYMENT_TAKE: 'payment.take',
  REFUND_CREATE: 'refund.create',
  ORDER_CREATE: 'order.create',
  ORDER_UPDATE: 'order.update',
  ORDER_VIEW: 'order.view',
  TABLE_VIEW: 'table.view',
  TABLE_MANAGE: 'table.manage',
  PRODUCT_VIEW: 'product.view',
  PRODUCT_MANAGE: 'product.manage',
  CATEGORY_VIEW: 'category.view',
  CATEGORY_MANAGE: 'category.manage',
  AUDIT_VIEW: 'audit.view',
  AUDIT_ADMIN: 'audit.admin',
  // ...
} as const;

// hooks/usePermissions.ts
export function usePermissions() {
  const { user } = useAuth(); // { role, roles, permissions? }
  const permissions = user?.permissions ?? getPermissionsFromRole(user?.role);

  const hasPermission = (perm: string) =>
    Array.isArray(permissions) && permissions.includes(perm);
  const hasAnyPermission = (perms: string[]) =>
    perms.some(p => hasPermission(p));

  return { permissions, hasPermission, hasAnyPermission };
}

// getPermissionsFromRole: backend RolePermissionMatrix ile senkron statik map
function getPermissionsFromRole(role: string | undefined): string[] {
  if (!role) return [];
  return ROLE_PERMISSIONS_MAP[role] ?? [];
}
```

**Route guard (FE-Admin / Next.js):**

```tsx
// components/RequirePermission.tsx
export function RequirePermission({ permission, children }: { permission: string; children: React.ReactNode }) {
  const { hasPermission } = usePermissions();
  if (!hasPermission(permission))
    return <Navigate to="/403" />;
  return <>{children}</>;
}

// layout veya sayfa
<RequirePermission permission={PERMISSIONS.REPORT_VIEW}>
  <ReportsPage />
</RequirePermission>
```

**Menu config (FE-Admin):**

```typescript
const MENU_ITEMS = [
  { key: 'users', path: '/users', label: 'Benutzer', permission: PERMISSIONS.USER_VIEW },
  { key: 'reports', path: '/reports', label: 'Berichte', permission: PERMISSIONS.REPORT_VIEW },
  { key: 'settings', path: '/settings', label: 'Einstellungen', permission: PERMISSIONS.SETTINGS_MANAGE },
  { key: 'categories', path: '/admin/categories', label: 'Kategorien', permission: PERMISSIONS.CATEGORY_VIEW },
  { key: 'products', path: '/admin/products', label: 'Produkte', permission: PERMISSIONS.PRODUCT_VIEW },
  // ...
];

// Render menü
MENU_ITEMS.filter(item => hasPermission(item.permission)).map(...)
```

**POS (Expo):** Aynı mantık; `usePermissions()` ve `SCREEN_ACCESS` permission’a göre (örn. `sale.create`, `payment.take`, `table.manage`) ekran listesi filtrelenir.

---

## E) Örnek kod

### E.1 Login response’a permissions ekleme (backend)

```csharp
// AuthController.cs – Login action içinde
var roles = await _userManager.GetRolesAsync(user);
var primaryRole = roles.FirstOrDefault() ?? user.Role ?? "User";
var token = GenerateJwtToken(user, primaryRole);

// Role → permissions (legacy uyumlu; Administrator matriste Admin ile aynı)
var permissions = RolePermissionMatrix.GetPermissionsForRoles(roles).ToList();

var response = new
{
    token = token,
    expiresIn = 3600,
    user = new
    {
        id = user.Id,
        email = user.Email,
        firstName = user.FirstName,
        lastName = user.LastName,
        role = RoleCanonicalization.GetCanonicalRole(primaryRole),
        roles = roles,
        permissions = permissions  // frontend menü/guard için
    }
};
return Ok(response);
```

### E.2 Kritik endpoint’e RequirePermission ekleme

```csharp
// PaymentController.cs – refund
[HttpPost("{id}/refund")]
[RequirePermission(Permissions.RefundCreate)]
public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest request)
{
    // ...
}

// AuditLogController.cs – cleanup
[HttpDelete("cleanup")]
[RequirePermission("audit.admin")]  // veya Permissions.AuditAdmin sabiti eklenirse
public async Task<IActionResult> Cleanup([FromQuery] DateTime? cutoffDate)
{
    // ...
}
```

### E.3 Legacy fallback: Handler’da Administrator

RolePermissionMatrix’te zaten `[Roles.Administrator] = all` (veya Admin ile aynı set) olduğu için, token’da `role: "Administrator"` gelen kullanıcı da Permission policy’den geçer. Ek kod gerekmez; matrisin Administrator’ı kapsaması yeterli.

### E.4 Frontend: GET /me ile permissions güncelleme

Login dışında sayfa yenilemede veya session restore’da permissions’ı almak için:

```typescript
// GET /api/me veya GET /api/user/me
interface MeResponse {
  id: string;
  email: string;
  role: string;
  roles: string[];
  permissions: string[];
}
// useAuth veya usePermissions bu endpoint’i çağırıp permissions’ı set eder.
```

Backend’de `GET /api/me` (veya AuthController’da) varsa response’a `permissions` eklenir (RolePermissionMatrix.GetPermissionsForRoles(roles)).

---

## Özet

| Çıktı | Konum |
|-------|--------|
| A) Endpoint bazlı permission matrisi | Bölüm A.1–A.4 |
| B) Backend policy tasarımı (Permissions sınıfı, AddAuthorization, HasPermission/RequirePermission) | B.1–B.3 |
| C) Migration planı (login role→permission, kritik endpoint listesi, legacy fallback) | C.1–C.3 |
| D) Frontend hasPermission / route guard / menu config | D.1 |
| E) Örnek kod (login permissions, RequirePermission, legacy fallback, GET /me) | E.1–E.4 |

Bu tasarım, swagger’daki `/api/admin/*`, `/api/pos/*`, legacy `/api/Cart`, `/api/Payment` ve yüksek riskli endpoint’lere göre uyarlanmıştır; mevcut user.role alanı ve Administrator legacy alias ile uyumludur.
