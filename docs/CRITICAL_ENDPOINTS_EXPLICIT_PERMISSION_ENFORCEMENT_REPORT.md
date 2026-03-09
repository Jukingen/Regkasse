# Critical Endpoints – Explicit Permission Enforcement Report

## 1. Endpoint → permission mapping özeti

### SettingsController (api/settings)

| Endpoint | Method | Permission | Not |
|----------|--------|------------|-----|
| GET /api/settings | GET | settings.view | Ana ayarlar okuma |
| PUT /api/settings | PUT | settings.manage | Ana ayarlar güncelleme |
| GET /api/settings/tax-rates | GET | settings.view | Vergi oranları okuma |
| PUT /api/settings/tax-rates | PUT | settings.manage | Vergi oranları güncelleme |
| GET /api/settings/backup | GET | **settings.view** | Backup ayarları okuma (read = view) |
| POST /api/settings/backup/now | POST | settings.manage | Manuel backup tetikleme |
| GET /api/settings/notifications | GET | settings.view | Bildirim ayarları okuma |
| PUT /api/settings/notifications | PUT | settings.manage | Bildirim ayarları güncelleme |
| GET /api/settings/export | GET | settings.manage | Ayarları dışa aktarma (hassas) |

### CashRegisterController (api/cashregister)

| Endpoint | Method | Permission | Not |
|----------|--------|------------|-----|
| GET /api/cashregister | GET | cashregister.view | Liste |
| GET /api/cashregister/{id} | GET | cashregister.view | Tek kasa |
| POST /api/cashregister | POST | cashregister.manage | Kasa oluşturma |
| POST /api/cashregister/{id}/open | POST | shift.open | Kasa açılışı (vardiya) |
| POST /api/cashregister/{id}/close | POST | shift.close | Kasa kapanışı (vardiya) |
| GET /api/cashregister/{id}/transactions | GET | cashregister.view | İşlem listesi |

Fiziksel çekmece (cashdrawer) için ayrı endpoint varsa: cashdrawer.open / cashdrawer.close kullanılabilir; mevcut open/close vardiya anlamında shift.open / shift.close ile korunuyor.

### SystemCritical kullanan controller/action’lar

| Controller | Action | Permission |
|------------|--------|------------|
| InvoiceController | POST backfill-from-payments | system.critical |
| EntityController (base) | DELETE {id}/permanent (HardDelete) | system.critical |

Tümü `[HasPermission(AppPermissions.SystemCritical)]` ile; rol policy yok.

---

## 2. Yeni / güncellenen permission’lar

| Permission | Sabit | Açıklama |
|------------|--------|----------|
| shift.manage | AppPermissions.ShiftManage | Vardiya yönetimi (open/close kapsayıcı). Catalog ve matrix’e eklendi. |

Mevcut ve bu raporla değişmeyenler: settings.view, settings.manage, cashregister.view, cashregister.manage, cashdrawer.open, cashdrawer.close, shift.view, shift.open, shift.close, system.critical.

---

## 3. Matrix değişiklikleri

- **AppPermissions:** `ShiftManage = "shift.manage"` eklendi.
- **PermissionCatalog:** `ShiftManage` listeye eklendi.
- **RolePermissionMatrix:**
  - **Manager:** CashRegisterManage, ShiftManage eklendi (zaten ShiftOpen/ShiftClose vardı).
  - **Cashier:** ShiftManage eklendi (ShiftOpen/ShiftClose zaten vardı).
  - **Admin/SuperAdmin:** Tüm permissions (SuperAdmin = all; Admin = all except SystemCritical). ShiftManage catalog’da olduğu için Admin/SuperAdmin otomatik alıyor.
  - **Waiter:** Sadece ShiftClose; ShiftManage verilmedi (open yetkisi yok).
  - **Manager:** SettingsManage verilmedi (sadece SettingsView); ayar değiştirme Admin/SuperAdmin’de kaldı.

---

## 4. Riskli noktalar

| Nokta | Risk | Önlem |
|-------|------|--------|
| GET backup | Read ama hassas bilgi (LastBackup, NextBackup) | GetBackupSettings artık settings.view ile korunuyor; backup tetikleme settings.manage. |
| Export settings | Dosya indirme | settings.manage ile korunuyor; sadece ayar yönetimi olan roller erişir. |
| Backfill / HardDelete | Veri bütünlüğü | system.critical; sadece SuperAdmin (matrix’te Admin’de yok). |
| CashRegister open/close | Vardiya aç/kapa | shift.open / shift.close; fiziksel çekmece ayrı endpoint ise cashdrawer.open/close eklenmeli. |
| Class-level [Authorize] | Tek [Authorize] ile tüm controller | Kullanılmıyor; her action’da [HasPermission(...)] var. |

---

## 5. Yapılan kod değişiklikleri

- **AppPermissions.cs:** ShiftManage sabiti eklendi.
- **PermissionCatalog.cs:** ShiftManage All listesine eklendi.
- **RolePermissionMatrix.cs:** Manager ve Cashier’a ShiftManage + Manager’a CashRegisterManage eklendi.
- **SettingsController.cs:** GetBackupSettings için SettingsManage → SettingsView (read = view).
- **AuthorizationExtensions.cs:** AddLegacyRolePolicies açıklaması güncellendi; legacy role policy yok, koruma tamamen HasPermission.

SettingsController ve CashRegisterController’da zaten action bazında explicit permission vardı; sadece backup GET view’a çekildi ve ShiftManage tanımı/matrix uyumu eklendi.
