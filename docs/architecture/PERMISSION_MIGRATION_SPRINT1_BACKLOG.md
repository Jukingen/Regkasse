# Permission Migration – İlk Sprint Backlog

**Tarih:** 2025-03-09

**Sprint hedefi:** Sistemi bozmadan permission altyapısını başlatmak; en kritik 5–10 endpoint’i permission policy’ye geçirmek; legacy role fallback’i korumak.

**Bağlam:** POS + backoffice hibrit; hedef role + permission; Administrator legacy alias; minimum kırılım.

---

## 1) En kritik endpoint’ler (ilk sprint kapsamı)

| # | Route | Method | Permission | Risk |
|---|--------|--------|-------------|------|
| 1 | api/pos/payment, api/Payment | POST {id}/cancel | payment.cancel | Critical |
| 2 | api/pos/payment, api/Payment | POST {id}/refund | refund.create | Critical |
| 3 | api/Orders | PUT {id}/status | order.update | High |
| 4 | api/Orders | DELETE {id} | order.cancel | High |
| 5 | api/CompanySettings | PUT (main ve alt) | settings.manage | Critical |
| 6 | api/AuditLog | DELETE cleanup | audit.cleanup | Critical |
| 7 | api/AuditLog | GET export | audit.export | High |
| 8 | api/FinanzOnline | PUT config | finanzonline.manage | Critical |
| 9 | api/FinanzOnline | POST submit-invoice | finanzonline.submit | Critical |
| 10 | api/Invoice | POST backfill-from-payments | settings.manage | Critical |

Ek (sprint süresi yeterse): CartController ForceCleanup, InventoryController adjust, EntityController permanent delete.

---

## 2) Sprint backlog listesi

### Item 1: CartController ForceCleanup – inline rol kaldır, permission ekle

| Alan | İçerik |
|------|--------|
| **Title** | CartController ForceCleanup: hardcoded "Admin"/"Kasiyer" kaldır, [HasPermission] kullan |
| **Description** | ForceCleanup action’ında `userRole != "Admin" && userRole != "Kasiyer"` kontrolünü kaldır. Bunun yerine action’a `[HasPermission(AppPermissions.CartManage)]` ekle (veya mevcut class-level policy yanında). Böylece yetki RolePermissionMatrix ile uyumlu olur. |
| **Why** | Magic string ve typo ("Kasiyer"); permission tek kaynak olmalı. |
| **Output** | ForceCleanup için `[HasPermission(AppPermissions.CartManage)]`; inline role check kaldırıldı. |
| **Dependency** | AppPermissions.CartManage ve RolePermissionMatrix’te CartManage tanımlı (mevcut). |
| **Priority** | P1 (tek sert ihlal; diğer endpoint’lerden önce yapılabilir). |

---

### Item 2: UserService.HasPermissionAsync – RolePermissionMatrix kullan

| Alan | İçerik |
|------|--------|
| **Title** | UserService.HasPermissionAsync: hardcoded role switch kaldır, RolePermissionMatrix + GetRolesAsync kullan |
| **Description** | HasPermissionAsync içindeki `user.Role switch { "Admin" => ..., "Cashier" => ..., "Demo" => ... }` kaldırılacak. Kullanıcı için Identity rollerini UserManager.GetRolesAsync ile al; RolePermissionMatrix.GetPermissionsForRoles(roles) ile permission set’i çıkar; requirement.Permission bu sette mi kontrol et. |
| **Why** | Tek kaynak: permission seti RolePermissionMatrix’te; UserService aynı mantığı kullanmalı. |
| **Output** | HasPermissionAsync sadece GetRolesAsync + RolePermissionMatrix kullanıyor; role switch yok. |
| **Dependency** | UserManager inject; RolePermissionMatrix static. |
| **Priority** | P1. |

---

### Item 3: PaymentController – Cancel ve Refund’a HasPermission ekle

| Alan | İçerik |
|------|--------|
| **Title** | PaymentController: Cancel ve Refund action’larına [HasPermission] ekle |
| **Description** | POST {id}/cancel için `[HasPermission(AppPermissions.PaymentCancel)]`, POST {id}/refund için `[HasPermission(AppPermissions.RefundCreate)]` ekle. Mevcut class-level `[Authorize(Policy = "PosSales")]` kalır (legacy fallback). |
| **Why** | Ödeme iptali ve iade kritik; permission policy ile açık yetki. |
| **Output** | Cancel ve Refund action’larında HasPermission attribute; 403 cevabında gerekirse "Permission:payment.cancel" / "Permission:refund.create" bilgisi. |
| **Dependency** | AppPermissions, HasPermissionAttribute, RolePermissionMatrix (PaymentCancel, RefundCreate Cashier/Manager/Admin’de). |
| **Priority** | P1. |

---

### Item 4: OrdersController – DeleteOrder ve UpdateOrderStatus permission

| Alan | İçerik |
|------|--------|
| **Title** | OrdersController: DeleteOrder ve UpdateOrderStatus için permission attribute |
| **Description** | PUT {id}/status zaten `[HasPermission(AppPermissions.OrderUpdate)]` olabilir; yoksa ekle. DELETE {id} (DeleteOrder) için `[HasPermission(AppPermissions.OrderCancel)]` ekle. Class-level PosTableOrder kalır. |
| **Why** | Sipariş durum değişikliği ve iptal kritik. |
| **Output** | UpdateOrderStatus ve DeleteOrder’da ilgili HasPermission; OrderCancel Waiter/Manager/Cashier/Admin’de. |
| **Dependency** | AppPermissions.OrderUpdate, OrderCancel; RolePermissionMatrix. |
| **Priority** | P1. |

---

### Item 5: CompanySettingsController – PUT action’lara settings.manage

| Alan | İçerik |
|------|--------|
| **Title** | CompanySettingsController: Tüm PUT action'lara [HasPermission(AppPermissions.SettingsManage)] ekle |
| **Description** | Ana PUT ve alt uçlar (business-hours, banking, localization, billing) için action seviyesinde `[HasPermission(AppPermissions.SettingsManage)]` ekle. Mevcut BackofficeSettings policy kalır. |
| **Why** | Şirket ayarı değişikliği kritik; sadece Admin/SuperAdmin. |
| **Output** | Tüm CompanySettings PUT'larda HasPermission(SettingsManage). |
| **Dependency** | AppPermissions.SettingsManage; RolePermissionMatrix (sadece Admin/SuperAdmin). |
| **Priority** | P1. |

---

### Item 6: FinanzOnlineController – config ve submit permission

| Alan | İçerik |
|------|--------|
| **Title** | FinanzOnlineController: PUT config ve POST submit-invoice için HasPermission |
| **Description** | PUT config (ve varsa benzeri) için `[HasPermission(AppPermissions.FinanzOnlineManage)]`, POST submit-invoice için `[HasPermission(AppPermissions.FinanzOnlineSubmit)]` ekle. Class-level BackofficeSettings kalır. |
| **Why** | FinanzOnline konfigürasyonu ve fatura gönderimi kritik. |
| **Output** | Config ve submit action’larında ilgili HasPermission. |
| **Dependency** | AppPermissions.FinanzOnlineManage, FinanzOnlineSubmit. |
| **Priority** | P1. |

---

### Item 7: AuditLogController – export ve cleanup permission

| Alan | İçerik |
|------|--------|
| **Title** | AuditLogController: Export ve Cleanup için HasPermission |
| **Description** | GET export için `[HasPermission(AppPermissions.AuditExport)]`; DELETE cleanup zaten `[HasPermission(AppPermissions.AuditCleanup)]` ise doğrula, yoksa ekle. |
| **Why** | Denetim dışa aktarma ve silme kritik. |
| **Output** | Export ve Cleanup action’larında ilgili HasPermission. |
| **Dependency** | AppPermissions.AuditExport, AuditCleanup. |
| **Priority** | P1. |

---

### Item 8: InvoiceController – backfill-from-payments permission

| Alan | İçerik |
|------|--------|
| **Title** | InvoiceController: BackfillFromPayments için [HasPermission(SettingsManage)] |
| **Description** | POST backfill-from-payments için `[HasPermission(AppPermissions.SettingsManage)]` ekle (mevcut SystemCritical policy yanında veya policy yerine). |
| **Why** | Toplu backfill sistem kritik; sadece Admin. |
| **Output** | BackfillFromPayments action’ında HasPermission(SettingsManage). |
| **Dependency** | AppPermissions.SettingsManage. |
| **Priority** | P1. |

---

### Item 9: InventoryController – adjust endpoint permission

| Alan | İçerik |
|------|--------|
| **Title** | InventoryController: Adjust endpoint’ine [HasPermission(InventoryAdjust)] |
| **Description** | POST {id}/adjust (ve varsa benzeri) için `[HasPermission(AppPermissions.InventoryAdjust)]` ekle. RolePermissionMatrix’te InventoryAdjust sadece Admin/SuperAdmin’de. |
| **Why** | Stok miktar düzeltme kritik. |
| **Output** | Adjust action’ında HasPermission(InventoryAdjust). |
| **Dependency** | AppPermissions.InventoryAdjust. |
| **Priority** | P2. |

---

### Item 10: Dokümantasyon ve 403 mesajı kontrolü

| Alan | İçerik |
|------|--------|
| **Title** | Permission migration: dokümantasyon ve 403 requiredPolicy bilgisi |
| **Description** | AUTHORIZATION_REFACTOR_CODE_AUDIT veya ilgili doc’ta “ilk sprint tamamlandı” notu; ForbiddenResponseAuthorizationHandler’ın permission policy için requiredPolicy adını (örn. "Permission:payment.cancel") döndüğünü doğrula; gerekirse test ile 403 body’de requiredPolicy alanını assert et. |
| **Why** | Frontend/ops 403’te hangi permission’ın eksik olduğunu anlayabilsin. |
| **Output** | Güncel doc; 403 cevabında policy adı mevcut. |
| **Dependency** | Önceki item’lar. |
| **Priority** | P2. |

---

## 3) Teknik borç notları

- **user.role kolonu:** Hâlâ ApplicationUser’da; GetRolesAsync boşsa fallback olarak kullanılıyor. Migration sonrası tek kaynak AspNetUserRoles olacak; user.role deprecated/kaldırma ayrı sprint.
- **Legacy policy isimleri:** PosSales, BackofficeSettings, SystemCritical vb. kalacak; yeni endpoint’ler hem policy hem HasPermission ile korunuyor. İleride sadece permission’a indirgenebilir.
- **BranchManager / Auditor:** RoleSeedData ve bazı policy’lerde string olarak geçiyor; Roles.* sabitine taşınmadı. İsteğe bağlı temizlik.
- **Refresh token:** Auth/refresh henüz tam implemente değil; token yenileme de aynı claim (role + permission) üretmeli.
- **DB’den permission okuma:** Şu an in-memory RolePermissionMatrix; ileride permissions/role_permissions tablolarından okuma (DB migration planına göre) ayrı sprint.

---

## 4) Test ihtiyaçları

| Ne | Nasıl |
|----|--------|
| **Permission policy 403** | Cashier ile Payment cancel/Refund çağrısı → 200; izinsiz rol (ör. sadece Waiter) ile → 403. Response body’de requiredPolicy = "Permission:payment.cancel" (veya benzeri) olmalı. |
| **HasPermission + legacy policy** | Mevcut PosSales/BackofficeSettings rollerinde ilgili endpoint’ler 200; rol kaldırıldığında 403. |
| **UserService.HasPermissionAsync** | GetRolesAsync mock’la; RolePermissionMatrix’e uygun roller verildiğinde true, aksi halde false. |
| **CartController ForceCleanup** | CartManage yetkisi olan kullanıcı → 200; yetkisiz → 403. Inline "Admin"/"Kasiyer" kontrolü artık yok. |
| **Regression** | Mevcut AuthControllerTests, AdminUsersControllerTests, UserManagementControllerUserLifecycleTests yeşil kalmalı. |

Mümkünse en az bir integration test: login → token → kritik endpoint (örn. Payment cancel) çağrısı; yetkili rol 200, yetkisiz 403.

---

## 5) Deployment ve rollback notu

**Deploy:**  
- Sadece backend değişikliği; migration yok (DB şeması aynı).  
- Mevcut JWT ve role’ler aynı; PermissionAuthorizationHandler zaten role’den permission türetiyor.  
- Sıralama: deploy → smoke test (login, bir ödeme iptali, bir ayar sayfası).

**Rollback:**  
- Son commit geri alınır veya önceki deployment’a dönülür.  
- Eski kodda HasPermission olmayacak, sadece legacy policy kalacak; davranış önceki hale döner.  
- Veri değişikliği yok; kullanıcı/rol verisi aynı kalır.

**Risk:**  
- Yanlış permission atanırsa (örn. Waiter’a payment.cancel verilirse) matris hatası; RolePermissionMatrix ve testlerle önlenir.  
- 403’te requiredPolicy’nin doğru dönmesi frontend için faydalı; yanlış dönse bile yetki reddi doğru çalışır.

---

## 6) Done criteria (sprint tamamlandı sayılır)

- [ ] CartController ForceCleanup inline rol kaldırıldı; `[HasPermission(AppPermissions.CartManage)]` eklendi.
- [ ] UserService.HasPermissionAsync RolePermissionMatrix + GetRolesAsync kullanıyor; hardcoded role switch yok.
- [ ] PaymentController Cancel ve Refund action’larında `[HasPermission(PaymentCancel)]` ve `[HasPermission(RefundCreate)]` var.
- [ ] OrdersController UpdateOrderStatus ve DeleteOrder’da `[HasPermission(OrderUpdate)]` ve `[HasPermission(OrderCancel)]` var.
- [ ] CompanySettingsController tüm PUT’larda `[HasPermission(SettingsManage)]` var.
- [ ] FinanzOnlineController config ve submit-invoice’da `[HasPermission(FinanzOnlineManage)]` ve `[HasPermission(FinanzOnlineSubmit)]` var.
- [ ] AuditLogController export ve cleanup’ta `[HasPermission(AuditExport)]` ve `[HasPermission(AuditCleanup)]` var.
- [ ] InvoiceController BackfillFromPayments’ta `[HasPermission(SettingsManage)]` var.
- [ ] InventoryController adjust’ta `[HasPermission(InventoryAdjust)]` var (P2; sprint süresi yeterse).
- [ ] Mevcut testler yeşil; en az bir permission/403 testi (manuel veya otomatik) yapıldı.
- [ ] Kısa deployment/rollback notu doc’a işlendi (bu dosya veya REFACTOR_CODE_AUDIT güncellendi).
