# ASP.NET Core Permission-Based Authorization Layer

**Tarih:** 2025-03-09

Bu belge, permission tabanlı policy yapısı, `AddAppAuthorization()` kaydı, `HasPermissionAttribute` kullanımı ve legacy uyumluluk stratejisini açıklar.

---

## 1) Program.cs / Startup registration

Tek satırla tüm authorization katmanı (legacy role policy’ler + permission policy’ler + handler’lar) kaydedilir:

```csharp
using KasseAPI_Final.Authorization;

// Authorization: legacy role policies + permission-based policies.
// Legacy: [Authorize(Policy = "PosSales")] etc. remain. New endpoints: [HasPermission(AppPermissions.X)].
builder.Services.AddAppAuthorization();
```

`AddAppAuthorization()` şunları yapar:

- `AddAuthorization(options => { AddLegacyRolePolicies(options); options.AddPermissionPolicies(); })`  
  - Legacy: AdminUsers, UsersView, BackofficeSettings, PosSales, PosTableOrder, CatalogManage, AuditView, PosTse, SystemCritical, vb. (Roles.* sabitleriyle).
  - Permission: `PermissionCatalog.All` içindeki her permission için bir policy (`"Permission:{permission}"`).
- `AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>()`
- `AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenResponseAuthorizationHandler>()`

---

## 2) Custom requirement / handler

- **PermissionRequirement:** `IAuthorizationRequirement`; tek property: `Permission` (string). Policy adı `PermissionCatalog.PolicyPrefix + permission`.
- **PermissionAuthorizationHandler:** `AuthorizationHandler<PermissionRequirement>`. Önce JWT’deki `permission` claim’lerine bakar; yoksa `role` claim’lerinden `RolePermissionMatrix.GetPermissionsForRoles` ile permission set çıkarır; requirement.Permission bu sette varsa başarılı.

Magic string kullanmamak için controller’da her zaman `AppPermissions.*` sabitleri kullanın.

---

## 3) HasPermissionAttribute

`[HasPermission(string permission)]` → aslında `[Authorize(Policy = "Permission:" + permission)]` kullanır. Permission sabiti zorunludur.

```csharp
[HasPermission(AppPermissions.PaymentTake)]
public async Task<IActionResult> CreatePayment(...) { ... }

[HasPermission(AppPermissions.OrderUpdate)]
public async Task<IActionResult> UpdateOrderStatus(...) { ... }

[HasPermission(AppPermissions.AuditCleanup)]
public async Task<IActionResult> CleanupOldAuditLogs(...) { ... }

[HasPermission(AppPermissions.SettingsManage)]
public async Task<IActionResult> Put(...) { ... }
```

Sınıf seviyesinde genel policy (ör. `[Authorize(Policy = "PosSales")]`) kalabilir; action seviyesinde ek olarak `[HasPermission(AppPermissions.X)]` ile izin daraltılır.

---

## 4) Controller kullanım örnekleri

### payment.take

```csharp
// backend/Controllers/PaymentController.cs
using KasseAPI_Final.Authorization;

[Authorize(Policy = "PosSales")]  // legacy; kaldırılmayacak
[ApiController]
[Route("api/[controller]")]
public class PaymentController : BaseController
{
    [HttpPost]
    [HasPermission(AppPermissions.PaymentTake)]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        // ...
    }
}
```

### order.update

```csharp
// backend/Controllers/OrdersController.cs
[Authorize(Policy = "PosTableOrder")]
public class OrdersController : BaseController
{
    [HttpPut("{id}/status")]
    [HasPermission(AppPermissions.OrderUpdate)]
    public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusRequest request)
    {
        // ...
    }
}
```

### audit.cleanup

```csharp
// backend/Controllers/AuditLogController.cs
[Authorize(Policy = "UsersView")]
public class AuditLogController : BaseController
{
    [HttpDelete("cleanup")]
    [HasPermission(AppPermissions.AuditCleanup)]
    public async Task<IActionResult> CleanupOldAuditLogs([FromQuery] CleanupAuditRequest request)
    {
        // ...
    }
}
```

### settings.manage

```csharp
// backend/Controllers/CompanySettingsController.cs
[Authorize]
public class CompanySettingsController : BaseController
{
    [HttpPut]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Put([FromBody] CompanySettingsDto dto)
    {
        // ...
    }
}
```

Policy adıyla kullanmak isterseniz (attribute yerine):

```csharp
[Authorize(Policy = "Permission:settings.manage")]
public async Task<IActionResult> Put(...) { ... }
```

Sabit kullanımı için: `Authorize(Policy = PermissionCatalog.PolicyPrefix + AppPermissions.SettingsManage)`.

---

## 5) Legacy uyumluluk stratejisi

| Madde | Açıklama |
|-------|----------|
| Eski `[Authorize(Roles = "...")]` | Projede zaten kullanılmıyor; controller’lar `[Authorize(Policy = "PolicyName")]` kullanıyor. Bu policy’ler **hemen kaldırılmayacak**; `AddAppAuthorization()` içinde `AddLegacyRolePolicies()` ile kayıtlı kalacak. |
| Yeni endpoint’ler | Permission policy kullanacak: `[HasPermission(AppPermissions.X)]` veya `[Authorize(Policy = "Permission:...")]`. |
| Administrator → Admin | Identity’de "Administrator" rolü kalabilir; **permission seti Admin ile aynı**. `RolePermissionMatrix` ve `RoleCanonicalization` ile Administrator, Admin ile aynı yetkileri alır. Yeni atamalarda rol adı olarak **Admin** tercih edilir. |
| Geçiş sırası | Önce kritik endpoint’lere (payment, refund, order status/cancel, company settings, audit cleanup, finanzonline, inventory adjust) `[HasPermission(...)]` eklenir; class seviyesi legacy policy aynı kalabilir. İstenirse ileride sadece permission policy’ye geçilir. |

---

## 6) Dosya referansları

| Dosya | Açıklama |
|-------|----------|
| `backend/Authorization/AuthorizationExtensions.cs` | `AddAppAuthorization()`, `AddPermissionPolicies()`, `AddLegacyRolePolicies()`. |
| `backend/Authorization/HasPermissionAttribute.cs` | `[HasPermission(permission)]`. |
| `backend/Authorization/PermissionRequirement.cs` | `IAuthorizationRequirement` için permission string. |
| `backend/Authorization/PermissionAuthorizationHandler.cs` | Permission claim veya RolePermissionMatrix ile değerlendirme. |
| `backend/Authorization/AppPermissions.cs` | Tüm permission sabitleri (magic string yok). |
| `backend/Authorization/PermissionCatalog.cs` | `All`, `PolicyPrefix`, `PermissionClaimType`. |
| `backend/Authorization/RolePermissionMatrix.cs` | Rol → permission set (Administrator = Admin set). |
