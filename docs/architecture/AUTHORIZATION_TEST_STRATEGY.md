# Authorization Migration – Test Stratejisi

**Tarih:** 2025-03-09  
**Hedef:** Role + permission geçişinde birim, entegrasyon, authorization ve regresyon testleri; negatif senaryolar ve frontend guard önerileri.

---

## 1. Test planı özeti

| Katman | Amaç | Araç / Konum |
|--------|------|----------------|
| **Unit** | RolePermissionMatrix, PermissionCatalog, RoleCanonicalization | xUnit, `KasseAPI_Final.Tests` |
| **Authorization** | Permission policy + handler (claim vs role fallback), Administrator alias | xUnit, IAuthorizationService |
| **Integration** | Kritik endpoint’lere farklı rollerle istek; 200 vs 403 | WebApplicationFactory veya custom TestServer |
| **Regression** | Mevcut role policy’lerin hâlâ çalışması | Policy tests (UsersView, UsersManage, PosSales vb.) |
| **Frontend** | Route/menu/button guard, hasPermission | Jest/React Testing Library, mock user with permissions |

---

## 2. Permission policy test senaryoları

- **Policy adı:** `Permission:{permission}` (örn. `Permission:payment.take`).
- **Handler:** PermissionAuthorizationHandler – önce JWT’deki `permission` claim’lere bakar, yoksa role’den RolePermissionMatrix ile türetir.

| # | Senaryo | Giriş | Beklenen |
|---|----------|--------|----------|
| P1 | Kullanıcıda ilgili permission claim var | Claims: `permission` = `payment.take` | Policy başarılı |
| P2 | Kullanıcıda permission yok, role’de var | Claims: `role` = Cashier; policy `payment.take` | Policy başarılı (role fallback) |
| P3 | Kullanıcıda ne permission ne de yetkili role var | Claims: `role` = ReportViewer; policy `payment.take` | Policy başarısız |
| P4 | Boş claim seti | Hiç role/permission claim yok | Policy başarısız |
| P5 | Birden fazla role, birinde permission var | Claims: `role` = Waiter, ReportViewer | policy `report.view` → başarılı |
| P6 | Permission claim ile role farklı; permission öncelikli | Claims: `permission` = `payment.take`, `role` = ReportViewer | Policy başarılı (claim öncelikli) |

---

## 3. Role → permission mapping testleri

- **Kaynak:** `RolePermissionMatrix.RoleHasPermission(roleName, permission)` ve `GetPermissionsForRoles(roleNames)`.

| # | Test adı (örnek) | Açıklama |
|---|-------------------|----------|
| R1 | RoleHasPermission_Cashier_Has_PaymentTake | Cashier için `payment.take` true |
| R2 | RoleHasPermission_Cashier_Has_RefundCreate | Cashier için `refund.create` true |
| R3 | RoleHasPermission_Waiter_Has_OrderCancel | Waiter için `order.cancel` true |
| R4 | RoleHasPermission_Waiter_DoesNotHave_RefundCreate | Waiter için `refund.create` false |
| R5 | RoleHasPermission_Manager_Has_AuditExport | Manager için `audit.export` true |
| R6 | RoleHasPermission_Manager_DoesNotHave_AuditCleanup | Manager için `audit.cleanup` false |
| R7 | RoleHasPermission_ReportViewer_Has_ReportView | ReportViewer için `report.view` true |
| R8 | RoleHasPermission_ReportViewer_DoesNotHave_PaymentTake | ReportViewer için `payment.take` false |
| R9 | GetPermissionsForRoles_MultipleRoles_ReturnsUnion | İki role verilince izinler birleşim kümesi |
| R10 | RoleHasPermission_UnknownRole_ReturnsFalse | Bilinmeyen rol için false |
| R11 | RoleHasPermission_NullOrEmptyRole_ReturnsFalse | null/boş rol için false (rol adı canonical olmalı; Identity/JWT’den gelen tam ad kullanılır) |

---

## 4. Administrator → Admin alias testleri

- **RoleCanonicalization:** Zaten `RoleCanonicalizationTests` ile var (Administrator → Admin, GetLegacyAliases).
- **RolePermissionMatrix:** Administrator rolü Admin ile aynı permission set’e sahip olmalı.

| # | Test adı (örnek) | Açıklama |
|---|-------------------|----------|
| A1 | Administrator_Has_SamePermissionsAs_Admin_For_SettingsManage | Administrator ve Admin için `settings.manage` ikisi de true |
| A2 | Administrator_Has_SamePermissionsAs_Admin_For_UserManage | `user.manage` ikisi de true |
| A3 | GetPermissionsForRoles_Administrator_Equals_Admin | GetPermissionsForRoles(["Administrator"]) ile GetPermissionsForRoles(["Admin"]) aynı set |
| A4 | RoleCanonicalization_GetCanonicalRole_Administrator_Returns_Admin | Mevcut RoleCanonicalizationTests ile uyumlu |

---

## 5. Kritik endpoint’ler için access matrix testleri (öneri)

Her satır: (Endpoint, Method, Rol, Beklenen HTTP).

| Endpoint | Method | Rol | Beklenen |
|----------|--------|-----|----------|
| /api/pos/payment (CreatePayment) | POST | Cashier | 200/201 |
| /api/pos/payment (CreatePayment) | POST | ReportViewer | 403 |
| /api/pos/payment/{id}/cancel | POST | Cashier | 200 |
| /api/pos/payment/{id}/cancel | POST | Waiter | 403 |
| /api/pos/payment/{id}/refund | POST | Cashier | 200 |
| /api/pos/payment/{id}/refund | POST | Waiter | 403 |
| /api/orders/{id} (DeleteOrder) | DELETE | Manager | 200 |
| /api/orders/{id}/status | PUT | Waiter | 200 |
| /api/companysettings | PUT | Admin | 200 |
| /api/companysettings | PUT | Cashier | 403 |
| /api/companysettings | PUT | Manager | 403 |
| /api/auditlog/cleanup | DELETE | Admin | 200 |
| /api/auditlog/cleanup | DELETE | Manager | 403 |
| /api/auditlog/export | GET | Manager | 200 |
| /api/auditlog/export | GET | Cashier | 403 |
| /api/finanzonline/config | PUT | Admin | 200 |
| /api/finanzonline/submit-invoice | POST | Admin | 200 |
| /api/finanzonline/submit-invoice | POST | Cashier | 403 |
| /api/inventory/{id}/adjust | POST | Manager | 200 |
| /api/inventory/{id}/adjust | POST | ReportViewer | 403 |

**Uygulama:** Integration test sınıfı; `WebApplicationFactory` veya TestServer; her senaryo için farklı JWT (role veya permission claim) ile istek atıp status code assert.

---

## 6. Frontend guard testleri (öneri)

- **Route guard:** Belirli path’e yönlendirildiğinde permission yoksa 403 veya redirect.
- **Menu:** `isMenuItemAllowed(key, permissions)` – permission listesine göre menü öğesinin görünür olup olmaması.
- **Button/aksiyon:** `hasPermission(user, 'settings.manage')` false ise buton disabled veya gizli.

| # | Test adı (örnek) | Açıklama |
|---|-------------------|----------|
| F1 | isMenuItemAllowed_WithSettingsView_ShowsSettingsItem | permissions içinde settings.view varsa /settings menüde |
| F2 | isMenuItemAllowed_WithoutUserView_HidesUsersItem | permissions’da user.view yoksa /users menüde yok |
| F3 | PermissionRouteGuard_WithoutOrderView_RedirectsTo403 | permission yokken /orders’a gidince 403 veya redirect |
| F4 | hasPermission_WithPaymentTake_ReturnsTrue | user.permissions içinde payment.take varsa true |
| F5 | PermissionGate_WithModeHide_WithoutPermission_RendersNothing | permission yoksa mode=hide’da children render edilmez |
| F6 | PermissionGateButton_WithoutRefundCreate_DisablesButton | refund.create yoksa iade butonu tıklanamaz/disabled |

**Araç:** Jest + React Testing Library; auth context/mock’ta `user: { permissions: ['…'] }` verilir.

---

## 7. Negatif senaryolar (zorunlu)

| # | Senaryo | Rol | Denenen aksiyon | Beklenen |
|---|----------|-----|------------------|----------|
| N1 | Waiter refund yapamaz | Waiter | POST …/refund (RefundCreate) | 403 |
| N2 | Cashier company settings güncelleyemez | Cashier | PUT /api/companysettings (SettingsManage) | 403 |
| N3 | Manager audit cleanup yapamaz | Manager | DELETE /api/auditlog/cleanup (AuditCleanup) | 403 |
| N4 | Report viewer ödeme alamaz | ReportViewer | POST /api/pos/payment (PaymentTake) | 403 |

Bu dört senaryo hem **RolePermissionMatrix** unit testleri (role’ün ilgili permission’a sahip olmadığını) hem de **integration/authorization** testleri (endpoint’e istekte 403) ile doğrulanmalı.

---

## 8. Örnek test case isimleri ve pseudo kod

### 8.1 RolePermissionMatrix

```csharp
[Fact] public void RoleHasPermission_Waiter_DoesNotHave_RefundCreate()
    => Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Waiter, AppPermissions.RefundCreate));

[Fact] public void RoleHasPermission_Cashier_DoesNotHave_SettingsManage()
    => Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Cashier, AppPermissions.SettingsManage));

[Fact] public void RoleHasPermission_Manager_DoesNotHave_AuditCleanup()
    => Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.Manager, AppPermissions.AuditCleanup));

[Fact] public void RoleHasPermission_ReportViewer_DoesNotHave_PaymentTake()
    => Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.ReportViewer, AppPermissions.PaymentTake));
```

### 8.2 PermissionAuthorizationHandler (policy evaluation)

```csharp
// Build services with AddAppAuthorization(); get IAuthorizationService.
// Policy name = PermissionCatalog.PolicyPrefix + AppPermissions.RefundCreate

[Fact] public async Task PermissionPolicy_RefundCreate_Denies_Waiter_Role()
{
    var user = CreatePrincipal(roles: new[] { "Waiter" }); // no permission claims
    var result = await _auth.AuthorizeAsync(user, null, new PermissionRequirement(AppPermissions.RefundCreate));
    Assert.False(result.Succeeded);
}

[Fact] public async Task PermissionPolicy_SettingsManage_Denies_Cashier_Role()
{
    var user = CreatePrincipal(roles: new[] { "Cashier" });
    var result = await _auth.AuthorizeAsync(user, null, new PermissionRequirement(AppPermissions.SettingsManage));
    Assert.False(result.Succeeded);
}

[Fact] public async Task PermissionPolicy_AuditCleanup_Denies_Manager_Role()
{
    var user = CreatePrincipal(roles: new[] { "Manager" });
    var result = await _auth.AuthorizeAsync(user, null, new PermissionRequirement(AppPermissions.AuditCleanup));
    Assert.False(result.Succeeded);
}

[Fact] public async Task PermissionPolicy_PaymentTake_Denies_ReportViewer_Role()
{
    var user = CreatePrincipal(roles: new[] { "ReportViewer" });
    var result = await _auth.AuthorizeAsync(user, null, new PermissionRequirement(AppPermissions.PaymentTake));
    Assert.False(result.Succeeded);
}
```

### 8.3 Administrator = Admin permission set

```csharp
[Fact] public void GetPermissionsForRoles_Administrator_Contains_All_That_Admin_Has()
{
    var adminPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Admin });
    var administratorPerms = RolePermissionMatrix.GetPermissionsForRoles(new[] { Roles.Administrator });
    Assert.Equal(adminPerms.Count, administratorPerms.Count);
    foreach (var p in adminPerms)
        Assert.True(administratorPerms.Contains(p));
}
```

### 8.4 Integration (pseudo)

```csharp
[Fact] public async Task POST_Refund_As_Waiter_Returns_403()
{
    var client = CreateClientWithToken(role: "Waiter");
    var response = await client.PostAsJsonAsync("/api/pos/payment/some-guid/refund", new { Amount = 10, Reason = "Test" });
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

---

## 9. Regresyon testleri

- **Mevcut policy testleri korunmalı:** `UserManagementAuthorizationPolicyTests` (UsersView, UsersManage, Admin, Administrator, Cashier deny).
- **RoleCanonicalizationTests** aynen çalışmalı.
- Yeni permission testleri eklendikten sonra tüm test suite’i yeşil kalmalı; özellikle Auth, UserManagement, Payment, Orders ile ilgili mevcut testler tekrar çalıştırılmalı.

---

## 10. Özet checklist

- [ ] RolePermissionMatrix unit testleri (RoleHasPermission, GetPermissionsForRoles; pozitif + negatif).
- [ ] Administrator = Admin permission set testleri.
- [ ] PermissionAuthorizationHandler / permission policy testleri (claim öncelikli, role fallback, negatif roller).
- [ ] N1–N4 negatif senaryolar (Waiter refund, Cashier settings, Manager audit cleanup, ReportViewer payment) unit ve mümkünse integration.
- [ ] Kritik endpoint access matrix için en az bir integration test sınıfı (ör. Payment, CompanySettings, AuditLog, FinanzOnline, Inventory).
- [ ] Frontend: isMenuItemAllowed, hasPermission, PermissionRouteGuard / PermissionGate için en az birer örnek test.
- [ ] Regresyon: Mevcut auth ve policy testlerinin geçtiğinden emin ol.
