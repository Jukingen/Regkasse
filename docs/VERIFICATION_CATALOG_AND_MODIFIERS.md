# Doğrulama: Catalog + Modifiers Senaryoları

Bu dokümanda aşağıdaki senaryolar kod akışı ile doğrulanmıştır.

---

## 1. POS ilk açılışta tek catalog request ile ürünler + modifiers yükleniyor

**Durum: DOĞRULANDI**

- **Akış:** `cash-register` → `useProductsUnified()` → `ProductCache.loadData()` → **tek çağrı** `getProductCatalog()` (GET `/Product/catalog`).
- **Kaynak:** `frontend/hooks/useProductsUnified.ts` (satır 92: `const catalog = await getProductCatalog();`), `frontend/services/api/productService.ts` (satır 233: `API_PATHS.PRODUCT.CATALOG`, satır 254–260: `modifierGroups` mapping).
- **Sonuç:** Catalog cevabında `Products[].modifierGroups` map ediliyor; `ProductRow` / `ProductGridCard` `product.modifierGroups` kullanıyor, ekstra modifier-groups isteği yok.

---

## 2. Ürün seçilip modifier seçildiğinde cart state güncelleniyor

**Durum: DOĞRULANDI**

- **Akış:**
  - Ürün tıklanınca: `onAdd(product, pendingModifiers)` → `CartContext.addItem(productId, 1, { modifiers, productName, unitPrice })`.
  - Optimistic update: `lineUnitPrice = unitPrice + modifierTotal`, `totalPrice = lineUnitPrice * quantity`; `modifiers` satıra yazılıyor (`frontend/contexts/CartContext.tsx` satır 302–356).
  - Modifier chip toggle: `onToggleModifier` → sepette satır varsa `toggleExtraOnCartItem`, yoksa `pendingModifiersByProduct` güncelleniyor (`cash-register.tsx` usePOSOrderFlow).
- **Kaynak:** `CartContext.addItem` (optimistic cart + backend sonrası merge), `CartContext.toggleExtraOnCartItem`, `lastCartItemModifiersByProductId` / `selectedModifiersForProduct` türetimi.
- **Sonuç:** Hem ekleme hem modifier seçimi cart state’i (items, modifiers, totals) güncelliyor.

---

## 3. Page refresh sonrası table-orders-recovery selected modifiers’ı geri yüklüyor

**Durum: FE HAZIR; BACKEND VERİ KAYNAĞI EKSİK**

- **Akış:**
  - Refresh sonrası: `useTableOrdersRecoveryOptimized` → GET `/cart/table-orders-recovery` (masa listesi + item’lar).
  - Masa seçilince: `switchTable(tableNumber)` → `fetchTableCart(tableNumber)` → GET `/cart/current?tableNumber=X` → gelen item’lar `SelectedModifiers ?? selectedModifiers` ile map edilip `cartsByTable` dolduruluyor.
- **FE:** `CartContext` (add-item ve fetchTableCart) item mapping’de `item.SelectedModifiers ?? item.selectedModifiers` kullanıyor; recovery tipinde `TableOrderRecoveryItem.selectedModifiers` tanımlı.
- **Backend:** `CartItem` / `TableOrderItem` entity’lerinde modifier alanı yok; `BuildCartResponse` ve table-orders-recovery item’ları şu an `SelectedModifiers`’ı hep boş liste dönüyor. Modifier’ların kalıcı saklanması (örn. JSON sütun veya ilişkili tablo) eklendiğinde aynı FE kodu geri yükleyecek.
- **Sonuç:** Akış ve tipler doğru; kalıcı geri yükleme için backend’de modifier persistence gerekli.

---

## 4. add-item response totals modifier fiyatlarını içeriyor

**Durum: BACKEND HENÜZ MODİFİER FİYATLARINI TOPLAMA DAHİL ETMİYOR**

- **Backend:** `AddItemToCart` sadece `product.Price` ile `CartItem.UnitPrice` set ediyor; modifier fiyatı yok. `BuildCartResponse` `CartMoneyHelper.ComputeLine(ci.UnitPrice, ci.Quantity, taxType)` kullanıyor; `UnitPrice` = ürün fiyatı, modifier yok.
- **Kaynak:** `backend/Controllers/CartController.cs` (add-item satır 304: `UnitPrice = product.Price`; BuildCartResponse satır 1387–1418: line/totals sadece `ci.UnitPrice`/`ci.Quantity` ile).
- **FE:** add-item cevabındaki cart’ı kullanıyor; backend totals modifier içermediği için response’taki toplamlar modifier’sız. Optimistic tarafta FE modifier’lı fiyatı hesaplıyor; backend cevabı gelince cart replace edildiği için toplamlar şu an backend’in (modifier’sız) değerine dönüyor.
- **Sonuç:** Modifier’ların toplama yansıması için backend’de (1) CartItem’da modifier saklama, (2) satır/toplam hesaplamasında modifier fiyatlarının eklenmesi gerekiyor.

---

## 5. Eski modifier-groups request fırtınası artık yok

**Durum: DOĞRULANDI**

- **Eski davranış:** Her `ProductRow` / `ProductGridCard` mount’unda `useProductModifierGroups(product.id)` → GET `/Product/{id}/modifier-groups` (N ürün = N istek).
- **Yeni davranış:** `ProductRow` ve `ProductGridCard` artık `useProductModifierGroups` kullanmıyor; modifier verisi `product.modifierGroups` (catalog’tan) ile geliyor.
- **Kalan tek kullanım:** `getProductModifierGroups` sadece `ModifierSelectionBottomSheet` ve `ModifierSelectionModal` içinde, kullanıcı modal/sheet açtığında (ve `modifierGroups` prop verilmediğinde) tek seferlik çağrılıyor; sayfa açılışında tetiklenmiyor.
- **Kaynak:** `frontend/components/ProductRow.tsx`, `ProductGridCard.tsx` (hook import/yok); `frontend/hooks/useProductModifierGroups.ts` artık hiçbir yerde import edilmiyor.
- **Sonuç:** Açılışta sadece 1× catalog isteği; ürün başına modifier-groups isteği yok.

---

## Özet tablo

| # | Senaryo | Durum | Not |
|---|---------|--------|-----|
| 1 | Tek catalog ile ürünler + modifiers | OK | useProductsUnified → getProductCatalog; modifierGroups mapping |
| 2 | Ürün + modifier seçimi → cart güncellemesi | OK | addItem optimistic + toggleExtraOnCartItem / pending |
| 3 | Refresh sonrası recovery ile selectedModifiers | FE ok, BE eksik | Mapping hazır; backend’de modifier persistence yok |
| 4 | add-item response totals modifier içeriyor | BE eksik | UnitPrice/totals sadece ürün fiyatı; modifier persistence + hesaplama gerekli |
| 5 | Modifier-groups request fırtınası yok | OK | Sadece catalog; row/card’da hook yok |
