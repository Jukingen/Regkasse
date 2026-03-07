# Legacy Modifier & Migration Katmanı Kademeli Kaldırma Planı

**Tarih:** 2025-03-07  
**Kapsam:** Kod değiştirme yapılmadan önce removal plan.  
**Hedef:** Legacy Modifier ve migration/bulk-migration uyumluluk katmanını kademeli kaldırmak.  
**Sabit:** Modifier sistemi **Product + Add-on-group** (product_modifier_groups + add_on_group_products + products) olarak kalacak.

---

## 1) Etki Analizi (Frontend / Backend / DB bağımlılıkları, dosya+satır)

### 1.1 Frontend Admin

| Dosya | Satır(lar) | Bağımlılık |
|-------|------------|------------|
| `frontend-admin/src/lib/api/legacyModifierMigration.ts` | 1–115 | Tüm dosya: GET migration-progress, POST migrate-legacy-modifiers, DTO’lar. |
| `frontend-admin/src/app/(protected)/modifier-groups/page.tsx` | 19, 24, 30, 50–59, 139–174, 178–210, 312–321, 364–393, 511–545, 539–608 | Import migrateLegacyModifier + getMigrationProgress/runBulkMigration; state (bulk/single migration); progress card; legacy bölümü; single + bulk modals. |
| `frontend-admin/src/lib/api/modifierGroups.ts` | 15, 48, 91–100, 126–152 | ModifierDto/IsActive; getModifierGroups (legacy modifiers dahil); addLegacyModifierToGroup (410); migrateLegacyModifier. |

### 1.2 Frontend POS

| Dosya | Satır(lar) | Bağımlılık |
|-------|------------|------------|
| `frontend/services/api/productModifiersService.ts` | 8–9, 47–58, 80 | ModifierDto; ModifierGroupDto.modifiers; mapGroupForPOS (group.modifiers kullanımı – Phase C’de kaldırılacak). |
| `frontend/services/api/productService.ts` | 4, 80, 137, 262–263 | ModifierGroupDto; mapModifierGroup (modifierGroups mapping). |
| `frontend/hooks/useProductModifierGroups.ts` | 7–8, 12, 18–19, 29, 47 | ModifierGroupDto; groups; products-only filtresi (Phase C yorumu). |
| `frontend/components/ModifierSelectionBottomSheet.tsx` | 17–18, 42, 105–106, 111, 159, 183, 206, 290 | ModifierGroupDto; option rows (group.products odaklı; legacy fallback kaldırılınca sadece products). |
| `frontend/components/ProductRow.tsx` | 52 | Phase C yorumu: group.products only. |
| `frontend/components/ProductGridCard.tsx` | 50 | Aynı. |
| `frontend/contexts/CartContext.tsx` | 223, 313, 421 | modifiers?.length (legacy cart line display). |
| `frontend/components/ExtrasChips.tsx` | 28 | modifiers?.length. |
| `frontend/__tests__/addOnFlow.test.ts` | 2, 7, 19–22, 30–35, 49–133 | ModifierGroupDto.modifiers (test şekli). |
| `frontend/__tests__/posModifierFlow.test.ts` | 25, 33–48, 53, 65, 80, 91–106 | group.products only (Phase C). |
| `frontend/utils/modifierSelectionUtils.ts` | 7, 31, 45, 76, 91, 100, 127 | ModifierGroupSelectionShape (product/modifier agnostik). |

### 1.3 Backend – API / Service / DTO

| Dosya | Satır(lar) | Bağımlılık |
|-------|------------|------------|
| `backend/Controllers/AdminMigrationController.cs` | 30–42, 50–77, 87–123 | GET migration-progress; POST migrate-legacy-modifiers; POST modifiers/{id}/migrate-to-product. |
| `backend/Controllers/ModifierGroupsController.cs` | 37–51, 69–80, 335–362, 365–391 | Include(g => g.Modifiers); MapToModifierGroupDto (Modifiers); MigrateLegacyModifier; log Phase2.LegacyModifier. |
| `backend/Controllers/AdminProductsController.cs` | 291–305, 388–412, 421–445 | GetProductModifierGroups Include; MapToModifierGroupDto (Modifiers); MapToModifierGroupDtoForAdminProduct (Modifiers = empty). |
| `backend/Controllers/ProductController.cs` | 166–167, 230–257, 263–287, 547–562 | MapToModifierGroupDto (Modifiers); MapToModifierGroupDtoForPos (Modifiers = []); GetProductModifierGroups. |
| `backend/Services/ModifierMigrationService.cs` | Tümü | GetMigrationProgressAsync; MigrateAsync (batch); MigrateSingleAsync / MigrateSingleByModifierIdAsync; ProductModifiers okuma/yazma. |
| `backend/Services/IModifierMigrationService.cs` | Tümü | Aynı. |
| `backend/Services/ProductModifierValidationService.cs` | 22–70 | GetAllowedModifierIdsForProductAsync (ProductModifiers); GetAllowedModifiersWithPricesForProductAsync (ProductModifiers). |
| `backend/Program.cs` | 240–274 | CLI migrate-legacy-modifiers. |
| `backend/DTOs/ModifierDTOs.cs` | 8–21, 36–47, 65–76, 113–124 | ModifierGroupDto.Modifiers; ModifierDto; CreateModifierRequest (deprecated); SelectedModifierDto/Input. |
| `backend/DTOs/ModifierMigrationDTOs.cs` | Tümü | Migration progress, batch/single request/result DTO’lar. |
| `backend/DTOs/CatalogDTOs.cs` | 38 | CatalogProductDto.ModifierGroups. |

### 1.4 Backend – Veri Erişimi / Model

| Dosya | Satır(lar) | Bağımlılık |
|-------|------------|------------|
| `backend/Data/AppDbContext.cs` | 20, 47, 756–760, 870–874 | DbSet CartItemModifiers, TableOrderItemModifiers; modifier_id config. ProductModifiers DbSet (Models’tan). |
| `backend/Models/ProductModifier.cs` | Tümü | Tablo product_modifiers. |
| `backend/Models/ProductModifierGroup.cs` | Navigation Modifiers | ProductModifier koleksiyonu. |
| `backend/Models/CartItemModifier.cs` | Tümü | modifier_id (FK yok; denormalized name/price). |
| `backend/Models/TableOrderItemModifier.cs` | Tümü | Aynı. |
| `backend/Controllers/CartController.cs` | 1214–1215, 1288–1289, 1477–1499, 1613–1630, 1694–1709 | SelectedModifiers (CartItemModifier/TableOrderItemModifier → DTO). |
| `backend/Services/PaymentService.cs` | 202–261, 288, 1162 | ModifierIds ignored (Phase 3); GetAllowedModifiersWithPricesForProductAsync; hasLegacyModifiers. |
| `backend/Services/ReceiptService.cs` | 126 | hasLegacyModifiers. |

### 1.5 Veritabanı

| Tablo | Amaç | Kaldırma |
|-------|------|----------|
| `product_modifier_groups` | Grup tanımı (Product + Add-on kalacak) | **KALMAZ** |
| `addon_group_products` | Grup–Product ilişkisi | **KALMAZ** |
| `product_modifiers` | Legacy modifier kayıtları | **KALDIRILACAK** (tüm migration tamamlandıktan ve API/okuma kodu kaldırıldıktan sonra). |
| `product_modifier_group_assignments` | Ürün–grup ataması | **KALMAZ** |
| `cart_item_modifiers` | Eski sepet satırı modifier’ları (modifier_id, name, price denormalized) | **KALMAZ** – tarihsel okuma; FK product_modifiers’a yok. |
| `table_order_item_modifiers` | Aynı (masa siparişi) | **KALMAZ** – aynı. |

**Not:** `cart_item_modifiers` / `table_order_item_modifiers` içindeki `modifier_id`, `product_modifiers` silindikten sonra yetim kalır; satırlar name/price ile okunmaya devam edilebilir.

---

## 2) Feature-Flag Tasarımı

### 2.1 Flag’ler (önerilen)

| Flag (env/config) | Varsayılan | Etki |
|-------------------|------------|------|
| `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED` | `true` | Admin: migration progress card, “Bulk-Migration ausführen” butonu ve modalı, “Legacy-Modifier (Kompatibilität)” bölümü, “Als Produkt migrieren” butonu/modalı. false → bu UI gizlenir; getMigrationProgress/runBulkMigration/migrateLegacyModifier çağrılmaz. |
| `API_LEGACY_MODIFIER_RESPONSE_ENABLED` | `true` | Backend: ModifierGroupDto.Modifiers doldurulsun mu (Admin + ModifierGroups). false → Include(Modifiers) atlanır veya Modifiers = [] dönülür; admin modifier-groups sayfası “Legacy” listesi boş. |

### 2.2 Uygulama Kuralları

- **Admin:** `modifier-groups/page.tsx` içinde progress card, bulk buton/modal, legacy bölümü, single-migrate modalı `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED === true` ise render edilir. useQuery(progress) `enabled: flag`.
- **Backend:** İlk aşamada endpoint’leri kaldırma; sadece response’ta Modifiers’ı flag’e göre doldur veya boş bırak. İleride exit criteria sonrası migration endpoint’lerini 410/404 ile kapat.
- **POS:** Phase C’de `group.modifiers` fallback’i kaldırıldığında ek flag gerekmez (API zaten Modifiers = [] dönüyor olabilir).

### 2.3 Flag Sırası (kademeli)

1. **Aşama 1:** `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED` ekle; demo’da exit criteria sağlandıktan sonra false yap (sadece UI gizleme).
2. **Aşama 2:** `API_LEGACY_MODIFIER_RESPONSE_ENABLED` ekle; admin/ModifierGroups’ta Modifiers’ı flag’e bağla; POS’ta modifiers kullanımı kaldırıldıktan sonra false yap.
3. **Aşama 3:** Migration endpoint’leri 410 dönene kadar flag’ler kodda kalabilir; sonra flag + ilgili kod silinir.

---

## 3) API / DTO Deprecation Sırası

### 3.1 Önerilen sıra

| Sıra | Ne | Koşul | Aksiyon |
|------|-----|--------|---------|
| 1 | POST `api/modifier-groups/{groupId}/modifiers` (AddLegacyModifier) | Zaten 410 | Dokümante “removed”; endpoint kaldır veya 410 body’de “Removed” mesajı. |
| 2 | GET `api/admin/migration-progress` | activeLegacyModifiersCount + groupsWithModifiersOnlyCount = 0, flag false | 410 veya 404; response body’de deprecation mesajı. |
| 3 | POST `api/admin/migrate-legacy-modifiers` | Aynı | 410. |
| 4 | POST `api/modifier-groups/{groupId}/modifiers/{modifierId}/migrate` | Tüm legacy migre edildi, flag false | 410. |
| 5 | POST `api/admin/modifiers/{modifierId}/migrate-to-product` | Aynı | 410. |
| 6 | ModifierGroupDto.Modifiers | Phase D; POS + admin Modifiers kullanmıyor | DTO’da [Obsolete] zaten var; response’ta her zaman [] veya alanı kaldır (breaking; versiyonlama gerekebilir). |
| 7 | ModifierDto (response’ta) | Modifiers boş/kaldırıldıktan sonra | Sadece migration/legacy endpoint’lerinde kullanılıyorsa kaldırılabilir; ModifierGroupDto’dan çıkar. |
| 8 | SelectedModifierDto / SelectedModifierInputDto | Tarihsel cart/table-order okuma kalacak | Deprecate et; kaldırma opsiyonel (read-only kalabilir). |
| 9 | PaymentItemRequest.ModifierIds | Zaten kullanılmıyor (Phase 3) | Dokümante “ignored”; contract’tan kaldırmak breaking olabilir, opsiyonel. |

### 3.2 DTO / Swagger

- ModifierMigrationDTOs (LegacyModifierMigrationProgressDto, ModifierMigrationResultDto, MigrateSingleModifierRequestDto/ResultDto): Migration endpoint’leri 410 olduktan sonra kaldırılabilir veya deprecated bırakılır.
- Swagger’da ilgili path’ler “deprecated: true” ile işaretlenir; 410 dönünce açıklama güncellenir.

---

## 4) DB Migration Sırası ve Rollback Planı

### 4.1 Kalacak tablolar (dokunulmaz)

- `product_modifier_groups`
- `product_modifier_group_assignments`
- `addon_group_products`
- `cart_item_modifiers` (tarihsel; FK product_modifiers’a yok)
- `table_order_item_modifiers` (tarihsel; aynı)

### 4.2 Kaldırılacak tablo

- **product_modifiers**  
  **Koşul:** (1) Tüm aktif legacy modifier’lar migre edilmiş (GET migration-progress → 0). (2) Yeni yazma yok (AddLegacyModifier 410). (3) Okuma kodu kaldırılmış (ModifierMigrationService, ProductModifierValidationService, Include(Modifiers), MapToModifierGroupDto Modifiers doldurma). (4) Yasal/saklama gereği varsa arşiv alındı.

### 4.3 Migration sırası

| Sıra | Migration | Açıklama |
|------|-----------|----------|
| 1 | (Yok – önce kod) | Önce API/uygulama katmanında legacy modifier okuma/yazma kaldırılır; migration progress 0 doğrulanır. |
| 2 | `DropProductModifiersTable` | product_modifiers tablosunu DROP. FK yok (cart_item_modifiers/table_order_item_modifiers product_modifiers’a FK ile bağlı değil). |

### 4.4 Rollback planı

- **product_modifiers drop öncesi:** Tablo ve veriyi yedekle (pg_dump veya tablo bazlı export). Gerekirse migration Down ile tabloyu tekrar oluştur (AddProductModifiers.Down sadece product_modifiers + product_modifier_groups + assignments’ı drop ediyor; burada sadece product_modifiers’ı geri ekleyen ayrı bir “RestoreProductModifiers” migration’ı tasarlanabilir – örn. boş tablo + gerekirse yedekten veri import).
- **Rollback adımları:** (1) Yeni migration ile `product_modifiers` tablosunu aynı şemada tekrar oluştur. (2) Yedekten veri yükle (opsiyonel). (3) Uygulama kodunu legacy modifier okumayı tekrar açacak şekilde geri al (feature flag veya eski commit).
- **Risk:** Cart/table-order’daki eski modifier_id’ler product_modifiers’a tekrar FK ile bağlanmaz; sadece tarihsel gösterim için name/price kullanılmaya devam eder.

---

## 5) Test Planı (Unit / Integration / Smoke)

### 5.1 Unit

| Alan | Yapılacak |
|------|------------|
| **ModifierMigrationService** | GetMigrationProgress_*, MigrateAsync_*, MigrateSingleByModifierId_* – önce yeşil; deprecation sonrası migration endpoint’leri kaldırılınca bu testler kaldırılır veya “legacy path” olarak skip/guard ile çalıştırılır. |
| **ProductModifierValidationService** | Product_modifiers kullanan testler – tablo kalkınca mock/stub veya servis kaldırılıyorsa testler kaldırılır. |
| **ModifierGroupDto / ModifierDto** | Phase2DtoCompatibilityTests – Modifiers alanı kaldırılırsa test güncellenir veya kaldırılır. |
| **Frontend admin** | modifier-groups: progress/bulk/single migration flag kapalıyken çağrı yapılmadığı ve ilgili UI’ın gizlendiği unit/component testi. |
| **Frontend POS** | addOnFlow.test, posModifierFlow.test, modifierSelectionUtils.test – group.products only; group.modifiers kullanımı kaldırıldığında testler güncellenir. |

### 5.2 Integration

| Alan | Yapılacak |
|------|------------|
| **GET modifier-groups** | Modifiers = [] (veya flag kapalı) döndüğü; admin ve POS client’ının hata vermediği. |
| **GET migration-progress** | 0 döndüğü; 410 sonrası 410 döndüğü. |
| **POST migrate-legacy-modifiers** | 410 döndüğü. |
| **POST …/modifiers/{id}/migrate** | 410 döndüğü. |
| **Cart/TableOrder** | Eski CartItemModifier/TableOrderItemModifier içeren sepet/sipariş okumanın çalıştığı (SelectedModifiers dolu); product_modifiers tablosu yokken de okuma hata vermez (denormalized name/price). |
| **Payment/CreatePayment** | ModifierIds gönderilse bile yeni modifier yazılmadığı; flat item’ların işlendiği. |

### 5.3 Smoke (Demo/Staging)

| Kontrol | Açıklama |
|---------|----------|
| Admin modifier-groups sayfası | Flag kapalıyken progress/bulk/legacy bölümü görünmüyor; grup listesi ve “+ Produkt” akışı çalışıyor. |
| Admin ürün detay | Modifier grupları atanıyor; sadece Products listesi anlamlı. |
| POS katalog / ürün detay | Add-on grupları ve seçenekler (group.products) görünüyor; hata yok. |
| POS sepet / ödeme | Add-on’lar ayrı satır olarak ekleniyor; ödeme başarılı. |
| Eski sepet (varsa) | Legacy SelectedModifiers’lı satırlar okunuyor; fiş/yazdırma bozulmuyor. |
| GET migration-progress | 0/0 veya 410. |

---

## 6) Cutover Checklist (Ölçülebilir Exit Criteria)

### 6.1 Migration UI/API kapatma (bulk + progress + single)

- [ ] **M1** `GET /api/admin/migration-progress` → `activeLegacyModifiersCount === 0` (en az 7 gün demo/prod).
- [ ] **M2** Aynı endpoint → `groupsWithModifiersOnlyCount === 0`.
- [ ] **M3** POS/backend log’da `Phase2.LegacyModifier.ModifierGroupDtoReturnedWithModifiers` tetiklenmiyor (veya 7 gün 0).
- [ ] **M4** Operasyonel onay: “Legacy modifier migration tamamlandı”.
- [ ] **M5** Feature flag `ADMIN_LEGACY_MODIFIER_MIGRATION_ENABLED=false` ile 7 gün sorunsuz.

### 6.2 API/DTO deprecation

- [ ] **D1** Migration endpoint’leri (migration-progress, migrate-legacy-modifiers, …/modifiers/…/migrate) 410 veya kaldırıldı.
- [ ] **D2** Swagger’da ilgili path’ler deprecated veya kaldırıldı.
- [ ] **D3** ModifierGroupDto.Modifiers artık dönmüyor veya her zaman [].

### 6.3 POS / Admin kod

- [ ] **C1** POS’ta `group.modifiers` ile add-on seçimi/display kodu yok (sadece group.products).
- [ ] **C2** Admin modifier-groups sayfasında legacy bölümü ve migration butonları kaldırıldı.
- [ ] **C3** ProductModifierValidationService ya kaldırıldı ya da sadece tarihsel okuma için kısıtlı.

### 6.4 Veritabanı

- [ ] **DB1** product_modifiers tablosuna yazan/okuyan kod yok.
- [ ] **DB2** Yasal/saklama gereği karşılandı (yedek/arşiv).
- [ ] **DB3** `DropProductModifiersTable` migration’ı uygulandı; rollback planı dokümante.

### 6.5 Test ve go-live

- [ ] **T1** İlgili unit/integration testler güncellendi veya kaldırıldı; CI yeşil.
- [ ] **T2** Smoke checklist demo/staging’de geçti.
- [ ] **T3** Production cutover kaydı ve rollback adımları yazıldı.

---

## Referanslar

- `LEGACY_MODIFIER_BULK_REMOVAL_RISK_ANALYSIS.md` – Bağımlılık ve exit criteria özeti.
- `LEGACY_MODIFIER_IMPLEMENTATION_AUDIT.md` – Phase A–E ve POS fallback konumu.
- `LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md` – Mevcut migration UI/API davranışı.
