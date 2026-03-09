# POS Feature–Role Matrix and Implementation Plan

**Tarih:** 2025-03-09  
**Amaç:** Verilen feature setine göre role matrisi, permission listesi, backend/frontend eşlemesi ve kademeli geçiş planı.

**Feature set:** Users, Products, Categories, Reports, Sales, Refund, Price override, Table orders, Shift operations, Cash drawer, Settings.

**Hedef roller:** SuperAdmin, Admin, Manager, Cashier, Waiter.

---

## 1) Role–Feature matrix

**✅** = Rol bu feature’a erişebilir (tanımlı yetki seviyesinde). **❌** = Erişim yok.

| Feature | SuperAdmin | Admin | Manager | Cashier | Waiter |
|---------|:----------:|:-----:|:-------:|:-------:|:------:|
| **Users** | ✅ full | ✅ full | ✅ view* | ❌ | ❌ |
| **Products** | ✅ full | ✅ full | ✅ full | ✅ view | ✅ view |
| **Categories** | ✅ full | ✅ full | ✅ full | ✅ view | ✅ view |
| **Reports** | ✅ full | ✅ full | ✅ full | ✅ view | ❌ |
| **Sales** | ✅ full | ✅ full | ✅ full | ✅ full | ✅ full |
| **Refund** | ✅ full | ✅ full | ✅ full | ✅ full | ❌ |
| **Price override** | ✅ full | ✅ full | ✅ full | ✅ full | ✅ limited** |
| **Table orders** | ✅ full | ✅ full | ✅ full | ✅ full | ✅ full |
| **Shift operations** | ✅ full | ✅ full | ✅ full | ✅ full | ✅ close only*** |
| **Cash drawer** | ✅ full | ✅ full | ✅ full | ✅ open | ❌ |
| **Settings** | ✅ full | ✅ full | ❌ | ❌ | ❌ |

- **\*** Manager: user list/view ve audit görüntüleme; create/update/deactivate/role atama Admin (veya policy ile ayrılabilir).
- **\*\*** Price override: Waiter sadece masa siparişlerinde indirim uygulayabilir; genel fiyat değişikliği Cashier/Manager+.
- **\*\*\*** Shift: Waiter sadece vardiya kapatma (gün sonu) yapabilir; vardiya açma Cashier/Manager+.

---

## 2) Permission list (feature → permissions)

Format: `resource.action`. Her feature için hangi permission’ların gerekli olduğu.

| Feature | Permissions | Açıklama |
|---------|-------------|----------|
| **Users** | user.view, user.manage | view: liste/detay/activity; manage: create, update, deactivate, reactivate, role, reset-password |
| **Products** | product.view, product.manage | view: GET products/categories/modifiers; manage: CRUD products, modifier groups |
| **Categories** | category.view, category.manage | view: GET categories; manage: POST/PUT/DELETE categories |
| **Reports** | report.view, report.export | view: rapor ekranları; export: CSV/PDF indirme |
| **Sales** | sale.create, sale.view, payment.take | create: sepet→ödeme→fiş; view: fiş listesi/detay; take: ödeme alma, TSE imza |
| **Refund** | refund.create | İade işlemi oluşturma |
| **Price override** | price.override | Satır/fiş indirimi veya fiyat değiştirme (POS’ta) |
| **Table orders** | order.create, order.update, order.view, table.view, table.manage | Sipariş ve masa akışı |
| **Shift operations** | shift.open, shift.close | Vardiya açma / gün sonu kapatma |
| **Cash drawer** | cashdrawer.open | Kasa çekmecesi açma |
| **Settings** | settings.manage | Sistem, şirket, vergi, yerelleştirme, fiş şablonu, kasa config, FinanzOnline |

**Ek (mevcut dokümanlarla uyumlu):**

| Ek permission | Kullanım |
|---------------|---------|
| customer.view, customer.manage | Müşteri (Table orders / Sales ile birlikte) |
| audit.view | Denetim kayıtları (Reports/Users ile) |
| receipt.reprint, receipt.void | Fiş yeniden yazdırma / iptal (admin) |
| inventory.view, inventory.manage | Stok (Products/Categories yanında) |

**Tüm permission listesi (alfabetik):**

```
audit.view
cashdrawer.open
category.manage
category.view
customer.manage
customer.view
order.create
order.update
order.view
payment.take
price.override
product.manage
product.view
receipt.reprint
receipt.void
refund.create
report.export
report.view
sale.create
sale.view
settings.manage
shift.close
shift.open
table.manage
table.view
user.manage
user.view
```

*(inventory.view, inventory.manage eklenebilir; bu dokümanda Products/Categories ile gruplanabilir.)*

---

## 3) Endpoint / UI action → permission mapping

### 3.1 Backend endpoint → permission

| Endpoint / Controller | Action | Gerekli permission |
|------------------------|--------|---------------------|
| **Users** | | |
| GET /api/UserManagement, GET /api/UserManagement/{id}, GET roles, GET user activity | List, detail, roles, activity | user.view |
| POST /api/UserManagement, PUT, deactivate, reactivate, reset-password, POST roles | Create, update, lifecycle, role | user.manage |
| **Products** | | |
| GET /api/Product, GET categories, GET modifier-groups (read) | Read catalog | product.view, category.view |
| POST/PUT/DELETE categories, AdminProducts, ModifierGroups (write) | Catalog write | product.manage, category.manage |
| **Categories** | | |
| GET /api/admin/categories | List categories | category.view (veya product.view) |
| POST/PUT/DELETE /api/admin/categories | CRUD categories | category.manage |
| **Reports** | | |
| GET /api/Reports/* | View reports | report.view |
| GET export, CSV/PDF | Export | report.export |
| **Sales** | | |
| CartController (add, update, submit) | Cart operations | sale.create |
| PaymentController (process, get receipt, TSE sign) | Payment, receipt | payment.take, sale.view |
| ReceiptsController (list, detail) | Receipt list/detail | sale.view |
| **Refund** | | |
| POST refund / refund endpoint | Create refund | refund.create |
| **Price override** | | |
| Cart/Order discount, line price override | Apply discount or override price | price.override |
| **Table orders** | | |
| TableController (GET, PUT status) | Table list, status | table.view, table.manage |
| OrdersController (GET, POST, PUT) | Order list, create, update | order.view, order.create, order.update |
| CustomerController (GET, POST, PUT) | Customer read/write | customer.view, customer.manage |
| **Shift operations** | | |
| TagesabschlussController (open shift) | Open shift | shift.open |
| TagesabschlussController (close shift) | Close shift / gün sonu | shift.close |
| TseController | TSE operations (imza vb.) | payment.take / shift açısından ilgili |
| **Cash drawer** | | |
| CashRegisterController POST (open drawer) veya ayrı open endpoint | Open drawer | cashdrawer.open |
| CashRegisterController GET, config | View / config | settings.manage (config), report.view (özet) |
| **Settings** | | |
| SettingsController, CompanySettingsController, LocalizationController, MultilingualReceiptController, FinanzOnlineController | All write/export | settings.manage |
| CashRegisterController POST (create register) | Register create | settings.manage veya cashregister.manage |

### 3.2 UI action → permission (özet)

| Ekran / Aksiyon | Gerekli permission |
|------------------|--------------------|
| Users sayfası (liste, detay, activity) | user.view |
| Kullanıcı oluştur / düzenle / devre dışı / rol atama | user.manage |
| Ürün/kategori listesi (POS veya admin) | product.view, category.view |
| Ürün/kategori ekleme/düzenleme/silme (admin) | product.manage, category.manage |
| Raporlar sayfası, grafikler | report.view |
| Rapor dışa aktar | report.export |
| POS: Sepet, ödeme, fiş | sale.create, sale.view, payment.take |
| POS: İade butonu | refund.create |
| POS: İndirim / fiyat değiştir | price.override |
| POS: Masa seçimi, sipariş oluştur/güncelle | table.view, table.manage, order.create, order.update, order.view |
| POS: Vardiya aç | shift.open |
| POS: Vardiya kapat / gün sonu | shift.close |
| POS: Kasa çekmecesi aç | cashdrawer.open |
| Ayarlar menüsü (sistem, şirket, dil, fiş, FinanzOnline) | settings.manage |

---

## 4) Backend policy mapping

Mevcut policy’ler rol seti ile tanımlı. Permission tabanlı modele geçildiğinde her policy tek bir permission (veya birleşik kural) ile eşleşecek. **Şu anki rol tabanlı policy’ler** aşağıdaki feature/permission’larla **kavramsal** eşleşiyor:

| Policy (mevcut) | İlgili feature(s) | İlgili permission(s) |
|-----------------|-------------------|----------------------|
| AdminUsers | Users | user.manage (strict) |
| UsersView | Users | user.view |
| UsersManage | Users | user.manage |
| BackofficeManagement | Reports, Users (view), Products/Categories (manage), Inventory | report.view, report.export, user.view, product.manage, category.manage, inventory.* |
| BackofficeSettings | Settings | settings.manage |
| PosSales | Sales, Refund, Price override (kısmen), Cash drawer (open) | sale.create, sale.view, payment.take, refund.create, price.override, cashdrawer.open |
| PosTableOrder | Table orders | order.create, order.update, order.view, table.view, table.manage, customer.view, customer.manage |
| PosCatalogRead | Products, Categories (read) | product.view, category.view |
| CatalogManage | Products, Categories (write) | product.manage, category.manage |
| InventoryManage / InventoryDelete | (Products stok) | inventory.manage |
| AuditView / AuditViewWithCashier / AuditAdmin | (Reports / denetim) | audit.view |
| PosTse | Shift operations, Sales (TSE) | shift.open, shift.close, payment.take |
| PosTseDiagnostics | Settings (TSE debug) | settings.manage |
| SystemCritical | Settings (backfill vb.) | settings.manage |
| CashRegisterManage | Settings (kasa kaydı) | settings.manage |

**Permission tabanlı policy önerisi (gelecek adım):**

- Her permission için bir policy: `Permission:user.view`, `Permission:report.view`, …  
- Veya feature bazlı bileşik policy: `Feature:Users` → RequirePermission(user.view) OR RequirePermission(user.manage) (action’a göre endpoint’te hangisi gerekiyorsa o kullanılır).
- Controller’da: `[Authorize(Policy = "Permission:report.view")]` veya `[RequirePermission("report.view")]` (custom attribute + handler).

---

## 5) Frontend visibility mapping

### 5.1 Menü / route görünürlüğü (hangi rol ne görür)

| Menü / Sayfa | SuperAdmin | Admin | Manager | Cashier | Waiter |
|--------------|:----------:|:-----:|:-------:|:-------:|:------:|
| **FE-Admin** | | | | | |
| Users | ✅ | ✅ | ✅ (view) | ❌ | ❌ |
| Products / Categories (admin) | ✅ | ✅ | ✅ | ❌ | ❌ |
| Reports | ✅ | ✅ | ✅ | ❌ | ❌ |
| Audit log | ✅ | ✅ | ✅ | ❌ | ❌ |
| Settings | ✅ | ✅ | ❌ | ❌ | ❌ |
| **POS (Expo)** | | | | | |
| Sales / Cart / Payment | ✅ | ✅ | ✅ | ✅ | ✅ |
| Receipts | ✅ | ✅ | ✅ | ✅ | ✅ |
| Refund | ✅ | ✅ | ✅ | ✅ | ❌ |
| Price override / Discount | ✅ | ✅ | ✅ | ✅ | ✅* |
| Table / Orders | ✅ | ✅ | ✅ | ✅ | ✅ |
| Shift open/close | ✅ | ✅ | ✅ | ✅ | close only |
| Cash drawer | ✅ | ✅ | ✅ | ✅ | ❌ |
| Reports (POS özet) | ✅ | ✅ | ✅ | ✅ | ❌ |
| Product/Category (read) | ✅ | ✅ | ✅ | ✅ | ✅ |

\* Waiter: sadece indirim (price.override limited); tam fiyat değişikliği yok.

### 5.2 Guard / visibility önerisi

- **Backend’den permission listesi al:** Login sonrası `GET /api/me` veya JWT claim ile `permissions: string[]` dönsün.
- **Tek kaynak:** Menü ve buton görünürlüğü bu listeye göre:
  - `hasPermission('user.view')` → Users menüsü (view).
  - `hasPermission('user.manage')` → Create user, deactivate, role atama butonları.
  - `hasPermission('report.view')` → Reports menüsü.
  - `hasPermission('settings.manage')` → Settings menüsü.
  - `hasPermission('refund.create')` → İade butonu (POS).
  - `hasPermission('cashdrawer.open')` → Kasa çekmecesi butonu.
  - `hasPermission('shift.open')` → Vardiya aç; `hasPermission('shift.close')` → Vardiya kapat.
- **Route guard:**  
  - FE-Admin: `/users` → user.view; `/settings` → settings.manage; `/reports` → report.view.  
  - POS: Her ekran için gerekli permission (örn. SalesScreen → sale.create, payment.take).
- **Fallback:** Permission listesi yoksa (eski token) role’e göre statik matris kullan (mevcut PermissionHelper gibi).

---

## 6) Migration steps (kademeli geçiş)

### Adım 1 – Doküman ve sabitler (kırılım yok)

1. Bu dokümandaki **permission listesini** backend’de sabit sınıf olarak tanımla (`Authorization/Permissions.cs` veya `Constants/Permissions.cs`).
2. Frontend’de (admin + POS) aynı permission string’lerini sabit veya enum olarak tut; backend ile bire bir aynı olsun.
3. **Role–permission matrisini** (bu dokümandaki feature→role + permission→role) tek yerde dokümante et veya backend’de statik dictionary olarak tut (`RolePermissionMatrix`).

### Adım 2 – Backend: Permission requirement + handler

4. `PermissionRequirement(string permission)` ve `PermissionAuthorizationHandler` ekle: kullanıcının rollerini al, matristen permission setini çıkar, istenen permission varsa succeed.
5. SuperAdmin için: tüm permission’ları otomatik ver (matriste “*” veya özel kural).
6. `Program.cs`’te her permission için bir policy kaydet:  
   `options.AddPolicy("Permission:report.view", p => p.Requirements.Add(new PermissionRequirement("report.view")));`  
   Veya tek generic policy + endpoint metadata’dan permission okuyan filter.

### Adım 3 – Controller’ları permission policy’ye bağla (kademeli)

7. **Önce tek modül:** Örn. Reports. `[Authorize(Policy = "BackofficeManagement")]` → `[Authorize(Policy = "Permission:report.view")]` (ve export için `Permission:report.export`). Test et.
8. **Sonra diğer modüller:** Users → user.view / user.manage; Settings → settings.manage; Sales → sale.create, sale.view, payment.take; Table orders → order.*, table.*; Shift → shift.open, shift.close; Cash drawer → cashdrawer.open; Products/Categories → product.view, category.view, product.manage, category.manage.
9. Eski policy isimlerini **alias** olarak tutmak istersen: `BackofficeManagement` policy’si “report.view VEYA report.export VEYA …” gibi birden fazla permission’ı require eden composite yapıya çevrilebilir (isteğe bağlı).

### Adım 4 – Frontend: Permission bilgisi ve guard

10. Backend’de `GET /api/me` (veya `/api/user/permissions`) cevabına `permissions: string[]` ekle; login/refresh’te bu listeyi döndür.
11. FE-Admin ve POS’ta `usePermissions()` veya `hasPermission(name)` hook’unu bu listeye bağla. Eski token’lar için fallback: role’den statik matris ile permission türet.
12. Menü ve route guard’ları permission’a göre güncelle (yukarıdaki tabloya göre). Buton visibility’yi de aynı permission’lara bağla.

### Adım 5 – İsteğe bağlı: Veritabanı

13. İleride rol–permission ilişkisini DB’den yönetmek istersen: `Permissions` ve `RolePermissions` tabloları + seed; handler’ın matrisi DB’den okuması. Migration adımları 1–4 aynen kalır; sadece matris kaynağı değişir.

---

## 7) Özet tablolar

### Role–feature matrix (özet)

| Feature | SuperAdmin | Admin | Manager | Cashier | Waiter |
|---------|:----------:|:-----:|:-------:|:-------:|:------:|
| Users | ✅ | ✅ | view | ❌ | ❌ |
| Products | ✅ | ✅ | ✅ | view | view |
| Categories | ✅ | ✅ | ✅ | view | view |
| Reports | ✅ | ✅ | ✅ | view | ❌ |
| Sales | ✅ | ✅ | ✅ | ✅ | ✅ |
| Refund | ✅ | ✅ | ✅ | ✅ | ❌ |
| Price override | ✅ | ✅ | ✅ | ✅ | limited |
| Table orders | ✅ | ✅ | ✅ | ✅ | ✅ |
| Shift operations | ✅ | ✅ | ✅ | ✅ | close only |
| Cash drawer | ✅ | ✅ | ✅ | ✅ | ❌ |
| Settings | ✅ | ✅ | ❌ | ❌ | ❌ |

### Backend policy mapping (mevcut → feature)

| Mevcut policy | Feature(s) |
|---------------|------------|
| AdminUsers, UsersView, UsersManage | Users |
| BackofficeManagement | Reports, Products, Categories, Inventory (view/manage) |
| BackofficeSettings | Settings |
| PosSales | Sales, Refund, Price override, Cash drawer |
| PosTableOrder | Table orders |
| PosCatalogRead | Products, Categories (read) |
| CatalogManage | Products, Categories (write) |
| PosTse | Shift operations, Sales (TSE) |
| CashRegisterManage | Settings (kasa kaydı) |

### Frontend visibility (permission → görünürlük)

| Permission | FE-Admin | POS |
|------------|----------|-----|
| user.view | Users menü, liste, detay | – |
| user.manage | Create, edit, deactivate, role | – |
| report.view | Reports menü | Rapor özeti (varsa) |
| report.export | Export butonları | – |
| settings.manage | Settings menü | – |
| sale.create, sale.view, payment.take | – | Sales, Cart, Payment, Receipts |
| refund.create | – | İade butonu |
| price.override | – | İndirim/fiyat butonu |
| order.*, table.* | – | Table, Orders ekranları |
| shift.open, shift.close | – | Vardiya aç/kapat |
| cashdrawer.open | – | Kasa çekmecesi butonu |
| product.view, category.view | Katalog (read) | Ürün/kategori listesi |

Bu doküman, mevcut policy tabanlı refactor ile uyumludur; permission tabanlı modele geçerken tek kaynak olarak kullanılabilir.
