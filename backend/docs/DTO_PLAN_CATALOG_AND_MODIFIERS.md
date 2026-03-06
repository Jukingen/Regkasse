# DTO Plan: Catalog modifierGroups + Item selectedModifiers

## Hedef
- **Catalog response:** Ürün başına `modifierGroups` (flat DTO, EF entity dönülmeyecek).
- **table-orders-recovery / add-item item DTO’ları:** Satır başına `selectedModifiers`.
- Tüm DTO’lar flat, JSON’da camelCase (ASP.NET Core default).

---

## 1. Yeni / Güncellenecek DTO’lar

### 1.1 SelectedModifierDto (satır seçili modifier)
- **Dosya:** `DTOs/ModifierDTOs.cs`
- **Alanlar:** `Id` (Guid), `Name` (string), `Price` (decimal)
- **Kullanım:** Cart item ve table-order item yanıtlarında seçili modifier listesi.

### 1.2 Catalog DTO’lar
- **Dosya:** `DTOs/CatalogDTOs.cs` (yeni)
- **CatalogCategoryDto:** `Id`, `Name`, `VatRate`
- **CatalogProductDto:** Mevcut katalog ürün alanları + `ModifierGroups` (List&lt;ModifierGroupDto&gt;). ModifierGroupDto mevcut, tekrar kullanılır.
- **CatalogResponseDto:** `Categories` (List&lt;CatalogCategoryDto&gt;), `Products` (List&lt;CatalogProductDto&gt;)

### 1.3 Cart / Table-Order item DTO’ları
- **CartItemResponse** (CartController): `SelectedModifiers` (List&lt;SelectedModifierDto&gt;, default boş).
- **TableOrderItemInfo** (CartController): `SelectedModifiers` (List&lt;SelectedModifierDto&gt;, default boş).

---

## 2. Mapping / Veri kaynağı

### 2.1 Catalog GetCatalog()
- Ürünler: Mevcut sorgu (aktif ürünler + CategoryNavigation).
- Modifier grupları: N+1 olmaması için toplu yükleme:
  1. Tüm ürün ID’leri ile `ProductModifierGroupAssignments` çek (productId → groupId listesi).
  2. Tüm benzersiz groupId’ler için `ProductModifierGroups` + `Modifiers` (Include) tek sorguda çek.
  3. ProductId → List&lt;ModifierGroupDto&gt; map’i oluştur (assignment SortOrder’a göre sıralı).
  4. Her ürün için CatalogProductDto: mevcut alanlar + ModifierGroups = map’ten veya boş liste.

### 2.2 BuildCartResponse
- CartItem → CartItemResponse: Mevcut mapping aynı, **SelectedModifiers = new List&lt;SelectedModifierDto&gt;()** (CartItem’da modifier saklanmadığı için şimdilik boş).

### 2.3 table-orders-recovery
- TableOrderItem / CartItem → TableOrderItemInfo: Mevcut mapping aynı, **SelectedModifiers = new List&lt;SelectedModifierDto&gt;()** (şimdilik boş).

---

## 3. Serialization
- C# tarafında PascalCase (Id, Name, ModifierGroups, SelectedModifiers).
- JSON çıktı: camelCase (id, name, modifierGroups, selectedModifiers) — ASP.NET Core varsayılan System.Text.Json ile.

---

## 4. Etkilenen dosyalar
| Dosya | Değişiklik |
|-------|------------|
| `DTOs/ModifierDTOs.cs` | SelectedModifierDto ekleme |
| `DTOs/CatalogDTOs.cs` | Yeni: CatalogCategoryDto, CatalogProductDto, CatalogResponseDto |
| `Controllers/ProductController.cs` | GetCatalog: CatalogResponseDto kullanımı, toplu modifier yükleme |
| `Controllers/CartController.cs` | CartItemResponse ve TableOrderItemInfo’ya SelectedModifiers ekleme; BuildCartResponse ve table-orders-recovery mapping’inde SelectedModifiers atama |
