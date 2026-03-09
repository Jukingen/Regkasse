# Frontend–Backend Authorization Alignment Report

## 1. FE–BE uyum için yapılan değişiklikler

### frontend-admin

| Dosya | Değişiklik |
|-------|------------|
| **shared/auth/permissions.ts** | `ROLE_VIEW`, `ROLE_MANAGE` eklendi (backend `role.view` / `role.manage` ile hizalı). |
| **features/auth/constants/roles.ts** | Canonical rol seti eklendi: `ROLES_CANONICAL` ve `CanonicalRole` tipi (SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant). Legacy rol adı yok. |
| **shared/auth/usersPolicy.ts** | `canCreateRole`: Permission varsa `hasPermission(ROLE_MANAGE)`, yoksa `roleCanCreateRole(role)` (SuperAdmin fallback). |
| **app/(protected)/layout.tsx** | Menü filtreleme: Permission varsa tüm öğeler için `isMenuItemAllowed(key, permissions)`; yoksa /users ve /rksv için role fallback. |

### frontend (POS)

| Dosya | Değişiklik |
|-------|------------|
| **contexts/AuthContext.tsx** | `User` arayüzüne `permissions?: string[]` eklendi; backend login/me cevabındaki permissions saklanıyor. |
| **services/api/authService.ts** | `User` ve `LoginResponse.user` içine `permissions?: string[]` eklendi. |
| **shared/utils/PermissionHelper.ts** | `SCREEN_REQUIRED_PERMISSION` (ekran → gerekli permission) eklendi. `hasScreenAccessByPermission(screenName, userPermissions)` eklendi; permission-first erişim için kullanılıyor. |
| **components/RoleBasedNavigation.tsx** | `AuthContext` ile `user` alınıyor. `user?.permissions` varsa menü ve erişim permission ile (`hasScreenAccessByPermission`); yoksa rol ile (mevcut `SCREEN_ACCESS` / `item.roles`) hesaplanıyor. |

---

## 2. Kalan role-based alanlar (bilinçli)

| Konum | Neden role kullanılıyor |
|-------|--------------------------|
| **usersPolicy.canResetPassword** | Hedef kullanıcı SuperAdmin ise sadece SuperAdmin sıfırlayabilir; backend ile aynı iş kuralı, rol bazlı kaldı. |
| **usersPolicy canCreateRole (fallback)** | Token’da permission yoksa `roleCanCreateRole(role)` (sadece SuperAdmin) kullanılıyor. |
| **AdminOnlyGate** | Permission yoksa `role === 'Admin' \|\| role === 'SuperAdmin'` fallback. |
| **Layout menu (fallback)** | Permission yoksa `/users` → `canViewUsers(role)`, `/rksv` → `canShowRksvMenu(role)`. |
| **POS RoleBasedNavigation (fallback)** | `user.permissions` yoksa menü ve ekran erişimi `PermissionHelper.setUserRole` + `SCREEN_ACCESS` / `item.roles` ile. |
| **usePermission (POS)** | `isAdmin`, `isCashier`, `isManager`, `isSuperAdmin` rol bazlı; `canManageSystemSettings`, `canViewReports`, `canManageUsers` permission-first + rol fallback. |

Bu alanlarda rol, permission bilgisi yokken veya özel iş kuralında (reset SuperAdmin) kullanılıyor; yetki kaynağı olarak öncelik permission’da.

---

## 3. Test edilmesi gereken ekranlar

### frontend-admin

- **/users** – Permission: `user.view` (liste), `user.manage` (oluştur/düzenle/deaktif/aktif/sıfırla), `role.manage` (rol oluştur). Rol fallback: Manager görür, Admin/SuperAdmin yönetir.
- **/dashboard, /settings, /rksv** – Menu ve route guard: `settings.view`, `settings.manage`, `finanzonline.manage` vb.
- **/products, /categories, /modifier-groups** – `product.view`, `category.view`, `modifier.view`.
- **/audit-logs, /invoices, /payments, /receipts** – İlgili view permission’lar.
- **403** – Yetkisiz erişimde PermissionRouteGuard veya AdminOnlyGate’in yönlendirdiği sayfa.

### frontend (POS)

- **Rol ile giriş (permissions API’de yok)** – Menü ve ekran erişimi rol ile (Cashier, Waiter, Manager, Admin) çalışmalı.
- **Permission ile giriş (backend permissions dönüyor)** – Menü ve ekran erişimi `SCREEN_REQUIRED_PERMISSION` ile; örn. sadece `report.view` olan kullanıcı yalnızca Reports’u görür.
- **RoleBasedNavigation** – Hem permission-first hem role fallback ile doğru öğelerin listelenmesi ve tıklanınca yetkisiz uyarısı verilmemesi.
- **AdminOnlyGate (POS RoleGuard)** – Admin/SuperAdmin veya ilgili permission’a sahip kullanıcılar korumalı içeriği görmeli.

---

## 4. Canonical rol seti (backend ile aynı)

- SuperAdmin  
- Admin  
- Manager  
- Cashier  
- Waiter  
- Kitchen  
- ReportViewer  
- Accountant  

Administrator, BranchManager, Auditor kullanılmıyor; kod taramasında bu string’lere rastlanmadı.

---

## 5. Unauthorized / 403 davranışı

- **frontend-admin**: PermissionRouteGuard, yetkisiz veya permission yoksa `router.replace('/403')`. AdminOnlyGate yetkisizse `/403`. 403 sayfasının var olduğundan emin olun (gerekirse basit bir `/app/403/page.tsx` eklenir).
- **frontend (POS)**: Yetkisiz ekran erişiminde `Alert.alert` ile uyarı; sessiz allow yok.
