# FE-Admin Users Sayfası – Role Management Analizi (Read-Only)

**Tarih:** 2025-03-10  
**Kapsam:** Role create dışındaki mevcut yetenekler, eksik full-stack sözleşme, policy/gateway/generated client/backend/matrix/menu ilişkisi.  
**Kural:** Hiçbir dosya değiştirilmedi; sadece analiz.

---

## 1) Current Behavior

### 1.1 Sayfa ve yetenekler (role create hariç)

**Dosya:** `frontend-admin/src/app/(protected)/users/page.tsx`

| Capability | Policy alanı | Gateway fonksiyonu | Backend endpoint | Backend permission |
|------------|--------------|--------------------|------------------|--------------------|
| Liste görüntüleme | `policy.canView` | `getUsersList` (usersApi) | `GET /api/UserManagement?query,role,isActive,page,pageSize` | `UserView` |
| Tekil kullanıcı (edit/detail) | — | `getUserById` (generated) | `GET /api/UserManagement/{id}` | `UserView` |
| Kullanıcı oluşturma | `policy.canCreate` | `gatewayCreateUser` | `POST /api/UserManagement` | `UserManage` |
| Kullanıcı güncelleme | `policy.canEdit` | `gatewayUpdateUser` | `PUT /api/UserManagement/{id}` | `UserManage` |
| Deaktivasyon | `policy.canDeactivate` | `gatewayDeactivateUser` (usersApi) | `PUT /api/UserManagement/{id}/deactivate` | `UserManage` |
| Reaktivasyon | `policy.canReactivate` | `gatewayReactivateUser` (usersApi) | `PUT /api/UserManagement/{id}/reactivate` | `UserManage` |
| Şifre sıfırlama | `policy.canResetPassword(record.role)` | `gatewayResetPassword` | `PUT /api/UserManagement/{id}/reset-password` | `UserManage` (+ SuperAdmin-only hedef kuralı) |
| Rol listesi (form/filtre) | — | `getRoles` (generated) | `GET /api/UserManagement/roles` | `UserView` |
| **Rol oluşturma** | `policy.canCreateRole` | `gatewayCreateRole` | `POST /api/UserManagement/roles` | **`UserManage`** (RoleManage değil) |

Referanslar:
- Policy: `useUsersPolicy()` → `usersPolicy.ts` → `getUsersPolicy(actorRole, permissions)`.
- Gateway: `usersGateway.ts` (generated + `usersApi.ts` wrapper’lar).
- Backend: `UserManagementController.cs` (HasPermission attribute’ları).

### 1.2 FE policy (usersPolicy.ts)

- **Permission-first:** `user.permissions` doluysa:
  - `canView` ← `PERMISSIONS.USER_VIEW`
  - `canCreate` / `canEdit` / `canDeactivate` / `canReactivate` ← `PERMISSIONS.USER_MANAGE`
  - `canCreateRole` ← `PERMISSIONS.ROLE_MANAGE`
  - `canResetPassword` ← role-based `roleCanResetPassword(role, targetRole)` (SuperAdmin hedef için sadece SuperAdmin).
- **Role fallback:** permissions yoksa `roles.ts`:
  - `canViewUsers` → SuperAdmin, Admin, Manager.
  - `canManageUsers` → SuperAdmin, Admin (create/edit/deactivate/reactivate).
  - `canCreateRole` → sadece SuperAdmin (`ROLES_CAN_CREATE_ROLE`).
  - `canResetPassword` → canManageUsers + SuperAdmin-target kuralı.

### 1.3 Backend role endpoint izinleri

- `GET /api/UserManagement/roles` → `[HasPermission(AppPermissions.UserView)]` (satır 502).
- `POST /api/UserManagement/roles` → `[HasPermission(AppPermissions.UserManage)]` (satır 519).

Yani backend’de rol oluşturma **RoleManage** ile korunmuyor; **UserManage** kullanılıyor. `AppPermissions.RoleManage` ve `RolePermissionMatrix` içinde tanımlı ama controller’da hiçbir endpoint `RoleManage` / `RoleView` kullanmıyor.

### 1.4 Menü görünürlüğü

- **Dosya:** `frontend-admin/src/shared/auth/menuPermissions.ts`  
  - `/users` → `PERMISSIONS.USER_VIEW` (tek permission).
- **Dosya:** `frontend-admin/src/app/(protected)/layout.tsx`  
  - Permission modunda: `isMenuItemAllowed(item.key, permissions)` ile filtre; `/users` için `user.view` gerekir.
  - Role fallback: `/users` için `canViewUsers(user?.role)` (SuperAdmin, Admin, Manager).

Sonuç: “Users” menü öğesi sadece **user.view** (veya role fallback) ile görünür. Rol oluşturma butonu sayfa içinde `canCreateRole` (ROLE_MANAGE veya SuperAdmin) ile ayrı kontrol edilir; menüde ayrı bir “role.manage” anahtarı yok.

### 1.5 Rol filtresi (liste)

- Sayfada `roleOptions = useMemo(() => roles?.map(...) ?? ROLE_OPTIONS, [roles])` var (`useRoles` ile backend’den roller geliyor).
- Filtre Select’inde `options={ROLE_OPTIONS}` kullanılıyor (satır 391–398); yani **sabit ROLE_OPTIONS**, `roleOptions` değil. Backend’den gelen roller filtre dropdown’ında kullanılmıyor.

---

## 2) Missing Capabilities

Aşağıdaki eksikler **backend controller, generated client ve FE sayfa** taramasıyla tespit edildi.

| Eksik | Backend | Generated client | FE (page/gateway) |
|-------|---------|------------------|--------------------|
| Rol güncelleme (örn. isim) | Yok: `PUT /roles/{id}` yok | Yok | Yok |
| Rol silme | Yok: `DELETE /roles/{id}` yok | Yok | Yok |
| Rol detay (tekil) | Yok: `GET /roles/{id}` yok | Yok | Yok |
| Rol–izin matrisi API | Matris sadece kodda (`RolePermissionMatrix.cs`); okuma/güncelleme endpoint’i yok | — | — |
| CreateRole için RoleManage | Controller `UserManage` kullanıyor | — | Policy `ROLE_MANAGE` kullanıyor |
| Liste sözleşmesi (sayfalama) | Backend `UsersListResponse` dönüyor | `getApiUserManagement` → `UserInfo[]` (sayfalama yok) | Liste `usersApi.getUsersList` ile (custom), generated kullanılmıyor |
| Filtre dropdown rollerini sunucudan kullanma | GET /roles var | Var | Sayfa filtrede `ROLE_OPTIONS` kullanıyor, `roleOptions` kullanmıyor |

Özet:
- **Rol CRUD:** Sadece list (GET /roles) ve create (POST /roles) var; update/delete ve GET-by-id yok.
- **İzin matrisi:** Sadece statik kod; API ile rol–permission eşlemesi okunamıyor/güncellenemiyor.
- **Backend/FE izin uyumu:** CreateRole backend’de `UserManage`, FE’de `ROLE_MANAGE`; backend’de `RoleManage` hiç kullanılmıyor.
- **Liste:** Backend sayfalı response döndüğü için generated client ile liste sözleşmesi uyumsuz; FE custom `getUsersList` kullanıyor.
- **Filtre:** Rol listesi API’den geliyor ama filtre Select’inde sabit liste kullanılıyor.

---

## 3) Proposed API Contract (Referans – Uygulama Yapılmadı)

Sadece eksikleri kapatmak için önerilen sözleşme taslağı:

**Rol CRUD (eksikler):**
- `GET /api/UserManagement/roles/{id}` → Rol detayı (örn. name, isSystem).
- `PUT /api/UserManagement/roles/{id}` → Rol güncelleme (örn. name); body `{ "name": "..." }`.
- `DELETE /api/UserManagement/roles/{id}` → Rol silme (custom roller için; sistem rolleri korumalı olabilir).

**İzin matrisi (opsiyonel):**
- `GET /api/UserManagement/roles/{id}/permissions` → Rolün permission listesi.
- `PUT /api/UserManagement/roles/{id}/permissions` → Rolün permission listesini güncelleme (custom roller için).

**İzin tutarlılığı:**
- `GET /roles` → `RoleView` veya mevcut `UserView` (mevcut davranış korunabilir).
- `POST /roles` → `RoleManage` (backend’de CreateRole’ün `UserManage` yerine `RoleManage` ile korunması).
- `PUT /roles/{id}`, `DELETE /roles/{id}` → `RoleManage`.

**Liste:**
- OpenAPI/backend: `GET /api/UserManagement` response tipi `UsersListResponse` (items + pagination) olarak tanımlanıp generated client’ın buna göre üretilmesi; böylece liste tek sözleşmede toplanır.

---

## 4) Frontend Change Plan (Öneri – Uygulama Yapılmadı)

1. **Policy:** Mevcut; sadece backend `RoleManage`’e geçilirse FE zaten `ROLE_MANAGE` kullanıyor.
2. **Menü:** `/users` için sadece `USER_VIEW` yeterli kalır; rol işlemleri sayfa içi butonlarla (canCreateRole / ileride canEditRole, canDeleteRole) kalabilir.
3. **Rol filtresi:** Filtre Select’inde `options={ROLE_OPTIONS}` yerine `options={roleOptions}` (useRoles’tan gelen) kullanılması; böylece backend’deki roller (sistem + custom) filtrede görünür.
4. **Gateway:** Yeni endpoint’ler (GET/PUT/DELETE role by id, role permissions) eklendikten sonra:
   - Önce OpenAPI + `npm run generate:api` ile generated client.
   - Gerekirse `usersGateway.ts` içinde ince wrapper’lar (query key, hata normalize).
5. **Sayfa:** Rol düzenleme/silme UI’ı ancak backend sözleşmesi ve izinleri (RoleManage) netleştikten sonra eklenmeli.

---

## 5) Backend Change Plan (Öneri – Uygulama Yapılmadı)

1. **CreateRole izni:** `POST /api/UserManagement/roles` için `[HasPermission(AppPermissions.UserManage)]` → `[HasPermission(AppPermissions.RoleManage)]` değişikliği (Admin/SuperAdmin’e RoleManage verildiği için davranış değişmez; sadece semantik ve FE ile uyum sağlanır).
2. **GET /roles/{id}:** IdentityRole’den name (ve isteğe bağlı isSystem) dönen endpoint; `RoleView` veya `UserView`.
3. **PUT /roles/{id}:** Name güncelleme; sadece “custom” (Identity’de olup RolePermissionMatrix’te sabit tanımı olmayan) roller için; `RoleManage`.
4. **DELETE /roles/{id}:** Sadece kullanıcıya atanmamış ve custom roller için; `RoleManage`; sistem rolleri (Roles.Canonical) 403 veya 409.
5. **Rol–permission API (ileride):** Matris runtime’da genişletilecekse (custom role’e permission atama) ayrı endpoint’ler ve muhtemelen yeni tablo/service; şu an sadece analiz, implementasyon yok.

---

## 6) Risks / Backward Compatibility

- **CreateRole’ü RoleManage’e taşımak:** JWT’de `role.manage` veren roller (RolePermissionMatrix’e göre Admin, SuperAdmin) aynı kalır; mevcut kullanıcılar etkilenmez. Sadece `user.manage` var `role.manage` yok bir senaryo varsa (özel konfig) o kullanıcı rol oluşturamaz; böyle bir konfig şu an dokümante değil.
- **Rol silme/güncelleme:** Sistem rolleri (Roles.Canonical) silinmez/güncellenmez kuralı konulmalı; aksi halde seed ve RolePermissionMatrix ile uyumsuzluk riski.
- **Liste sözleşmesi:** Generated’ı `UsersListResponse` yapmak, mevcut `getApiUserManagement` kullanan bir yer varsa breaking olur; FE şu an sadece `usersApi.getUsersList` kullandığı için sayfa tarafında risk düşük, ama client’ı değiştirirken tüm referanslar kontrol edilmeli.

---

## 7) Open Questions / Assumptions

- **Sistem rol / custom rol ayrımı:**  
  - **Mevcut durum:** `RolePermissionMatrix` ve `Roles.Canonical` sabit “sistem” rollerini tanımlıyor. POST /roles ile eklenen roller Identity’de var ama matrix’te karşılığı yok; bu yüzden `RoleHasPermission(customRole, x)` false döner, yani custom roller fiilen yetkisiz.  
  - **Ayrım gerekli mi?** Evet. Silme/güncelleme ve ileride permission atama için “sistem rolü” (silinemez, matris sabit) vs “custom rol” (silinebilir/güncellenebilir, ileride permission atanabilir) ayrımı yapılmalı. Backend’de örn. `Roles.Canonical` veya RolePermissionMatrix key set’i ile “sistem” tanımı kullanılabilir.
- **Rol listesi response:** GET /roles şu an `string[]` (isim listesi). GET /roles/{id} eklerse, liste endpoint’inin genişletilip `{ id, name, isSystem }[]` dönüp dönmeyeceği product kararı.
- **Menu’de ayrı “Rollen” sayfası:** Şu an rol yönetimi Users sayfası içinde. Ayrı bir “Rollen” menü öğesi ve sayfası açılacaksa, menü izni için `role.view` (veya `user.view`) ve sayfa içi aksiyonlar için `role.manage` kullanılabilir; `menuPermissions.ts`’e yeni key eklenir.

---

## Ek A: Role–Permission ile Menu Visibility İlişkisi

- **Menü görünürlüğü** tamamen **permission** ile yönetiliyor (permission yoksa role fallback).
  - `menuPermissions.ts`: Her menü key’i bir veya daha fazla permission ile eşleniyor (örn. `/users` → `USER_VIEW`).
  - `layout.tsx`: `isMenuItemAllowed(key, permissions)` ile menü öğesi filtreleniyor. Kullanıcıda ilgili permission yoksa (veya role fallback’te yetkisi yoksa) o menü öğesi gösterilmiyor.
- **Rol**, JWT’de claim olarak geliyor; backend `RolePermissionMatrix` ile role göre permission set’i üretip JWT’e permission claim’leri koyuyor. Yani menüde asıl kullanılan “permission”dır; rol dolaylı olarak o permission’ları belirler.
- **Users sayfası:**
  - Menüde “Users” linki → sadece `user.view` (veya role fallback: canViewUsers). Bu sayede sayfaya giriş izni verilir.
  - Sayfa içinde “Rolle anlegen” butonu → `canCreateRole` (yani `role.manage` veya SuperAdmin). Yani menü görünürlüğü ile sayfa içi aksiyon görünürlüğü ayrı: menü = user.view, rol oluşturma = role.manage / SuperAdmin.
- **Özet:** Menu visibility = “bu menü key’i için gerekli permission (veya role fallback)”. Role–permission ilişkisi backend’de (RolePermissionMatrix + JWT permissions); FE sadece “permissions” veya “role” ile menüyü filtreler. Rol adı doğrudan menü öğesine bağlanmıyor; permission (ve role fallback) bağlanıyor.

---

## Ek B: System Role / Custom Role Ayrımı

- **Sistem rolleri:** `RolePermissionMatrix` içinde açıkça tanımlı roller (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). `Roles.Canonical` ile uyumlu. Seed ile oluşturuluyor, davranış koda sabit.
- **Custom roller:** POST /roles ile eklenen; Identity’de rol var ama `RolePermissionMatrix`’te key yok. Şu an bu roller için `RoleHasPermission` false döndüğü için pratikte yetkisiz.
- **Ayrımın gerekmesi:**
  - Silme/güncelleme: Sistem rolleri silinmemeli veya isim değiştirilmemeli (seed ve matrix tutarlılığı).
  - İleride custom role’e permission atama: API ve veri modeli “custom” kavramına göre tasarlanmalı.
- **Öneri:** Backend’de “sistem rolü” = `Roles.Canonical` veya RolePermissionMatrix key set’i. GET /roles veya GET /roles/{id} response’unda `isSystem: boolean` ile ayrım yapılabilir; PUT/DELETE kuralları buna göre konulur.
