# Role-Based → Permission-Based Authorization: Fazlı Migration Planı

**Tarih:** 2025-03-09  
**Hedef:** Minimum kırılım, mevcut endpoint’ler çalışmaya devam etsin; kritik endpoint’ler önce permission policy’ye geçsin; Admin/Administrator sadeleşsin; backoffice ve POS ayrımı netleşsin.

---

## Genel ilkeler

- **Paralel çalışma:** Mevcut role policy’ler (PosSales, AuditAdmin, BackofficeSettings vb.) kaldırılmadan yeni permission policy’ler eklenir; aynı endpoint’te ikisi birlikte kullanılabilir (AND), sonra role kaldırılır.
- **Administrator:** Identity’de kalır; token’da canonical “Admin”, permission set Admin ile aynı (RolePermissionMatrix). Yeni atamalarda rol adı “Admin” tercih edilir.
- **Backoffice vs POS:** `/api/admin/*` = backoffice; `/api/pos/*` ve `/api/Orders`, `/api/Table`, Cart, Payment = POS. Permission matrisi bu ayrımı yansıtır.

---

## Faz 1 – Altyapı ve token (zaten büyük oranda tamamlandı)

**Amaç:** Permission catalog, login’de permission claim üretimi ve policy kaydı hazır olsun; controller’larda henüz değişiklik yok.

**Yapılacak işler**

- [x] AppPermissions / PermissionCatalog / RolePermissionMatrix tanımlı ve güncel.
- [x] AddPermissionPolicies() ile tüm permission’lar için policy kaydı.
- [x] ITokenClaimsService: login’de role → permission set, JWT’ye `permission` claim’leri ve response’ta `user.permissions`.
- [x] GET /me response’ta `permissions` ve canonical `role`.
- [ ] Dokümantasyon: endpoint–permission matrisi ve migration planı paylaşılır; ekip bilgilendirilir.

**Risk:** Düşük. Sadece token ve response’a alan ekleniyor; mevcut role policy’ler aynen çalışır.

**Rollback:** TokenClaimsService’i devre dışı bırakıp eski GenerateJwtToken(user, role) kullanımına dönmek; login/me response’tan `permissions` alanını kaldırmak.

**Done criteria**

- Login ve GET /me cevaplarında `user.permissions` array’i dönüyor.
- JWT’de `permission` claim’leri var (ve PermissionAuthorizationHandler bunları okuyor).
- Tüm permission’lar için `Permission:{name}` policy’si kayıtlı.

---

## Faz 2 – Kritik finansal ve ayar endpoint’leri

**Amaç:** Ödeme iptal/iade, denetim silme, şirket ayarları ve FinanzOnline gibi kritik aksiyonlar permission policy ile korunsun.

**Yapılacak işler**

- Payment: `POST {id}/cancel` → `[HasPermission(AppPermissions.PaymentCancel)]` (class-level PosSales kalabilir veya kaldırılır).
- Payment: `POST {id}/refund` → `[HasPermission(AppPermissions.RefundCreate)]`.
- AuditLog: `DELETE cleanup` → `[HasPermission(AppPermissions.AuditCleanup)]` (mevcut AuditAdmin kaldırılabilir veya AND kalır).
- AuditLog: `GET export` → `[HasPermission(AppPermissions.AuditExport)]`.
- CompanySettings: Tüm PUT (ana, business-hours, banking, billing, localization) → `[HasPermission(AppPermissions.SettingsManage)]`; GET’ler → `[HasPermission(AppPermissions.SettingsView)]` (veya mevcut [Authorize] kalır).
- FinanzOnline: PUT config, GET config/status/errors/history → `[HasPermission(AppPermissions.FinanzOnlineManage)]`; POST submit-invoice → `[HasPermission(AppPermissions.FinanzOnlineSubmit)]`.
- Invoice: POST backfill-from-payments → `[HasPermission(AppPermissions.SettingsManage)]` veya ayrı bir “system” permission.
- EntityController (ve türevleri): DELETE permanent → `[HasPermission(AppPermissions.SettingsManage)]` veya benzeri.
- Payment: GET signature-debug, POST verify-signature → `[HasPermission(AppPermissions.SettingsManage)]`.

**Risk:** Orta. Bu endpoint’lere erişen roller (Admin, SuperAdmin, Administrator) zaten aynı yetkileri RolePermissionMatrix’te alıyor; yanlış konfigürasyonda 403 artışı olabilir.

**Rollback:** İlgili action’lardan `[HasPermission(...)]` kaldırıp sadece mevcut role policy’ye dönmek.

**Done criteria**

- Yukarıdaki tüm aksiyonlar en az bir permission policy ile korunuyor.
- Admin/Administrator ile bu endpoint’lere erişim başarılı; izinsiz rol 403 alıyor.
- E2E veya manuel test: payment cancel/refund, audit cleanup, CompanySettings PUT, FinanzOnline submit.

---

## Faz 3 – Sipariş durumu ve fatura (yüksek riskli yazma)

**Amaç:** Order status/cancel ve Invoice kritik yazma aksiyonları permission’a taşınsın.

**Yapılacak işler**

- Orders: PUT `{id}/status`, DELETE `{id}` → `[HasPermission(AppPermissions.OrderUpdate)]`.
- Orders: GET listesi/detay → `[HasPermission(AppPermissions.OrderView)]` (veya class-level policy kalır).
- Orders: POST (create) → `[HasPermission(AppPermissions.OrderCreate)]`.
- Invoice: POST, PUT, DELETE, duplicate → `[HasPermission(AppPermissions.InvoiceManage)]`; POST credit-note → `[HasPermission(AppPermissions.CreditNoteCreate)]`.
- Invoice: GET list/detail/export → `[HasPermission(AppPermissions.InvoiceView)]` / `[HasPermission(AppPermissions.InvoiceExport)]` (export ayrı).

**Risk:** Orta. POS ve backoffice’te sipariş/fatura akışı sık kullanılıyor; rol–permission eşlemesi matriste doğrulanmalı.

**Rollback:** Order ve Invoice controller’larda permission attribute’ları kaldırıp eski policy’lere dönmek.

**Done criteria**

- Sipariş durum güncelleme ve silme permission ile korunuyor.
- Fatura oluşturma, düzenleme, kredi notu permission ile korunuyor.
- Waiter/Cashier/Manager test kullanıcılarıyla akış test edildi.

---

## Faz 4 – POS: sepet, ödeme okuma, TSE, vardiya

**Amaç:** POS günlük akışı (sepet, ödeme alma, TSE, vardiya) permission policy ile netleşsin.

**Yapılacak işler**

- Cart (api/pos/cart, api/Cart): GET’ler → `sale.view`; POST/PUT/DELETE (sepet yazma, complete) → `sale.create`.
- Payment: POST (create payment) → zaten `payment.take` (Faz 2’de veya bu fazda); GET methods, GET {id}, receipt, statistics → `payment.view`.
- TseController: Tüm aksiyonlar → uygun permission (örn. TSE ile ilgili `payment.take` veya `shift.view`/shift açma); tanılama → `settings.manage`.
- Tagesabschluss: GET → `shift.view`; POST daily/monthly/yearly → `shift.close`.
- Receipts: GET, create-from-payment → `sale.view` / `payment.take` (matrise göre).
- Table: GET, POST status → `table.view` / `table.manage`.

**Risk:** Düşük–orta. POS client’lar (Expo vb.) aynı token ile çalışmaya devam eder; token’da permission’lar olduğu sürece davranış değişmez.

**Rollback:** Bu controller’larda sadece permission attribute’ları kaldırıp class-level role policy’ye bırakmak.

**Done criteria**

- Sepet, ödeme (okuma + alma), TSE, vardiya kapatma ilgili permission’larla korunuyor.
- POS client ile tam satış akışı (sepet → ödeme → fiş) test edildi.

---

## Faz 5 – Backoffice: katalog, envanter, raporlar

**Amaç:** Admin/catalog, Inventory ve Reports endpoint’leri permission’a geçsin; backoffice–POS ayrımı net kalsın.

**Yapılacak işler**

- CategoriesController (api/admin/categories): GET → `category.view`; POST, PUT, DELETE → `category.manage`.
- AdminProductsController, ProductController (admin/pos): GET → `product.view`; POST, PUT, DELETE, stock, modifier-groups → `product.manage`.
- ModifierGroupsController: GET → `product.view` veya `modifier.view`; POST, PUT, DELETE → `product.manage` / `modifier.manage`.
- InventoryController: GET → `inventory.view`; POST, PUT, restock, adjust → `inventory.manage`; DELETE → `inventory.manage` (veya ayrı delete permission matriste varsa).
- ReportsController: GET’ler → `report.view`; GET export → `report.export`.
- CashRegister: GET → `cashregister.view`; POST (create) → `cashregister.manage` veya `settings.manage`; open/close → `cashdrawer.open` / `shift.close`.

**Risk:** Düşük. Çoğu backoffice kullanıcısı Manager/Admin; matris bu roller için gerekli permission’ları veriyor.

**Rollback:** İlgili controller’larda permission attribute’larını kaldırıp eski CatalogManage, BackofficeManagement vb. policy’lere dönmek.

**Done criteria**

- Katalog (kategori, ürün, modifier), envanter, raporlar ve kasa endpoint’leri permission ile korunuyor.
- Backoffice UI ile liste/düzenleme/rapor akışı test edildi.

---

## Faz 6 – Kullanıcı yönetimi ve denetim okuma

**Amaç:** User/role yönetimi ve audit okuma endpoint’leri permission’a taşınsın.

**Yapılacak işler**

- AdminUsersController (api/admin/users): GET, GET {id}, activity → `user.view`; POST, PATCH, deactivate, reactivate, force-password-reset → `user.manage`.
- UserManagementController (api/UserManagement): GET, GetRoles → `user.view`; POST, PUT, deactivate, reactivate, reset-password, DELETE, PostRoles → `user.manage`.
- AuditLogController: GET (list), GET {id}, user, correlation, transaction, statistics, suspicious-admin-actions → `audit.view`; GET payment/{paymentId} → `audit.view` (Cashier kendi ödemesi için matriste tanımlı).
- LocalizationController: GET → `localization.view` veya `settings.view`; PUT, add/remove language/currency, export → `localization.manage` veya `settings.manage`.
- SettingsController (api/Settings): GET → `settings.view`; PUT, tax-rates, backup, notifications → `settings.manage`.
- MultilingualReceiptController: GET → `receipttemplate.view`; POST, PUT, DELETE, export → `receipttemplate.manage` veya `settings.manage`.
- CustomerController (ve diğer EntityController türevleri): GET/POST/PUT/DELETE → ilgili resource.view / resource.manage (matrise göre).

**Risk:** Düşük. Kullanıcı ve denetim ekranları çoğunlukla Admin/Manager; yeni roller (ReportViewer, Accountant) sadece view/export alır.

**Rollback:** Permission attribute’ları kaldırıp UsersView, UsersManage, AuditView, AuditAdmin vb. policy’lere geri dönmek.

**Done criteria**

- Tüm user/audit/settings/localization/receipttemplate endpoint’leri permission ile korunuyor.
- ReportViewer/Accountant ile sadece view/export erişimi doğrulandı.

---

## Faz 7 – Role policy sadeleştirme ve Administrator yol haritası

**Amaç:** Gereksiz role policy’leri kaldırmak; Administrator’ı yalnızca legacy alias olarak bırakıp yeni atamalarda Admin kullanımını standartlaştırmak.

**Yapılacak işler**

- Tüm controller’larda sadece permission policy kullanıldığını doğrula; class-level `[Authorize(Policy = "PosSales")]` gibi role policy’leri kaldır (veya tek bir “fallback” policy bırakıp dokümante et).
- Program.cs’te kullanılmayan policy tanımlarını kaldır veya “deprecated” olarak işaretle.
- Dokümantasyon: “Yeni kullanıcı atamasında rol adı olarak Admin kullanın; Administrator sadece mevcut hesaplar için.”
- İsteğe bağlı: Identity’de “Administrator” rolünü “Admin”e yönlendiren bir migration (rol adı değişikliği) planla; büyük kırılım istemiyorsanız sadece dokümante edin.

**Risk:** Orta. Role policy kaldırıldığında yanlış permission matrisi 403 artışına neden olabilir; önce Faz 2–6’nın tam test edilmesi gerekir.

**Rollback:** Program.cs’te role policy’leri geri eklemek; controller’lara tekrar `[Authorize(Policy = "X")]` eklemek.

**Done criteria**

- Hiçbir endpoint yalnızca role policy’ye dayanmıyor (hepsi permission veya permission + opsiyonel role).
- Kullanılmayan policy’ler temizlendi veya deprecated işaretlendi.
- Administrator kullanımı dokümante edildi.

---

## Faz 8 – İsteğe bağlı: DB tabanlı permission ve frontend

**Amaç:** İleride role–permission’ın veritabanından okunması ve frontend’de menü/route guard ile uyum.

**Yapılacak işler**

- (Opsiyonel) UserRole, RolePermission tabloları ve seed; RolePermissionMatrix yerine DB’den permission set okuma.
- Frontend (admin/POS): Login/me’den gelen `permissions` ile menü gizleme ve route guard (`hasPermission(permission)`).
- Backend: Gerekirse permission’ı DB’den okuyan bir servis; token claim’leri hâlâ login anında set edilebilir.

**Risk:** Orta–yüksek. DB şeması ve migration; frontend’de tüm korumalı ekranların permission’a bağlanması.

**Rollback:** RolePermissionMatrix’e geri dönmek; frontend’de role veya eski kontrolü kullanmak.

**Done criteria**

- (Eğer yapılırsa) Varsayılan permission setleri DB’de; token claim’leri bu setten üretiliyor.
- Frontend’de en azından kritik sayfalar permission ile guard’lanıyor.

---

## Özet tablo

| Faz | Odak | Risk | Kırılım |
|-----|------|------|---------|
| 1 | Altyapı, token, permission claim | Düşük | Yok |
| 2 | Kritik: payment cancel/refund, audit cleanup, CompanySettings, FinanzOnline, backfill, permanent delete | Orta | Yok (ek koruma) |
| 3 | Orders status/cancel, Invoice CRUD, credit note | Orta | Düşük |
| 4 | POS: Cart, Payment read, TSE, Tagesabschluss, Table | Düşük–orta | Düşük |
| 5 | Backoffice: Catalog, Inventory, Reports, CashRegister | Düşük | Düşük |
| 6 | User management, Audit read, Settings/Localization/ReceiptTemplate | Düşük | Düşük |
| 7 | Role policy kaldırma, Administrator dokümantasyonu | Orta | Teste bağlı |
| 8 | Opsiyonel: DB permissions, frontend guard | Orta–yüksek | Planlama gerekir |

---

## İlk sprint backlog (Faz 2 – kritik endpoint’ler)

Aşağıdaki endpoint’lerin ilk sprintte permission policy’ye geçirilmesi önerilir. Her satır için: mevcut policy’yi koruyup üzerine `[HasPermission(...)]` ekleyebilir veya sadece permission’a geçebilirsiniz.

| # | Controller / Route | Action / Method | Önerilen permission | Not |
|---|--------------------|-----------------|---------------------|-----|
| 1 | PaymentController | POST {id}/cancel | `AppPermissions.PaymentCancel` | Ödeme iptali |
| 2 | PaymentController | POST {id}/refund | `AppPermissions.RefundCreate` | İade |
| 3 | AuditLogController | DELETE cleanup | `AppPermissions.AuditCleanup` | Denetim silme |
| 4 | AuditLogController | GET export | `AppPermissions.AuditExport` | Denetim dışa aktarma |
| 5 | CompanySettingsController | PUT (tüm yazma) | `AppPermissions.SettingsManage` | Ana + business-hours, banking, billing, localization |
| 6 | CompanySettingsController | GET (tüm okuma) | `AppPermissions.SettingsView` | İsteğe bağlı |
| 7 | FinanzOnlineController | PUT config, GET config/status/errors/history | `AppPermissions.FinanzOnlineManage` | |
| 8 | FinanzOnlineController | POST submit-invoice | `AppPermissions.FinanzOnlineSubmit` | |
| 9 | FinanzOnlineController | POST test-connection | `AppPermissions.FinanzOnlineManage` | |
| 10 | InvoiceController | POST backfill-from-payments | `AppPermissions.SettingsManage` (veya özel) | |
| 11 | EntityController (türevleri) | DELETE {id}/permanent | `AppPermissions.SettingsManage` (veya özel) | Kalıcı silme |
| 12 | PaymentController | GET {id}/signature-debug | `AppPermissions.SettingsManage` | TSE tanılama |
| 13 | PaymentController | POST verify-signature | `AppPermissions.SettingsManage` | TSE doğrulama |

**Sprint çıktısı:** Yukarıdaki 13 madde uygulandıktan sonra kritik finansal ve sistem endpoint’leri permission tabanlı korunmuş olur; mevcut Admin/Administrator/SuperAdmin erişimi aynı kalır, rollback için sadece bu attribute’ların kaldırılması yeterlidir.
