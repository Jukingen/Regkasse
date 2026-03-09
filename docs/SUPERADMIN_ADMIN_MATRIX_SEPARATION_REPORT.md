# SuperAdmin vs Admin – Role–Permission Matrix Separation Report

## 1. Yeni SuperAdmin / Admin fark tablosu

| Permission | SuperAdmin | Admin | Açıklama |
|------------|------------|--------|----------|
| **system.critical** | ✅ | ❌ | Kalıcı silme / yüksek riskli sistem aksiyonları (InvoiceController, EntityController). |
| **tse.diagnostics** | ✅ | ❌ | TSE tanılama/debug endpoint’leri (PaymentController). |
| **audit.cleanup** | ✅ | ❌ | Audit log temizleme/silme (AuditLogController). |
| **inventory.delete** | ✅ | ❌ | Envanter kaydı kalıcı silme (InventoryController). |
| **tse.sign** | ✅ | ✅ | Fiskal imza (POS/günlük kapanış); Admin backoffice’te işlem yapabiliyor. |
| **audit.export** | ✅ | ✅ | Audit dışa aktarma; uyumluluk için Admin’de kaldı. |
| **settings.manage** | ✅ | ✅ | Backoffice ayar yönetimi. |
| **user.manage** | ✅ | ✅ | Kullanıcı yönetimi. |
| **cashregister.manage** | ✅ | ✅ | Kasa/kasa yönetimi. |
| **Diğer tüm permission’lar** | ✅ | ✅ | Admin, yukarıdaki 4 hariç hepsine sahip. |

**Özet:** Admin = “all minus SuperAdmin-only”. SuperAdmin-only set: `system.critical`, `tse.diagnostics`, `audit.cleanup`, `inventory.delete`.

---

## 2. Etkilenen permission’lar

| Permission | Değişiklik | Etkilenen endpoint(ler) |
|------------|------------|--------------------------|
| **system.critical** | Değişmedi (zaten SuperAdmin-only) | InvoiceController (permanent delete vb.), EntityController (system-critical action). |
| **tse.diagnostics** | Admin’den **kaldırıldı** | PaymentController: TSE diagnostics ile ilgili GET/POST. |
| **audit.cleanup** | Admin’den **kaldırıldı** | AuditLogController: cleanup/purge aksiyonu. |
| **inventory.delete** | Admin’den **kaldırıldı** | InventoryController: DELETE envanter kaydı. |

PermissionCatalog ve AppPermissions değişmedi; sadece RolePermissionMatrix içinde Admin seti bu dört permission’ı artık içermiyor.

---

## 3. Etkilenen endpoint’ler ve allow/deny

| Controller | Action / Endpoint | Gerekli permission | Admin önceki | Admin yeni |
|------------|-------------------|---------------------|--------------|------------|
| **InventoryController** | DELETE (envanter silme) | inventory.delete | ✅ | ❌ |
| **AuditLogController** | Cleanup/purge | audit.cleanup | ✅ | ❌ |
| **PaymentController** | TSE diagnostics | tse.diagnostics | ✅ | ❌ |
| **InvoiceController** | System-critical (kalıcı silme vb.) | system.critical | ❌ | ❌ |
| **EntityController** | System-critical action | system.critical | ❌ | ❌ |

Admin rolüyle bu endpoint’lere yapılan istekler (inventory delete, audit cleanup, TSE diagnostics) artık **403 Forbidden** döner. SuperAdmin aynı aksiyonlara erişmeye devam eder.

---

## 4. Risk değerlendirmesi

| Risk | Seviye | Açıklama / Mitigasyon |
|------|--------|------------------------|
| **Admin envanter silemez** | Orta | Admin hâlâ inventory.view, inventory.manage, inventory.adjust ile görüntüleme/düzenleme/ayarlama yapabilir; sadece kalıcı silme (DELETE) SuperAdmin’e kısıtlandı. İş akışı gerekiyorsa silme işlemi SuperAdmin ile yapılır. |
| **Admin audit temizleyemez** | Düşük | Audit cleanup nadiren kullanılır; uyumluluk/saklama süresi için genelde SuperAdmin tercih edilir. Audit export Admin’de kaldı. |
| **Admin TSE diagnostics göremez** | Düşük | TSE diagnostics teknik/debug amaçlı; operasyonel POS akışı tse.sign ile çalışır, Admin tse.sign’e sahip. |
| **Mevcut Admin kullanıcılar** | Orta | Daha önce bu 4 aksiyonu (envanter silme, audit cleanup, TSE diagnostics) kullanan Admin hesaplar artık 403 alır. Bilgilendirme / runbook’ta SuperAdmin kullanımı belirtilmeli. |
| **UI’da buton/akış** | Düşük | Frontend’de “Envanter sil”, “Audit temizle”, “TSE diagnostics” gibi aksiyonlar Admin’e gösterilip tıklanınca 403 alınabilir; mümkünse bu aksiyonlar permission’a göre gizlenir veya devre dışı bırakılır. |

Genel risk: **Orta**. İş akışı gereksiz kırılmıyor; sadece en hassas aksiyonlar SuperAdmin’e toplandı. Fazla agresif kısıtlama yapılmadı.

---

## 5. Test gereksinimleri

- **RolePermissionMatrixTests**
  - `RoleHasPermission_Admin_Has_InventoryDelete` → **kaldırıldı**; yerine `RoleHasPermission_Admin_DoesNotHave_InventoryDelete` (Admin’in inventory.delete’i yok).
  - `RoleHasPermission_Admin_DoesNotHave_TseDiagnostics`, `RoleHasPermission_Admin_DoesNotHave_AuditCleanup` eklendi.
  - `RoleHasPermission_SuperAdmin_Has_TseDiagnostics`, `RoleHasPermission_SuperAdmin_Has_AuditCleanup`, `RoleHasPermission_SuperAdmin_Has_InventoryDelete` eklendi.
- **İsteğe bağlı integration testler**
  - Admin token ile `DELETE /api/Inventory/{id}` → 403.
  - Admin token ile audit cleanup endpoint’i → 403.
  - Admin token ile TSE diagnostics endpoint’i → 403.
  - SuperAdmin token ile aynı endpoint’ler → 200 (veya beklenen başarı kodu).
- **Manuel / QA**
  - Admin ile giriş: envanter silme, audit cleanup, TSE diagnostics arayüzü/akışı 403 veya gizli olmalı.
  - SuperAdmin ile aynı aksiyonlar çalışmalı.
