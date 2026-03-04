# Kategori Veri Akışı – Analiz Özeti

## Soru: Kategoriler Admin’den backend’e kaydedilip POS’a API ile mi geliyor?

**Kısa cevap:** Kısmen. Admin’de **Categories** tablosu (api/Categories) var ve oradan CRUD yapılıyor; fakat **POS’taki kategori sekmeleri** doğrudan bu tablodan beslenmiyor. POS, **Product** API’lerinden gelen veriyle çalışıyor: kategoriler, ürünlerin `Category` (string) alanından türetiliyor.

---

## 1) POS tarafında kategori verisinin kaynağı

| Kaynak | Açıklama |
|--------|----------|
| **Orval / generated hooks** | POS (Expo) tarafında Orval yok; API çağrıları `productService.ts` ve `apiPaths.ts` ile yapılıyor. |
| **React Query** | POS’ta product/category için React Query kullanılmıyor; `useProductsUnified` içinde doğrudan `productService` fonksiyonları kullanılıyor. |
| **Zustand** | Kategori state’i Zustand’da tutulmuyor; `useProductsUnified` içindeki singleton `ProductCache` sınıfı kullanılıyor. |
| **Mock / hardcoded** | Ana ekran (cash-register) API kullanıyor. Sadece **ProductSelector** bileşeninde sabit bir kategori listesi var (mock). |

### Ana akış (cash-register / CategoryFilter)

1. **`cash-register.tsx`** → `useProductsUnified()` kullanır.
2. **`useProductsUnified.ts`**:
   - Önce **`getProductCatalog()`** çağrılır → `GET /Product/catalog`.
   - Catalog cevabında `Categories` (id + name) ve `Products` (içinde `categoryId`, `ProductCategory`) gelir.
   - Kategoriler: `catalog.categories.map(c => c.name)` → **string[]**.
   - Bu liste `CategoryFilter`’a prop olarak verilir; **mock yok**, tamamen API.
3. Catalog hata verirse **fallback**:
   - `getAllProducts(1, 1000)` + `getAllCategories()`.
   - `getAllCategories()` → **`GET /Product/categories`** (ProductController).

Yani POS ana ekranındaki kategori tab’ları **backend’den** geliyor; endpoint’ler **Product** controller’ına ait.

### Mock / hardcoded kullanım

- **`frontend/components/ProductSelector.tsx`** (satır 50–63):  
  `categories` sabit bir dizi:
  - `'all', 'Hauptgerichte', 'Getränke', 'Desserts', 'Alkoholische Getränke', 'Snacks', 'Suppen', 'Vorspeisen', 'Salate', 'Kaffee & Tee', 'Süßigkeiten', 'Spezialitäten', 'Brot & Gebäck'`
- Bu bileşen fatura/ürün seçimi gibi farklı bir akışta; ana kasa ekranındaki kategori tab’ları bu listeyi kullanmıyor.

### Özet tablo (POS)

| Bileşen | Kategori kaynağı |
|---------|-------------------|
| cash-register.tsx + CategoryFilter | `useProductsUnified()` → `getProductCatalog()` veya `getAllCategories()` → **API** |
| ProductList | `categoryFilter` prop + cache’teki ürünler (API) |
| QuickProductSearch / AdvancedProductSearch | `products` üzerinden türetilen kategori seti (API verisi) |
| **ProductSelector** | **Hardcoded** `categories` dizisi |

---

## 2) Backend’de kategori endpoint’leri

### ProductController (POS’un kullandığı)

| Endpoint | Açıklama |
|----------|----------|
| **GET /Product/catalog** | Aktif ürünlerden kategori isimlerini toplar; her biri için deterministik GUID üretir. Dönen yapı: `{ Categories: [{ Id, Name }], Products: [...] }`. Ürünlerde `ProductCategory` (string) ve `CategoryId` (guid) var. |
| **GET /Product/categories** | `Products` tablosundan **distinct** `Category` (string) listesi döner. **categories** tablosuna bakmaz. |
| **GET /Product/category/{categoryName}** | `Product.Category == categoryName` ve `IsActive` olan ürünleri döner. |
| **GET /Product/active** | Aktif ürünleri kategoriye göre gruplu döner. |
| **GET /Product/debug/categories-products** | Debug: kategori listesi ve ürün sayıları. |

Kategoriler **taxRate** içermiyor; vergi bilgisi ürün seviyesinde (`TaxType`, `TaxRate`).

### CategoriesController (Admin’in kullandığı)

| Endpoint | Açıklama |
|----------|----------|
| **GET /api/Categories** | `categories` tablosundan aktif kayıtlar (Name, Description, Color, Icon, SortOrder, IsActive, Id, …). |
| **GET /api/Categories/{id}** | Tek kategori. |
| **POST /api/Categories** | Yeni kategori (Admin/Manager). |
| **PUT /api/Categories/{id}** | Kategori güncelleme. |
| **DELETE /api/Categories/{id}** | Soft delete (Administrator). |
| **GET /api/Categories/{id}/products** | İlgili kategori **adına** sahip ürünler (`Product.Category == category.Name`). |
| **GET /api/Categories/search?query=** | İsim/açıklama ile arama. |

`Category` modelinde **taxRate** yok (Name, Description, Color, Icon, SortOrder).

---

## 3) Ürün–kategori ilişkisi

- **Product** modeli:
  - **`Category`** (string): Zorunlu, max 100 karakter. Filtreleme ve gruplama buna göre.
  - **`CategoryId`** (Guid?, nullable): Var ama ProductController tarafında **doldurulmuyor**; catalog’da kategori ID’si, isimden üretilen deterministik GUID.
- **categories** tablosu:
  - Ayrı bir entity (Id, Name, Description, Color, Icon, SortOrder, IsActive, …).
  - İlişki: Product’ta `CategoryId` FK olarak tanımlı ama **ürün kaydederken** backend’de bu alan set edilmiyor; ürünler **sadece `Category` (string)** ile kategorize ediliyor.
- Sonuç: **Ürünler kategoriye string (`Category`) ile bağlı.** `CategoryId` şu an anlamlı şekilde kullanılmıyor; catalog’daki ID, runtime’da isimden türetiliyor.

---

## 4) Admin’de category management

- **Ayrı sayfa:** `frontend-admin/src/app/(protected)/categories/page.tsx` → **Categories** sayfası var (sidebar’da “Categories” linki).
- **İçerik:** Liste, arama, oluşturma/düzenleme/silme (soft delete), genişletilebilir satırda o kategorideki ürünler (GET /api/Categories/{id}/products).
- **Products sayfasında kategori:**  
  `ProductForm.tsx` içinde:
  - `useCategories().useList()` → **GET /api/Categories** ile kategori listesi alınır.
  - Select’te “Category” alanı bu listeyle doldurulur (label/value = kategori **adı**).
  - Kayıtta **product’a sadece `category` (string)** gönderilir; yani Admin’de seçilen kategori **adı** product’ın `Category` alanına yazılıyor. `CategoryId` kullanılmıyor.

Özet: Admin’de category management var; ürün formunda kategori **api/Categories** listesinden seçiliyor ama backend’e **sadece kategori adı (string)** gidiyor.

---

## 5) Data flow diyagramı

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ADMIN (Next.js)                                                             │
│  • Categories sayfası: GET/POST/PUT/DELETE /api/Categories                  │
│  • Product form: GET /api/Categories → dropdown; kayıtta category: string  │
└───────────────────────────────────┬───────────────────────────────────────┘
                                     │
                                     │  Product create/update: body.category = "Speisen" (string)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  BACKEND                                                                     │
│  • categories tablosu: Admin CRUD (api/Categories)                          │
│  • products tablosu: Product.Category (string), Product.CategoryId (optional)│
│  • POS için: ProductController                                                │
│    - GET /Product/catalog   → categories from Product.Category + products   │
│    - GET /Product/categories → distinct Product.Category                     │
└───────────────────────────────────┬───────────────────────────────────────┘
                                     │
                                     │  GET /Product/catalog (veya fallback GET /Product/categories)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  POS (Expo)                                                                  │
│  • useProductsUnified → getProductCatalog() → categories = catalog.categories  │
│  • CategoryFilter: props.categories (API’den)                               │
│  • ProductSelector: HARCODED categories (tek mock noktası)                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6) Kategori listesi hangi endpoint’ten geliyor?

- **POS ana ekran (tab’lar):**  
  **GET /Product/catalog** (önce tercih), hata olursa **GET /Product/categories**.  
  İkisi de **Product** controller; veri kaynağı **products** tablosundaki `Category` string’leri.
- **Admin Categories sayfası:** **GET /api/Categories** (CategoriesController).
- **Admin Product formu dropdown:** **GET /api/Categories**.

---

## 7) Mock’un kaldırılması (ProductSelector)

- **Dosya:** `frontend/components/ProductSelector.tsx`.
- **Sorun:** `categories` sabit dizi (satır 50–63) kullanılıyor; API’den gelen kategoriler kullanılmıyor.
- **Öneri:**
  1. Aynı akışta kullanılıyorsa `useProductsUnified()` ile categories + getProductsByCategory alınabilir.
  2. Veya `getAllCategories()` / `getProductCatalog()` ile kategori listesi çekilip state’e yazılır; tab’lar bu state’ten render edilir.
  3. Hardcoded dizi kaldırılır; tek kaynak backend (Product/catalog veya Product/categories) olur.

---

## 8) Özet cevaplar

| Soru | Cevap |
|------|--------|
| Kategoriler Admin’den backend’e kaydedilip POS’a API ile mi geliyor? | Admin’de categories tablosu var ve oradan yönetiliyor. POS ise **Product** endpoint’leriyle çalışıyor; kategoriler **ürünlerin Category alanından** türetiliyor. Yani Admin’deki “Categories” ile POS’taki tab’lar **aynı veri kaynağına** (categories tablosuna) bağlı değil; POS sadece product’taki string’e bakıyor. |
| Kategori listesi hangi endpoint’ten? | POS: **GET /Product/catalog** (veya **GET /Product/categories**). Admin: **GET /api/Categories**. |
| Ürünler categoryId ile mi, string mi? | **String:** `Product.Category`. `CategoryId` alanı var ama anlamlı şekilde doldurulmuyor; catalog’daki ID isimden üretilen GUID. |
| Mock nerede? | Sadece **ProductSelector.tsx** içinde hardcoded kategori listesi. Kaldırmak için bu bileşende kategori listesini API’den (catalog veya categories) almak yeterli. |
