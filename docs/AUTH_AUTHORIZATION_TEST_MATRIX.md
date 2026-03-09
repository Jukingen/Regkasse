# Auth / Authorization / Permission Test Matrix

**Canonical auth model:** [FinalAuthorizationModel.md](FinalAuthorizationModel.md).

**Scope:** Administrator removed; single admin role **Admin**; permission-first model; FE/BE share same role/permission names. Demo environment — no backward-compatibility tests for legacy roles.

---

## 1. Updated test matrix

### Backend (KasseAPI_Final.Tests)

| Test file | Kapsam | Değişiklik |
|-----------|--------|------------|
| **UserManagementAuthorizationPolicyTests** | user.view / user.manage | Legacy policy (UsersView, UsersManage) kaldırıldı. Permission-first: AddAppAuthorization + PermissionPolicy(UserView/UserManage). Admin, SuperAdmin, Manager (view); Admin, SuperAdmin (manage); Cashier/Waiter denied. |
| **RolePermissionMatrixTests** | Rol → permission | Administrator referansı yok. SuperAdmin SystemCritical, Admin yok; Waiter CartView/CartManage ayrımı; Manager UserView/UserManage, InventoryDelete; GetPermissionsForRoles. |
| **PermissionAuthorizationHandlerTests** | Permission policy | UserView/UserManage, SystemCritical (SuperAdmin/Admin), InventoryDelete, TseDiagnostics, CartManage (Waiter deny, Cashier allow) senaryoları eklendi. |
| **AdminUsersControllerTests** | AdminUsers controller | actorRole "Administrator" → "Admin". |
| **AuditLogControllerGetUserAuditLogsTests** | AuditLog controller | role / UserRole "Administrator" → "Admin". |
| **UserManagementControllerUserLifecycleTests** | User lifecycle | actorRole "Administrator" → "Admin". |
| **UserLifecycleAuditIntegrationTests** | Audit log integration | "Administrator" → "Admin" (actor role in LogUserLifecycleAsync). |
| **RoleCanonicalizationTests** | Canonicalization | Zaten sadece trim/empty; Administrator senaryosu yok. |
| **PaymentSecurityMiddlewareTests** | Payment middleware | **Yeni.** Admin, SuperAdmin, Manager, Cashier allowed; Waiter ve unauthenticated 403. |
| **CartControllerForceCleanupAuthorizationTests** | Cart force-cleanup | **Yeni.** Force-cleanup CartManage gerektirir; Cashier/Manager/Admin have, Waiter does not. |
| **EndpointAuthorizationRepresentativeTests** | Temsilci endpoint auth | **Yeni.** Users (UserView/UserManage), Catalog (ProductView/ProductManage), Inventory (View/Delete), Reports (ReportView), Settings (SettingsManage), POS (CartManage), TSE (TseSign/TseDiagnostics), SystemCritical. |

### Frontend (frontend-admin)

| Test file | Kapsam | Değişiklik |
|-----------|--------|------------|
| **users/__tests__/page.test.tsx** | Users sayfası | useAuth role: 'Admin', useRoles canonical list. **Yeni:** authorization describe – canView: false iken no-permission alert (Nur mit Berechtigung / Benutzer anzeigen). beforeEach’te mockUseUsersPolicy varsayılan canView: true. |
| **useUsersList.test.ts** | useUsersList hook | role: 'Admin' (zaten). |

---

## 2. Missing tests (önerilen ekler)

| Alan | Eksik | Öncelik |
|------|--------|---------|
| **Frontend route guard** | PermissionRouteGuard / layout’ta route bazlı 403 redirect testi | Orta |
| **Frontend menu visibility** | usePermissions + isMenuItemAllowed ile menü öğelerinin role/permission’a göre gizlenmesi | Orta |
| **Frontend button visibility** | Users sayfasında canCreate/canEdit false iken butonların gizli olması | Düşük |
| **E2E** | Gerçek login (Admin, Manager, Cashier, Waiter) + kritik sayfa erişimi (403/200) | Yüksek (manuel veya Playwright) |
| **Integration** | WebApplicationFactory ile gerçek pipeline: GET /api/UserManagement with Bearer (Cashier) → 403 | Orta |
| **PaymentSecurityMiddleware** | Claim type "role" vs ClaimTypes.Role – production JWT ile uyum testi | Düşük (not: middleware şu an "role" arıyor) |

---

## 3. Manual smoke checklist

Her rol ile login olup aşağıdaki adımlar çalıştırılmalı.

| # | Senaryo | Admin | SuperAdmin | Manager | Cashier | Waiter | ReportViewer |
|---|---------|-------|------------|---------|---------|--------|--------------|
| 1 | **Login** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| 2 | **Users list (GET /api/UserManagement)** | ✅ | ✅ | ✅ | ❌ 403 | ❌ 403 | ❌ 403 |
| 3 | **User create (POST)** | ✅ | ✅ | ❌ 403 | ❌ | ❌ | ❌ |
| 4 | **Inventory list** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 5 | **Inventory delete** | ✅ | ✅ | ❌ 403 | ❌ | ❌ | ❌ |
| 6 | **Payment take (POS)** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| 7 | **Payment cancel / refund** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 8 | **Report view** | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| 9 | **Report export** | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ |
| 10 | **Settings manage (PUT)** | ✅ | ✅ | ❌ 403 | ❌ | ❌ | ❌ |
| 11 | **User manage (create/edit/deactivate)** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| 12 | **TSE actions (sign, daily close)** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| 13 | **TSE diagnostics (signature-debug)** | ✅ | ✅ | ❌ 403 | ❌ 403 | ❌ | ❌ |
| 14 | **System critical (backfill, permanent delete)** | ❌ 403 | ✅ | ❌ | ❌ | ❌ | ❌ |
| 15 | **Cart force-cleanup** | ✅ | ✅ | ✅ | ✅ | ❌ 403 | ❌ |

- **Admin login:** Token’da role: Admin, permissions list (system.critical hariç). Users, Catalog, Inventory, Reports, Settings, POS, TSE (sign) erişimi. Backfill ve permanent delete 403.
- **Manager login:** User view/create değil manage; inventory delete 403; settings manage 403; TSE diagnostics 403.
- **Cashier login:** Users 403; Report view 403; Cart manage ve payment take/cancel/refund ok; TSE sign ok, diagnostics 403.
- **Waiter login:** Users 403; Inventory 403; Report 403; Cart manage 403 (sadece view); Payment take ok; TSE sign 403 (matrix’e göre Waiter’da TseSign var mı kontrol et – mevcut matrix’te Waiter’da TseSign yok).
- **ReportViewer login:** Sadece report view/export, audit view, settings view; users/catalog/inventory manage yok.

*(Tablo matrix ile çelişirse RolePermissionMatrix.cs esas alınır.)*

---

## 4. Go/no-go criteria for demo validation

**GO kriterleri (hepsi sağlanmalı):**

1. **Backend unit testler:** Tüm auth/permission testleri geçiyor (UserManagementAuthorizationPolicyTests, RolePermissionMatrixTests, PermissionAuthorizationHandlerTests, PaymentSecurityMiddlewareTests, CartControllerForceCleanupAuthorizationTests, EndpointAuthorizationRepresentativeTests).
2. **Administrator yok:** Kod ve testlerde "Administrator" rolü yok; tek admin rolü "Admin".
3. **Permission-first:** Endpoint’ler HasPermission ile korunuyor; legacy role policy kullanılmıyor.
4. **Smoke (manuel):** Admin, Manager, Cashier, Waiter, ReportViewer ile login; yukarıdaki tabloya uygun 200/403 davranışı.
5. **FE Users sayfası:** Admin/Manager ile liste ve (sadece Admin) create; Cashier ile no-permission alert.
6. **TSE & SystemCritical:** TSE diagnostics ve system.critical (backfill, permanent delete) yalnızca yetkili rollerle (SuperAdmin for critical).

**NO-GO (düzeltilmeden demo’a geçilmez):**

1. Admin kullanıcı Users veya Settings’e erişemiyorsa.
2. Cashier veya Waiter, Users veya Report view’a 200 alıyorsa.
3. SuperAdmin dışında bir rol backfill veya permanent delete yapabiliyorsa.
4. Waiter payment cancel/refund veya cart manage (force-cleanup) yapabiliyorsa (matrix’e göre).
5. Auth/permission ile ilgili unit testler kırmızıysa.

---

## 5. Test komutları

```bash
# Backend
cd backend && dotnet test --filter "FullyQualifiedName~UserManagementAuthorizationPolicyTests|FullyQualifiedName~RolePermissionMatrixTests|FullyQualifiedName~PermissionAuthorizationHandlerTests|FullyQualifiedName~PaymentSecurityMiddlewareTests|FullyQualifiedName~CartControllerForceCleanupAuthorizationTests|FullyQualifiedName~EndpointAuthorizationRepresentativeTests"

# Tüm backend testler
dotnet test

# Frontend (frontend-admin)
npm run test -- --run src/app/\(protected\)/users/__tests__/page.test.tsx
```
