# Role Management Backend – Implementation Summary

**Tarih:** 2025-03-10  
**Kapsam:** SuperAdmin custom rol silme, rol bazlı permission set yönetimi, menu visibility ile uyum.

---

## 1) Etkilenen / Eklenen Dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Services/IRolePermissionResolver.cs` | **Yeni** – Rol başına etkin permission çözümleme arayüzü |
| `backend/Services/RolePermissionResolver.cs` | **Yeni** – Sistem rolü => matrix, custom rol => AspNetRoleClaims |
| `backend/Services/IRoleManagementService.cs` | **Yeni** – Catalog, list with permissions, set permissions, delete |
| `backend/Services/RoleManagementService.cs` | **Yeni** – Use-case implementasyonu |
| `backend/Authorization/PermissionCatalogMetadata.cs` | **Yeni** – key, group, resource, action, description metadata |
| `backend/Services/TokenClaimsService.cs` | **Değişti** – IRolePermissionResolver kullanımı, async BuildClaimsAsync |
| `backend/Controllers/UserManagementController.cs` | **Değişti** – 4 yeni endpoint, IRoleManagementService, SuperAdmin kontrolü, DTO’lar |
| `backend/Models/AuditLog.cs` | **Değişti** – ROLE_DELETE, ROLE_PERMISSIONS_UPDATE; AuditLogEntityTypes.ROLE |
| `backend/Program.cs` | **Değişti** – IRolePermissionResolver, IRoleManagementService kayıt |
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | **Değişti** – CreateController’a IRoleManagementService parametresi |
| `backend/KasseAPI_Final.Tests/RoleManagementTests.cs` | **Yeni** – Yeni endpoint ve policy testleri |

**Değiştirilmedi:** `RolePermissionMatrix.cs`, `AppPermissions.cs`, `PermissionCatalog.cs` (mevcut davranış aynı).

---

## 2) Alınan Mimari Kararlar

- **Deterministic claim modeli**
  - **Sistem rolleri:** İzin seti yalnızca `RolePermissionMatrix` (kod); değiştirilmez.
  - **Custom roller:** İzin seti `AspNetRoleClaims` (ClaimType = `permission`, Value = permission key); sadece SuperAdmin PUT ile güncelleyebilir.
  - JWT permission claim’leri: `TokenClaimsService` → `IRolePermissionResolver.GetPermissionsForRolesAsync(roles)` ile birleşik set (matrix + role claims).

- **Permission update sadece custom roller:** Sistem rolleri (Roles.Canonical) için PUT permissions 400 + `SYSTEM_ROLE_NOT_EDITABLE`.

- **Rol silme:** Sadece custom roller; sistem rolü 400 + `SYSTEM_ROLE_NOT_DELETABLE`; en az bir kullanıcı atanmışsa 409 + `ROLE_HAS_ASSIGNED_USERS`.

- **SuperAdmin-only aksiyonlar:** PUT `roles/{roleName}/permissions` ve DELETE `roles/{roleName}` controller içinde `IsCurrentUserSuperAdmin()` ile kontrol; değilse 403 (mevcut 403 payload formatı).

- **Audit:** Rol silme ve permission güncelleme için `LogSystemOperationAsync` (EntityType = Role); extension point değişmedi, mevcut servis kullanıldı.

- **İş mantığı controller’da biriktirilmedi:** Tüm kurallar `RoleManagementService` ve `RolePermissionResolver` içinde.

---

## 3) Örnek Request / Response

### GET /api/UserManagement/roles/permissions-catalog  
**Response (200):**
```json
[
  { "key": "user.view", "group": "User & Role", "resource": "user", "action": "view", "description": null },
  { "key": "product.view", "group": "Product", "resource": "product", "action": "view", "description": null }
]
```

### GET /api/UserManagement/roles/with-permissions  
**Response (200):**
```json
[
  { "roleName": "Admin", "permissions": ["user.view", "user.manage", "product.view", ...], "isSystemRole": true, "userCount": 2 },
  { "roleName": "CustomRole", "permissions": ["product.view", "order.view"], "isSystemRole": false, "userCount": 0 }
]
```

### PUT /api/UserManagement/roles/CustomRole/permissions  
**Request:**
```json
{ "permissions": ["product.view", "order.view", "sale.view"] }
```
**Response (200):** `{ "message": "Role permissions updated successfully" }`  
**400 (invalid key):** `{ "message": "One or more permission keys are invalid...", "code": "VALIDATION_ERROR", "errors": { "Permissions": ["Invalid permission key(s)."] } }`  
**400 (system role):** `{ "message": "System roles cannot be edited...", "code": "SYSTEM_ROLE_NOT_EDITABLE" }`  
**404:** `{ "message": "Role not found", "code": "ROLE_NOT_FOUND" }`  
**403 (not SuperAdmin):** `{ "code": "AUTH_FORBIDDEN", "reason": "MISSING_ROLE_OR_SCOPE", "requiredPolicy": "SuperAdmin", ... }`

### DELETE /api/UserManagement/roles/CustomRole  
**Response (200):** `{ "message": "Role deleted successfully" }`  
**400 (system role):** `{ "message": "System roles cannot be deleted.", "code": "SYSTEM_ROLE_NOT_DELETABLE" }`  
**409 (users assigned):** `{ "message": "Cannot delete role: one or more users are assigned to this role.", "code": "ROLE_HAS_ASSIGNED_USERS" }`  
**404:** `{ "message": "Role not found", "code": "ROLE_NOT_FOUND" }`  
**403 (not SuperAdmin):** Aynı 403 payload.

---

## 4) Test Sonuçları

- **Tüm proje:** `dotnet test -f net10.0` → **231 test geçti** (önceki 224 + 7 yeni).
- **Yeni testler** (`RoleManagementTests`): Catalog metadata, geçerli key kontrolü, SetRolePermissions/DeleteRole için 403 (Admin), 200 (SuperAdmin + success), 409 (RoleHasAssignedUsers).
- **UserManagementControllerUserLifecycleTests:** CreateController’a `IRoleManagementService` mock eklendi; 34 test geçiyor.

---

## 5) Bilinen Sınırlamalar

- **Sistem rolleri:** `Roles.Canonical` (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant) sabit; yeni sistem rolü eklemek kod değişikliği gerektirir.
- **Permission catalog description:** Şu an tüm öğelerde `description: null`; ileride key’e göre metin eklenebilir.
- **Rol adı case:** Identity rol adları case-sensitive olabilir; API’de `roleName` route’tan olduğu gibi kullanılıyor. FindByNameAsync genelde case-insensitive.
- **OpenAPI/Swagger:** Yeni endpoint’ler projede Swagger kullanılıyorsa otomatik görünür; ayrıca OpenAPI spec’i yeniden generate edilmedi (frontend-admin `npm run generate:api` ile güncellenmeli).
- **Menu visibility:** Backend tarafında değişiklik yok; FE permission-first mimarisi mevcut (user.view / role.view vb.) ile uyumlu; custom rol permission’ları JWT’e yansıdığı için menü görünürlüğü aynı mantıkla çalışır.
