# Authorization Migration – Refactor Edilecek Dosyalar Taraması

**Tarih:** 2025-03-09  
**Amaç:** Role-based → permission-based geçişte değiştirilecek/adreslenecek tüm kullanımların listesi.

---

## 1) Tarama sonuçları tablosu

| File | MatchedCode | WhyItMatters | RefactorPriority |
|------|-------------|--------------|------------------|
| **Controllers/CartController.cs** | `[Authorize(Policy = "PosSales")]` (class) | POS sepet; policy → permission (sale.view, sale.create) | High |
| **Controllers/CartController.cs** | `userRole != "Admin" && userRole != "Kasiyer"` (L1029) | Inline role check; typo "Kasiyer", canonical "Cashier" yok; permission ile değiştirilmeli | **Critical** |
| **Controllers/PaymentController.cs** | `[Authorize(Policy = "PosSales")]`, `PosTse`, `PosTseDiagnostics` | Ödeme/TSE; bir kısım zaten [HasPermission(PaymentTake)]; diğer action’lar permission’a taşınacak | High |
| **Controllers/OrdersController.cs** | `[Authorize(Policy = "PosTableOrder")]` | Sipariş; bir action [HasPermission(OrderUpdate)]; class/actions permission’a taşınacak | High |
| **Controllers/AuditLogController.cs** | `[Authorize]`, `UsersView`, `AuditView`, `AuditViewWithCashier`, `AuditAdmin` | Denetim; cleanup zaten [HasPermission(AuditCleanup)]; tüm action’lar permission’a taşınacak | High |
| **Controllers/CompanySettingsController.cs** | `[Authorize]`, `BackofficeSettings`, `BackofficeManagement` | Şirket ayarları; kritik endpoint’ler permission ile korunacak | High |
| **Controllers/FinanzOnlineController.cs** | `[Authorize(Policy = "BackofficeSettings")]` (class) | FinanzOnline config/submit; permission (finanzonline.manage, finanzonline.submit) | High |
| **Controllers/InvoiceController.cs** | `[Authorize]`, `[Authorize(Policy = "SystemCritical")]` (backfill) | Fatura ve backfill; action bazlı permission | High |
| **Controllers/InventoryController.cs** | `[Authorize]`, `InventoryManage`, `InventoryDelete` | Stok; view/manage/delete permission ayrımı | Medium |
| **Controllers/CashRegisterController.cs** | `[Authorize]`, `CashRegisterManage` | Kasa; permission (cashregister.view, cashregister.manage, cashdrawer.open, shift.close) | Medium |
| **Controllers/CategoriesController.cs** | `PosCatalogRead`, `CatalogManage` | Kategori; category.view, category.manage | Medium |
| **Controllers/ModifierGroupsController.cs** | `PosCatalogRead`, `CatalogManage` (çoklu action) | Modifier; product.view, product.manage | Medium |
| **Controllers/AdminProductsController.cs** | `[Authorize(Policy = "CatalogManage")]` (class) | Admin ürün; product.view (GET), product.manage (write) | Medium |
| **Controllers/ProductController.cs** | `[Authorize(Policy = "PosCatalogRead")]` (class) | POS ürün; product.view / product.manage ayrımı | Medium |
| **Controllers/TableController.cs** | `[Authorize(Policy = "PosTableOrder")]` | Masa; table.view, table.manage | Medium |
| **Controllers/CustomerController.cs** | `[Authorize(Policy = "PosTableOrder")]` | Müşteri; customer.view, customer.manage | Medium |
| **Controllers/ReceiptsController.cs** | `[Authorize(Policy = "PosSales")]` (class) | Fişler; sale.view, payment.take | Medium |
| **Controllers/ReportsController.cs** | `[Authorize(Policy = "BackofficeManagement")]` | Raporlar; report.view, report.export | Medium |
| **Controllers/TseController.cs** | `[Authorize(Policy = "PosTse")]` (class) | TSE; shift.view, payment.take, settings.manage (tanılama) | Medium |
| **Controllers/TagesabschlussController.cs** | `[Authorize(Policy = "PosTse")]` (class) | Vardiya; shift.view, shift.close | Medium |
| **Controllers/LocalizationController.cs** | `[Authorize]`, `BackofficeSettings` (çoklu action) | Yerelleştirme; settings.view, settings.manage | Medium |
| **Controllers/SettingsController.cs** | `[Authorize]`, `BackofficeSettings` (çoklu action) | Sistem ayarları; settings.view, settings.manage | Medium |
| **Controllers/MultilingualReceiptController.cs** | `[Authorize]`, `BackofficeSettings` (çoklu action) | Fiş şablonları; receipttemplate.view, receipttemplate.manage | Medium |
| **Controllers/AdminUsersController.cs** | `[Authorize(Policy = "AdminUsers")]` (class) | Admin kullanıcılar; user.view, user.manage | Medium |
| **Controllers/UserManagementController.cs** | `UsersView`, `UsersManage` (çoklu action), `requiredPolicy = "UsersManage"` | Kullanıcı yönetimi; user.view, user.manage; SuperAdmin kontrolü RoleCanonicalization ile kalabilir | Medium |
| **Controllers/Base/EntityController.cs** | `[Authorize(Policy = "SystemCritical")]` (DeletePermanent) | Kalıcı silme; settings.manage veya özel permission | High |
| **Controllers/AuthController.cs** | `AddToRoleAsync(user, "Cashier")` (Register) | Rol ataması; sabit "Cashier" → Roles.Cashier kullanılabilir | Low |
| **Program.cs** | `RequireRole("SuperAdmin", "Admin", "Administrator", ...)` (tüm policy’ler) | Role policy tanımları; migration sonrası kaldırılacak veya deprecated; permission policy zaten AddPermissionPolicies ile ekleniyor | Phase 7 |
| **Auth/RoleCanonicalization.cs** | `Canonical.SuperAdmin`, `Canonical.Admin`, `BranchManager`, `Auditor` | Legacy rol eşlemesi; Administrator→Admin. Kalır; UserManagementController bu sabitleri kullanıyor | — |
| **Data/RoleSeedData.cs** | `"Auditor"`, `"BranchManager"`, `"SuperAdmin"` | Seed roller; Identity rolleri; değiştirme zorunlu değil | Low |
| **Data/UserSeedData.cs** | `Role = "SuperAdmin"`, `AddToRoleAsync(..., "SuperAdmin")`, `adminUser.Role != "SuperAdmin"` | Seed kullanıcı; Roles.SuperAdmin sabiti kullanılabilir | Low |
| **Services/UserService.cs** | `HasPermissionAsync(userId, permission)` | Zaten permission tabanlı; RolePermissionMatrix kullanıyor; kalır | — |
| **Services/IUserService.cs** | `HasPermissionAsync` | Arayüz; kalır | — |
| **KasseAPI_Final.Tests/UserManagementAuthorizationPolicyTests.cs** | `RequireRole("SuperAdmin", "Admin", "Administrator")` | Test policy set’i; migration’da policy isimleri veya permission test’e geçilebilir | Low |
| **KasseAPI_Final.Tests/UserManagementControllerUserLifecycleTests.cs** | `Role = "SuperAdmin"` (test user) | Test verisi; kalır veya Roles.SuperAdmin | Low |
| **KasseAPI_Final.Tests/RoleCanonicalizationTests.cs** | `Canonical.SuperAdmin` | Canonicalization test; kalır | — |

---

## 2) Yeni eklenecek / mevcut dosyalar

Aşağıdaki dosyalar **zaten projede mevcut**. Migration sırasında referans alınır; ek oluşturma gerekmez.

| Dosya | Durum |
|-------|--------|
| **Authorization/AppPermissions.cs** | Mevcut |
| **Authorization/PermissionCatalog.cs** | Mevcut |
| **Authorization/RolePermissionMatrix.cs** | Mevcut |
| **Authorization/HasPermissionAttribute.cs** | Mevcut |
| **Authorization/AuthorizationExtensions.cs** | Mevcut |
| **Authorization/RequirePermissionAttribute.cs** | Mevcut |
| **Authorization/PermissionRequirement.cs** | Mevcut |
| **Authorization/PermissionAuthorizationHandler.cs** | Mevcut |
| **Authorization/Roles.cs** | Mevcut |
| **Authorization/Permissions.cs** | Mevcut (eski sabitler; AppPermissions tercih edilebilir) |

**Öneri:** Controller’larda yeni yetki için `[HasPermission(AppPermissions.X)]` kullanın; policy isimleri Program.cs’teki role policy’ler migration’ın son fazında kaldırılacak.

---

## 3) Uygulanacak refactor sırası

1. **CartController.cs – inline role check (Critical)**  
   - `userRole != "Admin" && userRole != "Kasiyer"` → Permission kontrolü (örn. `sale.create` veya özel bir “cart.cleanup”); veya `[HasPermission(AppPermissions.SaleCreate)]` + action’da `_userService.HasPermissionAsync(userId, ...)`.  
   - "Kasiyer" typo kaldırılmalı; canonical rol `Roles.Cashier` veya permission kullanılmalı.

2. **Kritik finansal/sistem controller’ları (Faz 2)**  
   - PaymentController: cancel, refund, signature-debug, verify-signature → zaten bir kısmı HasPermission; eksik action’ları ekleyin.  
   - AuditLogController: export, cleanup (cleanup zaten HasPermission) → export için [HasPermission(AuditExport)].  
   - CompanySettingsController: tüm PUT → [HasPermission(SettingsManage)]; GET → [HasPermission(SettingsView)] veya [Authorize].  
   - FinanzOnlineController: class/action → FinanzOnlineManage, FinanzOnlineSubmit.  
   - InvoiceController: backfill → [HasPermission(SettingsManage)] veya benzeri.  
   - EntityController: DeletePermanent → [HasPermission(SettingsManage)] veya özel permission.

3. **OrdersController, InvoiceController (Faz 3)**  
   - Orders: class + tüm action’lar → order.view, order.create, order.update.  
   - Invoice: list/detail/export/create/update/delete/creditnote → ilgili permission’lar.

4. **POS: CartController, PaymentController (okuma), ReceiptsController, TableController, TseController, TagesabschlussController (Faz 4)**  
   - Cart: class + action’lar → sale.view, sale.create.  
   - Payment: GET’ler → payment.view; POST create → payment.take (zaten var).  
   - Receipts, Table, Tse, Tagesabschluss → matrise göre permission.

5. **Backoffice: Categories, AdminProducts, Product, ModifierGroups, Inventory, Reports, CashRegister (Faz 5)**  
   - Her controller’da class/action bazlı category.view, category.manage, product.view, product.manage, inventory.view, inventory.manage, report.view, report.export, cashregister.view, cashregister.manage.

6. **UserManagement, AdminUsers, Localization, Settings, MultilingualReceipt, Customer (Faz 6)**  
   - user.view, user.manage; settings.view, settings.manage; receipttemplate.view, receipttemplate.manage; customer.view, customer.manage.

7. **AuthController, Data/UserSeedData, Data/RoleSeedData (Low)**  
   - "Cashier", "SuperAdmin" string’leri → `Roles.Cashier`, `Roles.SuperAdmin` sabitleri.

8. **Program.cs (Faz 7)**  
   - Tüm controller’lar permission’a geçtikten sonra kullanılmayan role policy’leri kaldır veya deprecated işaretle.

9. **Test projesi**  
   - UserManagementAuthorizationPolicyTests: policy set’i veya permission tabanlı testlere uyarlama.  
   - Diğer testler: Role sabitleri (Roles.*) kullanımı.

Bu sıra, minimum kırılım ve kritik endpoint’lerin önce permission’a alınması hedefine göre düzenlenmiştir.
