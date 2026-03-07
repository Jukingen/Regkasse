# Admin: Legacy Modifier & Bulk Migration Kaldırma Risk Analizi

**Tarih:** 2025-03-07  
**Kapsam:** Yalnızca analiz; kod değişikliği yok.  
**Hedef:** Admin’de legacy modifier ve bulk migration akışının kaldırılması riskinin değerlendirilmesi.

---

## 1) Bağımlı Ekranlar ve API’ler (Frontend + Backend, dosya/satır)

### 1.1 Sadece bulk migration + progress’e bağlı (kaldırılırsa sadece bu kısım etkilenir)

| Konum | Dosya | Satır(lar) | Açıklama |
|--------|------|------------|----------|
| **Frontend – API client** | `frontend-admin/src/lib/api/legacyModifierMigration.ts` | 1–115 | Tüm dosya: `getMigrationProgress()`, `runBulkMigration()`, DTO’lar. GET `/api/admin/migration-progress`, POST `/api/admin/migrate-legacy-modifiers`. |
| **Frontend – sayfa** | `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | 24 | Import: `getMigrationProgress`, `runBulkMigration`, `BulkMigrationResultDto`. |
| | | 30 | `migrationProgressKey`. |
| | | 50–54 | State: `bulkModalOpen`, `bulkLoading`, `bulkResult`, `bulkConfirm`, `bulkForm`. |
| | | 55–59 | `useQuery` migration progress (`getMigrationProgress`). |
| | | 139–167 | `handleBulkMigration` (içinde `runBulkMigration`). |
| | | 168–174 | `closeBulkModal` (refetch migration progress). |
| | | 369–393 | Card: “Aktive Legacy-Modifier”, “Gruppen nur mit Legacy-Modifiern” istatistikleri + “Bulk-Migration ausführen” butonu. |
| | | 539–608 | Bulk migration modal (form, dry run, confirmation, result). |
| **Backend – controller** | `backend/Controllers/AdminMigrationController.cs` | 30–42 | `GetMigrationProgress` → GET `api/admin/migration-progress`. |
| | | 50–77 | `MigrateLegacyModifiers` → POST `api/admin/migrate-legacy-modifiers`. |
| **Backend – service** | `backend/Services/IModifierMigrationService.cs` | 45 | `GetMigrationProgressAsync`. |
| **Backend – service** | `backend/Services/ModifierMigrationService.cs` | 11 (comment), 286–~310 | `GetMigrationProgressAsync` implementasyonu; batch `MigrateAsync` (progress değil, bulk action). |
| **Backend – DTO** | `backend/DTOs/ModifierMigrationDTOs.cs` | 78+ | `LegacyModifierMigrationProgressDto`. |
| **Backend – CLI** | `backend/Program.cs` | 240–274 | `migrate-legacy-modifiers` CLI: args, `MigrateAsync` çağrısı. |
| **Backend – test** | `backend/KasseAPI_Final.Tests/ModifierMigrationServiceTests.cs` | 603–657+ | `GetMigrationProgress_*` testleri; batch `MigrateAsync_*` testleri. |
| **Swagger** | `backend/swagger.json` | 9, 21 | Paths: `/api/admin/migration-progress`, `/api/admin/migrate-legacy-modifiers`. |

**Özet:** Bulk + progress akışı yalnızca **tek ekrana** bağlı: **Add-on-Gruppen (modifier-groups)**. Başka admin sayfası veya POS bu endpoint’leri kullanmıyor.

---

### 1.2 “Legacy modifier” genel (tek tek “Als Produkt migrieren” + UI gösterimi)

Bulk’tan bağımsız; sadece “legacy modifier” UI’ı ve tekli migration da kaldırılacaksa aşağıdakiler de etkilenir.

| Konum | Dosya | Satır(lar) | Açıklama |
|--------|------|------------|----------|
| **Frontend – API** | `frontend-admin/src/lib/api/modifierGroups.ts` | 15, 48, 91–100, 126–152 | Legacy modifier yorumları; `addLegacyModifierToGroup` (deprecated 410); `migrateLegacyModifier` (POST `.../modifiers/{id}/migrate`). |
| **Frontend – sayfa** | `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | 19, 178–210, 312–321, 364–366, 511–545 | `migrateLegacyModifier` import ve kullanımı; “Legacy-Modifier (Kompatibilität)” bölümü; tekli migration modal. |
| **Backend – controller** | `backend/Controllers/ModifierGroupsController.cs` | 335–362 | POST `api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate` → `MigrateLegacyModifier`. |
| **Backend – controller** | `backend/Controllers/AdminMigrationController.cs` | 87–123 | POST `api/admin/modifiers/{modifierId}/migrate-to-product` (admin alternatif; frontend şu an modifier-groups route kullanıyor). |
| **Backend – service** | `backend/Services/ModifierMigrationService.cs` | 150–282 | `MigrateSingleAsync`, `MigrateSingleByModifierIdAsync`. |

POS tarafında `group.modifiers` hâlâ kullanılıyor (Phase C tamamlanmadan legacy gösterim kaldırılamaz); bu analizde “admin’de legacy + bulk kaldırma” odaklı olduğu için POS listesi kısaltıldı. Detay: `LEGACY_MODIFIER_IMPLEMENTATION_AUDIT.md` §4.1, §5.2.

---

## 2) Kaldırma için ölçülebilir exit criteria

Aşağıdakiler sağlandığında **bulk + progress** (ve istenirse tamamı **legacy modifier migration** UI/API) güvenle kaldırılabilir.

| # | Kriter | Ölçüm | Kaynak |
|---|--------|--------|--------|
| 1 | **Aktif legacy modifier kalmamış** | `GET /api/admin/migration-progress` → `activeLegacyModifiersCount === 0` (en az 1 hafta demo/prod’da). | Backend DTO; mevcut progress endpoint veya tek seferlik SQL/script. |
| 2 | **Sadece legacy modifier içeren grup kalmamış** | Aynı endpoint → `groupsWithModifiersOnlyCount === 0`. | Böylece tüm add-on’lar ürün tabanlı. |
| 3 | **POS’ta legacy fallback kullanımı yok** | Log: `Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers` 0 (veya anlamlı süre boyunca tetiklenmiyor). | ProductController / ModifierGroupsController; LEGACY_MODIFIER_IMPLEMENTATION_AUDIT.md §4.2. |
| 4 | **Operasyonel onay** | Demo/prod’da “Legacy-Modifier migrasyonu tamamlandı” kararı; gerekirse yedek/rollback planı. | Runbook / change record. |
| 5 | **(Opsiyonel) Süre** | Son bulk/single migration’dan sonra X gün (örn. 14) geçmiş; yeni legacy oluşturulmuyor (410). | Zaman + 410 davranışı. |

**Not:** Sadece **bulk + progress UI/API** kaldırılacaksa 1–2 yeterli olabilir (sayılar 0). **Tüm legacy modifier UI + single migration** kaldırılacaksa 1–4 ve Phase C (POS fallback kaldırma) ile uyum gerekir.

---

## 3) Demo ortamı için düşük riskli rollout planı

### 3.1 Feature flag (önerilen)

- **Flag adı (örnek):** `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED` (veya `SHOW_LEGACY_MODIFIER_MIGRATION`).
- **Davranış:**  
  - `true`: Mevcut davranış (progress card + “Bulk-Migration ausführen” + legacy bölümü + “Als Produkt migrieren”).  
  - `false`: Progress card gizlenir, bulk butonu ve bulk modal render edilmez; isteğe bağlı: legacy modifier bölümü ve tekli migrate butonu da gizlenir.
- **Uygulama yeri:**  
  - Frontend: `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` içinde progress card, bulk buton/modal ve (opsiyonel) legacy bölümü `flag === true` ise göster.  
  - API çağrıları: Flag kapalıyken `getMigrationProgress` ve `runBulkMigration` hiç çağrılmamalı (query `enabled: flag`, bulk buton tıklanmaz).
- **Backend:** İlk aşamada endpoint’leri kaldırmayın; sadece UI’ı kapatın. İleride exit criteria sonrası endpoint’leri 410/404 ile kapatabilir veya silebilirsiniz.

### 3.2 Aşamalı kaldırma (staged removal)

| Aşama | Ne yapılır | Risk |
|--------|------------|------|
| **1 – Flag ekleme** | Feature flag + modifier-groups sayfasında progress/bulk (ve istenirse legacy bölümü) flag’e bağlanır. Varsayılan: `true`. | Düşük |
| **2 – Demo’da ölçüm** | Demo’da `migration-progress` ile 1–2 hafta boyunca `activeLegacyModifiersCount` ve `groupsWithModifiersOnlyCount` 0 mı izlenir. | Düşük |
| **3 – Demo’da flag kapatma** | Exit criteria sağlandıysa demo’da flag `false` yapılır; progress + bulk UI kaybolur. API’ler çalışır durumda kalır. | Düşük |
| **4 – Gözlem** | 1–2 hafta: modifier-groups sayfası normal kullanımda, hata/şikayet yok. | Düşük |
| **5 – Kod kaldırma** | Progress/bulk ile ilgili frontend kodu (legacyModifierMigration.ts kullanımı, state, modal, card) ve gerekirse backend endpoint’leri kaldırılır. Single migration kaldırılacaksa ayrı task. | Orta (geri almak için geri commit gerekir) |

### 3.3 Rollback

- **Sadece UI kapatıldıysa:** Flag’i tekrar `true` yapmak yeterli.  
- **Kod silindiyse:** Exit criteria’yı tekrar kontrol edin; gerekirse branch/commit ile geri alıp, sonra yeniden flag’li kaldırma yapın.

---

## 4) Sonraki adımda uygulanacak teknik task listesi

Aşağıdaki liste, “kod değiştirme yapma” kısıtı olmadan **ileride uygulanacak** işler için referans task listesidir.

### 4.1 Sadece bulk + progress kaldırma (single migration ve legacy bölümü kalsın)

- [ ] **Task 1** Feature flag ekle: env veya config’ten `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED` (veya eşdeğeri) okuyacak şekilde.
- [ ] **Task 2** `modifier-groups/page.tsx`: Progress card (Statistic’ler + “Bulk-Migration ausführen” butonu) ve bulk modal’ı flag’e bağla; flag false iken `getMigrationProgress` / `runBulkMigration` çağrılmasın (`useQuery` `enabled`, buton disabled/gizli).
- [ ] **Task 3** Exit criteria doğrulama: Demo’da progress endpoint ile 1–2 hafta 0 sayıları; dokümante et.
- [ ] **Task 4** Demo’da flag’i false yap; 1–2 hafta gözlem.
- [ ] **Task 5** Flag false ve stabil ise: `legacyModifierMigration.ts` importlarını ve ilgili state/handler/card/modal kodunu modifier-groups sayfasından kaldır; `migrationProgressKey` ve progress refetch’leri kaldır.
- [ ] **Task 6** (Opsiyonel) `frontend-admin/src/lib/api/legacyModifierMigration.ts` dosyasını sil veya sadece tipleri bırak (başka yerde kullanım yok).
- [ ] **Task 7** (Opsiyonel) Backend: GET `migration-progress` ve POST `migrate-legacy-modifiers` için 410/404 dön veya endpoint’i kaldır; Program.cs CLI dalını kaldır. Swagger ve testleri güncelle.

### 4.2 Tüm legacy modifier migration UI’ı kaldırma (single + bulk + legacy bölümü)

- [ ] **Task 8** Phase C ile uyum: POS’ta `group.modifiers` kullanımı kaldırıldıktan ve exit criteria sağlandıktan sonra yapılmalı (LEGACY_MODIFIER_IMPLEMENTATION_AUDIT.md).
- [ ] **Task 9** modifier-groups sayfasında “Legacy-Modifier (Kompatibilität)” bölümünü ve “Als Produkt migrieren” modalını flag’e bağla veya kaldır.
- [ ] **Task 10** `modifierGroups.ts`: `migrateLegacyModifier` export’unu kaldır; `addLegacyModifierToGroup` zaten deprecated.
- [ ] **Task 11** Backend: ModifierGroupsController’daki `MigrateLegacyModifier` ve AdminMigrationController’daki `MigrateModifierToProduct` kaldırma veya 410; ModifierMigrationService single/batch path’leri ve ilgili testler.

### 4.3 Dokümantasyon ve ops

- [ ] **Task 12** Runbook’u güncelle: “Legacy modifier migration tamamlandıktan sonra admin’de bulk/progress kaldırıldı” ve flag/rollback adımları.
- [ ] **Task 13** `LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md` ve `CLEANUP_AND_CONSISTENCY_REPORT.md` içinde “removal” notu ekle; bu analiz dokümanına referans ver.

---

## Özet tablo

| Soru | Cevap |
|------|--------|
| **Bulk + progress’e kim bağlı?** | Sadece `frontend-admin` modifier-groups sayfası ve `legacyModifierMigration.ts`; backend AdminMigrationController + ModifierMigrationService + Program.cs CLI. |
| **Başka ekran/uygulama?** | Hayır. POS bu endpoint’leri kullanmıyor. |
| **Exit criteria** | `activeLegacyModifiersCount === 0`, `groupsWithModifiersOnlyCount === 0` (ve opsiyonel POS log 0, operasyonel onay). |
| **Demo rollout** | Feature flag ile progress + bulk UI’ı kapat; ölçüm → flag false → gözlem → kod silme. |
| **Risk** | Flag ile aşamalı kaldırma düşük risk; doğrudan kod silme orta risk (rollback için geri commit). |
