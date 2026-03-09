# Authorization Migration – Refactor Edilecek Kod Alanları Envanteri

**Tarih:** 2025-03-09

**Bağlam:** POS + backoffice hibrit; authorization büyük ölçüde role bazlı; hedef role + permission; Administrator legacy alias; minimum kırılımlı migration. Aranan pattern’ler: `Authorize(Roles`, `[Authorize(`, `AllowAnonymous`, `IsInRole(`, `role ==`/`role !=`, `Claims`, `permission`, `Administrator`, `Admin`, `Manager`, `Cashier`, `Waiter`.

---

## 1) Controller’lar – [Authorize] / [Authorize(Policy = "...")]

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Controllers/AuditLogController.cs` | `[Authorize]` (class), `[Authorize(Policy = "UsersView")]`, `AuditView`, `AuditViewWithCashier`, `AuditAdmin`; `[HasPermission(AppPermissions.AuditCleanup)]` (cleanup) | Kritik denetim; cleanup zaten permission. Diğer GET/export policy → HasPermission (audit.view, audit.export) ile değiştirilebilir. | **High** |
| `backend/Controllers/PaymentController.cs` | `[Authorize(Policy = "PosSales")]`, `PosTse`, `PosTseDiagnostics`; `[HasPermission(AppPermissions.PaymentTake)]` (CreatePayment) | Ödeme/iade/iptal kritik; Cancel/Refund için HasPermission (payment.cancel, refund.create) eklenmeli. | **High** |
| `backend/Controllers/OrdersController.cs` | `[Authorize(Policy = "PosTableOrder")]`; `[HasPermission(AppPermissions.OrderUpdate)]` (UpdateOrderStatus) | Sipariş durum/iptal kritik; DeleteOrder için order.cancel permission eklenmeli. | **High** |
| `backend/Controllers/CompanySettingsController.cs` | `[Authorize]`, `[Authorize(Policy = "BackofficeSettings")]`, `BackofficeManagement` | Şirket ayarı kritik; settings.manage permission’a taşınacak. | **High** |
| `backend/Controllers/FinanzOnlineController.cs` | `[Authorize(Policy = "BackofficeSettings")]` | FinanzOnline config/submit kritik; finanzonline.view, .manage, .submit permission. | **High** |
| `backend/Controllers/InvoiceController.cs` | `[Authorize]`, `[Authorize(Policy = "SystemCritical")]` (BackfillFromPayments) | Fatura/kredi notu/backfill; invoice.*, creditnote.create, settings.manage. | **High** |
| `backend/Controllers/InventoryController.cs` | `[Authorize]`, `InventoryManage`, `InventoryDelete` | Stok adjust kritik; inventory.view, .manage, .adjust permission. | **Medium** |
| `backend/Controllers/CategoriesController.cs` | `[Authorize(Policy = "PosCatalogRead")]`, `CatalogManage` | Katalog; category.view, category.manage. | **Medium** |
| `backend/Controllers/ModifierGroupsController.cs` | `PosCatalogRead`, `CatalogManage` | Modifier; product.view, product.manage. | **Medium** |
| `backend/Controllers/AdminProductsController.cs` | `[Authorize(Policy = "CatalogManage")]` | Ürün yönetimi; product.manage. | **Medium** |
| `backend/Controllers/ProductController.cs` | `[Authorize(Policy = "PosCatalogRead")]` | Ürün okuma (POS); product.view. | **Medium** |
| `backend/Controllers/TableController.cs` | `[Authorize(Policy = "PosTableOrder")]` | Masa; table.view, table.manage. | **Medium** |
| `backend/Controllers/CustomerController.cs` | `[Authorize(Policy = "PosTableOrder")]` | Müşteri; customer.view, customer.manage. | **Medium** |
| `backend/Controllers/CartController.cs` | `[Authorize(Policy = "PosSales")]` | Sepet; cart.manage, sale.view. | **Medium** |
| `backend/Controllers/ReceiptsController.cs` | `[Authorize(Policy = "PosSales")]` | Fiş; sale.view, payment.take. | **Medium** |
| `backend/Controllers/CashRegisterController.cs` | `[Authorize]`, `[Authorize(Policy = "CashRegisterManage")]` | Kasa; cashregister.view, settings.manage (create). | **Medium** |
| `backend/Controllers/TseController.cs` | `[Authorize(Policy = "PosTse")]` | TSE; shift.view, payment.take. | **Medium** |
| `backend/Controllers/TagesabschlussController.cs` | `[Authorize(Policy = "PosTse")]` | Vardiya; shift.view, shift.close. | **Medium** |
| `backend/Controllers/ReportsController.cs` | `[Authorize(Policy = "BackofficeManagement")]` | Rapor; report.view, report.export. | **Medium** |
| `backend/Controllers/LocalizationController.cs` | `[Authorize]`, `[Authorize(Policy = "BackofficeSettings")]` | Yerelleştirme; settings.view, localization.manage. | **Medium** |
| `backend/Controllers/SettingsController.cs` | `[Authorize]`, `BackofficeSettings` | Sistem ayarları; settings.view, settings.manage. | **Medium** |
| `backend/Controllers/MultilingualReceiptController.cs` | `[Authorize]`, `BackofficeSettings` | Fiş şablonu; receipttemplate.view, receipttemplate.manage. | **Medium** |
| `backend/Controllers/UserManagementController.cs` | `[Authorize]`, `UsersView`, `UsersManage` | Kullanıcı yönetimi; user.view, user.manage. | **Medium** |
| `backend/Controllers/AdminUsersController.cs` | `[Authorize(Policy = "AdminUsers")]` | Admin kullanıcı listesi; user.view. | **Medium** |
| `backend/Controllers/Base/EntityController.cs` | `[Authorize(Policy = "SystemCritical")]` (permanent delete) | Kalıcı silme; settings.manage. | **High** |
| `backend/Controllers/Base/BaseController.cs` | `[Authorize]` (class), `User.IsInRole(role)`, `HasRole`, `HasAnyRole` | Tüm controller’lar base’den türer; HasRole/HasAnyRole role bazlı – permission tabanlı helper eklenebilir. | **Medium** |
| `backend/Controllers/UserSettingsController.cs` | `[Authorize]` | Kendi ayarları; authenticated yeterli. | **Low** |
| `backend/Controllers/TestController.cs` | `[AllowAnonymous]`, `//[Authorize(Roles = "Admin")]` | Test endpoint; migration dışı. | **Low** |

---

## 2) Inline role / claim kullanımı

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Controllers/CartController.cs` | `userRole != "Admin" && userRole != "Kasiyer"` (ForceCleanup) | Hardcoded rol; "Kasiyer" typo (Cashier). cart.manage veya settings.manage permission ile değiştirilmeli. | **High** |
| `backend/Controllers/CartController.cs` | `User.FindFirst(ClaimTypes.Role)?.Value` (birkaç yerde) | Rol okuma; permission claim veya RolePermissionMatrix ile tutarlı kullanım. | **Medium** |
| `backend/Controllers/CashRegisterController.cs` | (UserManager<ApplicationUser>) | Role bilgisi Identity’den; refactor’da dokunulmayabilir. | **Low** |

---

## 3) Auth helper / filter / attribute sınıfları

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Authorization/HasPermissionAttribute.cs` | `[HasPermission(permission)]` | Yeni permission attribute; kullanım artırılacak. | — (mevcut) |
| `backend/Authorization/RequirePermissionAttribute.cs` | `[RequirePermission(permission)]` | Alternatif attribute; HasPermission ile aynı mantık. | — (mevcut) |
| `backend/Authorization/PermissionAuthorizationHandler.cs` | `user.Claims` (permission), `GetRolesFromContext`, `RolePermissionMatrix.GetPermissionsForRoles` | Permission claim öncelikli; role fallback. Migration’da değişiklik gerekmez. | — (mevcut) |
| `backend/Authorization/AuthorizationExtensions.cs` | `AddPermissionPolicies()` | Policy kayıt; mevcut. | — (mevcut) |
| `backend/Middleware/ForbiddenResponseAuthorizationHandler.cs` | `[Authorize(Roles="...")]` yorumu, `a.Roles`, "role" claim | 403 cevabında policy/role gösterimi; permission policy için de çalışır. | **Low** |

---

## 4) Login / JWT / claims üreten kod

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Controllers/AuthController.cs` | `GetRolesAsync`, `user.Role`, `RoleCanonicalization.GetCanonicalRole`, `ITokenClaimsService.BuildClaimsAsync`, `RolePermissionMatrix.GetPermissionsForRoles`, `permissions` response | Login/me’de rol + permission claim; zaten permission set dönüyor. | — (uyumlu) |
| `backend/Services/TokenClaimsService.cs` | `BuildClaimsAsync`, `user.Role`, `RoleCanonicalization`, `RolePermissionMatrix`, `PermissionCatalog.PermissionClaimType` | JWT’ye permission claim yazıyor; migration ile uyumlu. | — (uyumlu) |
| `backend/Services/ITokenClaimsService.cs` | `BuildClaimsAsync(ApplicationUser, IList<string> roles)` | Claims sözleşmesi; gerekirse scope claim eklenir. | **Low** |

---

## 5) Kullanıcı modeli ve rol bilgisinin tutulduğu yapı

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Models/ApplicationUser.cs` | `public string Role { get; set; } = "Cashier"` | Legacy tek rol; Identity AspNetUserRoles ile birlikte kullanılıyor. Uzun vadede role-permission tablosu. | **Low** (şimdilik korunacak) |
| `backend/Data/RoleSeedData.cs` | `RoleExistsAsync("Administrator")`, `"Admin"`, `"Cashier"`, `"Waiter"`, `"Manager"` vb. | Rol seed; Administrator legacy, yeni atamada Admin tercih. | **Low** |
| `backend/Auth/RoleCanonicalization.cs` | Administrator → Admin eşlemesi | Legacy alias; mevcut. | — (mevcut) |
| `backend/Authorization/Roles.cs` | `Admin`, `Manager`, `Cashier`, `Waiter`, `Administrator` sabitleri | Rol adları tek kaynak; policy ve seed ile uyumlu. | — (mevcut) |

---

## 6) Service katmanında permission kontrolü

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Services/UserService.cs` | `HasPermissionAsync(userId, permission)`, `user.Role switch { "Admin" => true, "Cashier" => permission switch { ... }, "Demo" => ... }` | Hardcoded rol/permission eşlemesi; RolePermissionMatrix ile değiştirilmeli (Identity rollerini alıp matrix’ten permission set çıkar). | **High** |
| `backend/Services/IUserService.cs` | `Task<bool> HasPermissionAsync(string userId, string permission)` | Arayüz kalır; implementasyon RolePermissionMatrix kullanacak. | **Medium** |

---

## 7) Pipeline / Program.cs – policy kayıt ve auth

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/Program.cs` | `AddAuthorization`, `RequireRole("SuperAdmin", "Admin", "Administrator", ...)` (tüm policy’ler), `AddPermissionPolicies()`, `PermissionAuthorizationHandler`, `ForbiddenResponseAuthorizationHandler`, `ITokenClaimsService`, `IScopeCheckService` | Role policy’ler legacy kalacak; yeni endpoint’ler HasPermission ile. Policy isimleri değişmez, sadece yeni endpoint’ler permission kullanır. | **Low** (yeni policy ekleme yok) |

---

## 8) Test projeleri

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| `backend/KasseAPI_Final.Tests/AuthControllerTests.cs` | `ITokenClaimsService` mock, `BuildClaimsAsync` | Login testleri; TokenClaimsService zaten kullanılıyor. | **Low** |
| `backend/KasseAPI_Final.Tests/AdminUsersControllerTests.cs` | `ClaimsPrincipal`, `ClaimTypes.Role`, "Administrator", "Cashier" | Rol bazlı test kullanıcısı; permission test için claim eklenebilir. | **Low** |
| `backend/KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs` | "Administrator", "Admin", Role atama | Kullanıcı yaşam döngüsü; roller mevcut. | **Low** |
| `backend/KasseAPI_Final.Tests/UserManagementAuthorizationPolicyTests.cs` | `UserWithRole(string role)` | Policy testleri; permission policy test eklenebilir. | **Low** |
| `backend/KasseAPI_Final.Tests/RoleCanonicalizationTests.cs` | RoleCanonicalization | Canonicalization birim testi. | — (mevcut) |

---

## 9) Önerilen / mevcut yeni dosyalar

| Dosya | Durum | Açıklama |
|-------|--------|----------|
| `backend/Authorization/AppPermissions.cs` | Var | Permission sabitleri; tek kaynak. |
| `backend/Authorization/PermissionCatalog.cs` | Var | All listesi, PolicyPrefix, PermissionClaimType. |
| `backend/Authorization/RolePermissionMatrix.cs` | Var | Rol → permission set. |
| `backend/Authorization/HasPermissionAttribute.cs` | Var | [HasPermission(permission)]. |
| `backend/Authorization/AuthorizationExtensions.cs` | Var | AddPermissionPolicies(). |
| `backend/Authorization/PermissionRequirement.cs` | Var | IAuthorizationRequirement. |
| `backend/Authorization/PermissionAuthorizationHandler.cs` | Var | Permission + role fallback. |
| `backend/Auth/RoleCanonicalization.cs` | Var | Administrator → Admin. |
| `backend/Services/TokenClaimsService.cs` | Var | JWT claims (role + permission). |
| `backend/Services/ITokenClaimsService.cs` | Var | Claims sözleşmesi. |
| `PermissionClaimsFactory` (ör. IClaimsFactory) | Öneri (opsiyonel) | TokenClaimsService zaten claim üretiyor; ayrı factory gerekmez. |
| `PermissionSeeder.cs` | Öneri (opsiyonel) | DB’de role-permission tablosu kullanılırsa seed; şu an RolePermissionMatrix statik. |

---

## 10) Uygulanacak refactor sırası

1. **Yüksek öncelik (kritik endpoint’ler + tek sert ihlal)**  
   - `CartController.cs`: ForceCleanup’taki `"Admin"` / `"Kasiyer"` kontrolünü kaldır; `[HasPermission(AppPermissions.CartManage)]` veya mevcut policy + permission ile değiştir.  
   - `UserService.cs`: `HasPermissionAsync` içindeki role switch’i kaldır; `UserManager.GetRolesAsync` + `RolePermissionMatrix.GetPermissionsForRoles` kullan.  
   - `PaymentController.cs`: Cancel ve Refund action’larına `[HasPermission(AppPermissions.PaymentCancel)]`, `[HasPermission(AppPermissions.RefundCreate)]` ekle.  
   - `OrdersController.cs`: DeleteOrder’a `[HasPermission(AppPermissions.OrderCancel)]` ekle.  
   - `CompanySettingsController.cs`: PUT action’larına `[HasPermission(AppPermissions.SettingsManage)]` ekle (policy yanında veya policy’yi permission’a taşı).  
   - `FinanzOnlineController.cs`: Config PUT’a `[HasPermission(AppPermissions.FinanzOnlineManage)]`, Submit’e `[HasPermission(AppPermissions.FinanzOnlineSubmit)]` ekle.  
   - `AuditLogController.cs`: Export’a `[HasPermission(AppPermissions.AuditExport)]`; cleanup zaten HasPermission.  
   - `InvoiceController.cs`: BackfillFromPayments’a `[HasPermission(AppPermissions.SettingsManage)]` ekle.  
   - `EntityController.cs`: Permanent delete zaten SystemCritical; istenirse `[HasPermission(AppPermissions.SettingsManage)]` eklenebilir.

2. **Orta öncelik (policy → permission, tutarlılık)**  
   - `IUserService.cs` / `UserService.cs`: HasPermissionAsync implementasyonunu RolePermissionMatrix ile yaz.  
   - Kalan controller’larda kritik olmayan action’lara HasPermission ekle (ENDPOINT_PERMISSION_MATRIX’e göre).  
   - `BaseController.cs`: İsteğe bağlı `HasPermission(string permission)` helper (claim veya RolePermissionMatrix ile).

3. **Düşük öncelik (temizlik, dokümantasyon)**  
   - `Program.cs`: Policy yorumlarını güncelle; role policy’lerin legacy olduğunu belirt.  
   - `ForbiddenResponseAuthorizationHandler`: Permission policy adlarını 403 cevabında anlamlı göstermek (zaten Policy adı dönüyor).  
   - Test: Yeni permission policy testleri eklenebilir.

---

## 11) İlk sprintte dokunulacak dosyalar

| # | Dosya | Yapılacak |
|---|--------|-----------|
| 1 | `backend/Controllers/CartController.cs` | ForceCleanup: "Admin"/"Kasiyer" inline kontrolü kaldır; `[HasPermission(AppPermissions.CartManage)]` veya benzeri. |
| 2 | `backend/Services/UserService.cs` | HasPermissionAsync: RolePermissionMatrix + GetRolesAsync kullan. |
| 3 | `backend/Controllers/PaymentController.cs` | Cancel: `[HasPermission(AppPermissions.PaymentCancel)]`; Refund: `[HasPermission(AppPermissions.RefundCreate)]`. |
| 4 | `backend/Controllers/OrdersController.cs` | DeleteOrder: `[HasPermission(AppPermissions.OrderCancel)]`. |
| 5 | `backend/Controllers/CompanySettingsController.cs` | PUT action’lar: `[HasPermission(AppPermissions.SettingsManage)]` ekle. |
| 6 | `backend/Controllers/FinanzOnlineController.cs` | PUT config: `[HasPermission(AppPermissions.FinanzOnlineManage)]`; Submit: `[HasPermission(AppPermissions.FinanzOnlineSubmit)]`. |
| 7 | `backend/Controllers/AuditLogController.cs` | Export: `[HasPermission(AppPermissions.AuditExport)]`. |
| 8 | `backend/Controllers/InvoiceController.cs` | BackfillFromPayments: `[HasPermission(AppPermissions.SettingsManage)]`. |
| 9 | `backend/Controllers/InventoryController.cs` | Adjust: `[HasPermission(AppPermissions.InventoryAdjust)]` (AppPermissions’ta varsa); yoksa InventoryManage. |
| 10 | `backend/Authorization/RolePermissionMatrix.cs` veya `AppPermissions.cs` | inventory.adjust zaten var; InventoryController’da adjust endpoint’ine HasPermission(InventoryAdjust) eklenir. |

Bu listeye göre ilk sprint: **CartController**, **UserService**, **PaymentController**, **OrdersController**, **CompanySettingsController**, **FinanzOnlineController**, **AuditLogController**, **InvoiceController**, **InventoryController** ve gerekirse **BaseController** (HasPermission helper) ile sınırlı tutulabilir.
