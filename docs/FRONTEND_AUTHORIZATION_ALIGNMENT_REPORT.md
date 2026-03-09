# Frontend Authorization Alignment Report

**Date:** 2025-03  
**Scope:** frontend (POS), frontend-admin (Admin panel) aligned with backend permission-first refactor.  
**Single source of truth:** [FinalAuthorizationModel.md](FinalAuthorizationModel.md) — roles, permissions, FE strategy.

Administrator is not a role; use **Admin** or **SuperAdmin**. Permission-based UI gating and role fallback applied.

---

## 1. FE’de “Administrator” kaldırılan yerler

| Location | Change |
|----------|--------|
| **frontend-admin** `src/features/users/constants/copy.ts` | `noPermission`: "Nur Administratoren können Benutzer verwalten." → "Nur Admins (Admin/SuperAdmin) können Benutzer verwalten." |
| **frontend-admin** `src/features/auth/constants/roles.ts` | Removed BranchManager, Auditor from `ROLES_CAN_VIEW_USERS` and `ROLES_CAN_MANAGE_USERS`. No "Administrator" reference (was already removed in prior cleanup). |
| **frontend-admin** `src/app/(protected)/users/page.tsx` | Alert description: no longer mentions "BranchManager, Auditor"; text now refers to permission "Benutzer anzeigen" (e.g. SuperAdmin, Admin, Manager). `ROLE_OPTIONS`: removed BranchManager, Auditor; added Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. |

**Not:** frontend (POS) kodunda "Administrator" string’i yoktu; sadece Admin kullanılıyordu.

---

## 2. Permission-first yapılan alanlar

### frontend-admin

| Area | Implementation |
|------|----------------|
| **Sidebar / menü** | `(protected)/layout.tsx`: Token’da `user.permissions` doluysa menü öğeleri `isMenuItemAllowed(key, permissions)` ile filtreleniyor. Boşsa rol fallback: `canViewUsers`, `canShowRksvMenu`. Tüm menü (dashboard, invoices, products, users, settings, rksv, …) permission ile gizlenebilir. |
| **Users sayfası yetkisi** | `shared/auth/usersPolicy.ts`: `getUsersPolicy(actorRole, permissions?)` permission-first. `user.permissions` varsa `user.view` / `user.manage` ile `canView`, `canCreate`, `canEdit`, `canDeactivate`, `canReactivate` belirleniyor; yoksa rol fallback (SuperAdmin, Admin, Manager). `canCreateRole` ve `canResetPassword` hâlâ rol tabanlı (SuperAdmin kuralları). |
| **Route guard** | `PermissionRouteGuard` ve `ROUTE_PERMISSIONS` zaten permission tabanlı; değişiklik yok. |
| **Permission sabitleri** | `shared/auth/permissions.ts`: Backend ile uyum için eklendi: `INVENTORY_DELETE`, `CASHREGISTER_VIEW`, `CASHREGISTER_MANAGE`, `LOCALIZATION_VIEW`, `LOCALIZATION_MANAGE`, `TSE_SIGN`, `TSE_DIAGNOSTICS`, `SYSTEM_CRITICAL`, `PRICE_OVERRIDE`, `RECEIPT_REPRINT`. |
| **Menü visibility** | `menuPermissions.ts` + `layout` ile menü öğeleri path → permission eşlemesine göre gösteriliyor (user.view, product.view, audit.view, settings.view, finanzonline.manage vb.). |

### frontend (POS)

| Area | Implementation |
|------|----------------|
| **Rol seti** | `types/auth.ts`: `UserRole` backend ile uyumlu: SuperAdmin, Admin, Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant. |
| **PermissionHelper** | `shared/utils/PermissionHelper.ts`: `UserRole` enum’a SuperAdmin, Waiter, Kitchen, ReportViewer, Accountant eklendi. `PERMISSIONS` ve `SCREEN_ACCESS` bu rollere göre güncellendi. Admin yetkileri Admin + SuperAdmin. |
| **usePermission** | `hooks/usePermission.ts`: `isAdmin` = `hasRole('Admin') || hasRole('SuperAdmin')`. `isSuperAdmin` eklendi. Return type’a `isSuperAdmin` eklendi. |
| **ReportDisplay** | `userRole` tipi: 'SuperAdmin' eklendi. |

**Not:** POS tarafında API’den gelen `user.permissions` ile permission-first guard (örn. menü/buton) kullanımı mevcut auth/context yapısında sınırlı; rol tabanlı erişim (PermissionHelper + usePermission) kullanılmaya devam ediyor. İleride token’da permission listesi kullanılırsa aynı pattern (permission varsa ona göre, yoksa role göre) uygulanabilir.

---

## 3. Sadece role-based bırakılan alanlar

| Area | Reason |
|------|--------|
| **AdminOnlyGate (FE-Admin)** | Admin veya SuperAdmin kontrolü rol ile: `user?.role === 'Admin' \|\| user?.role === 'SuperAdmin'`. Permission ile de yapılabilir (örn. user.manage) ama mevcut tasarım “admin sayfaları” için rol kullanıyor. |
| **Users: canCreateRole, canResetPassword** | Backend’de sadece SuperAdmin rol kuralı var; bu yüzden policy’de rol tabanlı kaldı. |
| **RKSV menü (rol fallback)** | Token’da permission yoksa `canShowRksvMenu(user?.role)` (SuperAdmin, Admin) ile menü gösteriliyor. |
| **frontend RoleGuard / AdminOnly** | Admin veya SuperAdmin için `hasRole('Admin') \|\| hasRole('SuperAdmin')`. |
| **frontend RoleBasedNavigation** | Hangi ekranların gösterileceği `PermissionHelper.getUserRole()` ve `NAVIGATION_ITEMS[].roles` ile rol bazlı. |
| **frontend PermissionHelper.hasScreenAccess** | Rol → ekran listesi (`SCREEN_ACCESS`) ile. |

---

## 4. Test edilmesi gereken ekranlar / akışlar

### frontend-admin

- **Login** → `/me` ile `role` ve `permissions` dönüyor mu kontrol et.
- **Menü:** Admin ile giriş → Users, RKSV, Settings, Products, Audit, Invoices vb. görünüyor mu. Manager ile → Users görünmemeli (user.view yok), Reports/Audit/Products görünmeli. ReportViewer → sadece report/audit/settings görünür olmalı.
- **/users:** SuperAdmin/Admin → liste, create, edit, deactivate, reset password. Manager → sadece liste (user.view), create/edit butonları yok (user.manage yok). Permission’sız token (eski API) → rol fallback ile Manager liste görebilir, Admin create/edit yapabilir.
- **/403:** Yetkisiz sayfaya gidince yönlendirme.
- **RKSV alt menü:** Sadece Admin/SuperAdmin (veya finanzonline.manage / settings.view) ile görünmeli.

### frontend (POS)

- **Login** → `user.role` SuperAdmin, Admin, Manager, Cashier, Waiter vb. doğru geliyor mu.
- **AdminOnly / RoleGuard:** Admin veya SuperAdmin ile korunan içerik görünür; sadece Cashier ile görünmez.
- **RoleBasedNavigation:** Cashier → Sales, Products, Cart, Customers, Tables. Admin/SuperAdmin → buna ek users, roles, system, reports, audit, inventory, finanzonline, backup. Manager → POS + reports, audit, staff, schedule.
- **ReportDisplay:** userRole Admin, Manager, Cashier, SuperAdmin ile render/export davranışı.
- **PermissionHelper.getUserRole / setUserRole:** Login sonrası rol doğru yazılıyor ve ekran erişimi buna göre çalışıyor mu.

---

## 5. Özet

- **Administrator:** Sadece FE-Admin copy ve users sayfası metinleri/rol listesi güncellendi; artık yalnızca Admin/SuperAdmin (ve rol listesinde Manager, Cashier, Waiter, Kitchen, ReportViewer, Accountant) kullanılıyor.
- **Permission-first:** FE-Admin menü ve Users sayfası policy’si token’daki `permissions` ile çalışıyor; permissions yoksa rol fallback.
- **Role-based kalan:** AdminOnlyGate, Users canCreateRole/canResetPassword, POS menü/ekran erişimi (RoleBasedNavigation, PermissionHelper), RKSV menü fallback.
- **Sabitler:** frontend-admin `roles.ts` ve `permissions.ts`, frontend `UserRole` ve `PermissionHelper` backend rol/permission seti ile hizalandı.
