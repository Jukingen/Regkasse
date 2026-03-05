# Route Plan: /api/pos ve /api/admin

**Uygulama tarihi:** 2025-03-05  
**Hedef:** POS için `/api/pos/...`, Admin için `/api/admin/...`; mevcut endpoint’ler alias olarak çalışmaya devam ediyor.

---

## 1) Backend’de yapılan değişiklikler (diff özeti)

### Yeni dosyalar

- **`Swagger/PosAdminTagsAndDeprecationFilter.cs`**  
  - Legacy path’leri Swagger’da deprecated işaretler.  
  - `api/pos/*` → **POS** tag, `api/admin/*` → **Admin** tag.

- **`Controllers/AdminProductsController.cs`**  
  - `[Route("api/admin/products")]`  
  - `GET api/admin/products` — list (sayfalama, `categoryId`, `name` araması)  
  - `GET api/admin/products/{id}` — tekil ürün  

### Controller route eklemeleri

| Dosya | Eklenen attribute / not |
|-------|--------------------------|
| **ProductController.cs** | `[Route("api/pos")]` (mevcut `[Route("api/Product")]` yanında). Summary: legacy api/Product deprecated, POS için api/pos, Admin için api/admin/products. |
| **CartController.cs** | `[Route("api/pos/cart")]` (mevcut `[Route("api/[controller]")]` → api/Cart yanında). Summary: legacy api/Cart deprecated, use api/pos/cart. |
| **PaymentController.cs** | `[Route("api/pos/payment")]` (mevcut `[Route("api/[controller]")]` → api/Payment yanında). Summary: legacy api/Payment deprecated, use api/pos/payment. |
| **CategoriesController.cs** | `[Route("api/admin/categories")]` (mevcut `[Route("api/[controller]")]` → api/Categories yanında). Summary: legacy api/Categories deprecated, use api/admin/categories. |

### Program.cs

- `AddSwaggerGen` içinde:  
  `c.OperationFilter<KasseAPI_Final.Swagger.PosAdminTagsAndDeprecationFilter>();`

---

## 2) Alias davranışı (breaking yok)

Aynı action hem eski hem yeni path’te çalışır:

| Eski (legacy, deprecated) | Yeni (tercih edilen) |
|---------------------------|------------------------|
| GET api/Product | GET api/pos |
| GET api/Product/list | GET api/pos/list |
| GET api/Product/catalog | GET api/pos/catalog |
| GET api/Product/all, /active, /categories, /search, /{id}, vb. | GET api/pos/... (aynı segmentler) |
| GET/POST/PUT/DELETE api/Cart/* | GET/POST/PUT/DELETE api/pos/cart/* |
| GET/POST api/Payment/* | GET/POST api/pos/payment/* |
| GET api/Categories | GET api/admin/categories |
| GET api/Categories/{id}, POST, PUT, DELETE | api/admin/categories (aynı action’lar) |

Admin ürünler için yeni route’lar (sadece yeni path):

| Yeni endpoint | Açıklama |
|---------------|----------|
| GET api/admin/products | List (pageNumber, pageSize, categoryId, name) |
| GET api/admin/products/{id} | Tekil ürün |

---

## 3) Swagger’da deprecated ve tag’ler

- **Legacy path’ler** (api/Product, api/Cart, api/Payment, api/Categories) Swagger’da **Deprecated** ve açıklamada “Deprecated. Use /api/pos/... for POS or /api/admin/... for Admin.” metni.
- **Tag’ler:**
  - Path `api/pos` veya `api/pos/*` → **POS**
  - Path `api/admin` veya `api/admin/*` → **Admin**

---

## 4) Hangi endpoint POS, hangisi Admin (tag özeti)

| Tag | Path öneki | Örnek endpoint’ler |
|-----|------------|---------------------|
| **POS** | api/pos | api/pos/catalog, api/pos/all, api/pos/current (cart), api/pos/methods (payment), api/pos/add-item, api/pos/list, api/pos/search, api/pos/{id}, api/pos/cart/current, api/pos/cart/add-item, api/pos/payment/methods, api/pos/payment (POST), vb. |
| **Admin** | api/admin | api/admin/products, api/admin/products/{id}, api/admin/categories, api/admin/categories/{id}, api/admin/categories (POST, PUT, DELETE) |

Legacy path’ler (api/Product, api/Cart, api/Payment, api/Categories) Swagger’da deprecated olarak işaretlenir; tag atanmaz (veya mevcut controller adı kalır).

---

## 5) Frontend tarafı (opsiyonel sonraki adım)

- **POS (Expo):** İstenirse `api/pos`, `api/pos/cart`, `api/pos/payment` kullanacak şekilde güncellenebilir; mevcut api/Product, api/Cart, api/Payment çağrıları çalışmaya devam eder.
- **FE-Admin:** İstenirse ürün listesi/detay için `api/admin/products` ve `api/admin/products/{id}`, kategoriler için `api/admin/categories` kullanacak şekilde güncellenebilir.

Bu belge route planı ve uygulama özetidir; frontend değişikliği zorunlu değildir.
