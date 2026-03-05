# FE-Admin: Legacy api/Product ve api/Categories Kaldırımı

Bu dokümanda backend’e admin product endpoint’leri eklenmesi ve FE-Admin’in tamamen admin endpoint’lere geçmesi özetleniyor.

---

## 1) Backend: Eklenen admin endpoint’ler

**Dosya:** `backend/Controllers/AdminProductsController.cs`

Aşağıdaki endpoint’ler eklendi (mevcut ProductController davranışı ve validation ile uyumlu):

| Method | Path | Açıklama |
|--------|------|----------|
| POST | `/api/admin/products` | Yeni ürün oluştur (RKSV validasyonu, category sync) |
| PUT | `/api/admin/products/{id}` | Ürün güncelle |
| PUT | `/api/admin/products/stock/{id}` | Stok güncelle (body: `{ "quantity": number }`) |
| DELETE | `/api/admin/products/{id}` | Ürün sil (soft delete) |
| GET | `/api/admin/products/search?name=&category=` | Ürün ara (name, category adı) |
| GET | `/api/admin/products/{id}/modifier-groups` | Ürüne atanmış modifier grupları |
| POST | `/api/admin/products/{id}/modifier-groups` | Ürüne modifier grupları ata (body: `{ "modifierGroupIds": ["uuid", ...] }`) |

**Kullanılan servisler / validation:**
- `IGenericRepository<Product>`, `AppDbContext`
- `ValidateProductForRKSVAsync` (fiyat, stok, CategoryId, TaxType, RKSV alanları, TaxRate uyumu)
- `UpdateStockRequest` (ProductController ile aynı namespace’te)
- `ValidationResult` (ProductController ile aynı namespace’te)
- DTO’lar: `ModifierGroupDto`, `SetProductModifierGroupsRequest` (`KasseAPI_Final.DTOs`)
- Modeller: `Product`, `TaxTypes`, `RksvProductTypes` (`KasseAPI_Final.Models`)

**Diff (özet):**
- `using KasseAPI_Final.DTOs` eklendi.
- `GetList`, `GetById` mevcut; buna ek olarak:
  - `[HttpGet("search")]` Search
  - `[HttpPost]` Create
  - `[HttpPut("{id:guid}")]` Update
  - `[HttpPut("stock/{id:guid}")]` UpdateStock
  - `[HttpDelete("{id:guid}")]` Delete
  - `[HttpGet("{id:guid}/modifier-groups")]` GetProductModifierGroups
  - `[HttpPost("{id:guid}/modifier-groups")]` SetProductModifierGroups
  - `ValidateProductForRKSVAsync` private helper

**Categories:** Zaten yalnızca `api/admin/categories` route’u kullanılıyor (legacy `api/Categories` önceki cleanup’ta kaldırıldı). FE-Admin kategori çağrıları `src/api/admin/categories.ts` üzerinden `/api/admin/categories` kullanıyor.

---

## 2) FE-Admin: Değişen dosyalar ve snippet’lar

### Değişen dosya listesi

| Dosya | Değişiklik |
|-------|------------|
| `frontend-admin/src/api/admin/products.ts` | Tüm product çağrıları `/api/admin/products` ve alt path’lere taşındı; `LEGACY_PRODUCT` kaldırıldı. |

Diğer feature/hook dosyaları zaten admin API’yi kullanıyordu (`useProducts` → `@/api/admin/products`, `useCategories` → `@/api/admin/categories`). Sadece `products.ts` içindeki URL’ler admin’e çekildi.

### Kritik snippet’lar (products.ts)

**Önce (legacy):**
```ts
const ADMIN_PRODUCTS = '/api/admin/products';
const LEGACY_PRODUCT = '/api/Product';

// create, update, delete, search, stock, modifier-groups → LEGACY_PRODUCT
```

**Sonra (yalnızca admin):**
```ts
const ADMIN_PRODUCTS = '/api/admin/products';

// createAdminProduct  → POST   ADMIN_PRODUCTS
// updateAdminProduct  → PUT    ADMIN_PRODUCTS/${id}
// deleteAdminProduct  → DELETE ADMIN_PRODUCTS/${id}
// searchAdminProducts → GET    ADMIN_PRODUCTS/search?name=&category=
// updateAdminProductStock → PUT ADMIN_PRODUCTS/stock/${id}
// getAdminProductModifierGroups  → GET  ADMIN_PRODUCTS/${productId}/modifier-groups
// setAdminProductModifierGroups  → POST ADMIN_PRODUCTS/${productId}/modifier-groups
```

- **Liste / detay:** Zaten `getAdminProductsList`, `getAdminProductById` → `ADMIN_PRODUCTS`, `ADMIN_PRODUCTS/{id}`.
- **Categories:** `src/api/admin/categories.ts` zaten tüm çağrıları `ADMIN_CATEGORIES = '/api/admin/categories'` ile yapıyor; değişiklik yok.
- **modifierGroups.ts:** Ürün modifier’ları için `getAdminProductModifierGroups` ve `setAdminProductModifierGroups` kullanıyor; bu fonksiyonlar artık admin URL’lerini kullanıyor.

---

## 3) Cleanup: "/api/Product" ve "/api/Categories" araması

- **Uygulama kodu (el yazımı):** `src/api/admin/*`, `src/features/*`, `src/app/*` içinde bu string’ler yok. Tüm product/category akışları admin endpoint’lere gidiyor.
- **Orval ile üretilen dosyalar:** `src/api/generated/product/product.ts` ve `src/api/generated/categories/categories.ts` içinde hâlâ `/api/Product` ve `/api/Categories` path’leri var (Swagger/Orval aynı kaldığı için).
- Bu generated dosyalar FE-Admin’de product/category listesi, CRUD veya arama için **kullanılmıyor**; tüm kullanım `src/api/admin/products.ts` ve `src/api/admin/categories.ts` üzerinden.

**Sonuç:**
- App code (generated hariç): **0** adet `/api/Product` veya `/api/Categories` kullanımı.
- Tüm `src` (generated dahil) içinde sadece Orval çıktısındaki 2 dosyada bu string’ler geçiyor; davranış değişmediği ve Orval input’u aynı kaldığı için bu dosyalar değiştirilmedi.

---

## 4) UI davranışı

- Ürün listesi, sayfalama, arama, oluşturma, düzenleme, silme, stok güncelleme ve ürün modifier grupları aynı şekilde çalışır; yalnızca istekler `/api/admin/products*` adreslerine gider.
- Kategori listesi, CRUD ve arama davranışı değişmedi; zaten `/api/admin/categories` kullanılıyordu.

---

## 5) Özet

| Alan | Durum |
|------|--------|
| Backend admin product | POST, PUT, PUT stock, DELETE, search, modifier-groups eklendi; validation/servisler mevcut davranışla uyumlu. |
| FE-Admin product | Tüm çağrılar `/api/admin/products*`; legacy path kullanımı kaldırıldı. |
| FE-Admin categories | Zaten `/api/admin/categories`; değişiklik yok. |
| Legacy string’ler (app code) | 0 (sadece Orval generated dosyalarında path’ler duruyor, kullanılmıyor). |
