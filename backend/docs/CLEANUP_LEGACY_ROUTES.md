# Legacy Route Cleanup

Bu dokümanda eski endpoint kullanımı taranmış, kullanılmayan legacy route kaldırılmış; hâlâ kullanılanlar için TODO bırakılmıştır (breaking değişiklik yapılmadı).

---

## 1) Repo tarama sonucu

| Legacy prefix   | Kullanım yeri | Karar |
|-----------------|---------------|--------|
| `api/Categories` | Yok (FE-Admin artık sadece `api/admin/categories`) | **Kaldırıldı** |
| `api/Product`   | FE-Admin `src/api/admin/products.ts` (create/update/delete/stock/modifier-groups), POS `frontend/services/api/apiPaths.ts` | Kaldırılmadı, TODO |
| `api/Cart`      | POS `cartService.ts` (`/cart/current`, `/cart/...`) | Kaldırılmadı, TODO |
| `api/Payment`   | POS `paymentService.ts` (`/Payment`), FE-Admin `signature-debug.ts` | Kaldırılmadı, TODO |

---

## 2) Kaldırılan endpointler

Sadece **api/Categories** ile başlayan tüm path'ler kaldırıldı. Controller artık yalnızca `api/admin/categories` üzerinden erişilebilir; Swagger'dan da `api/Categories` path'leri otomatik düştü.

**Kaldırılan route (tek satır):**

- `[Route("api/[controller]")]` — `CategoriesController` üzerinden gelen `api/Categories`, `api/Categories/{id}`, `api/Categories/search`, `api/Categories/{id}/products` vb.

---

## 3) Kaldırma diff / snippet

### CategoriesController.cs

```diff
     /// <summary>
-    /// Kategori yönetimi. Legacy: api/Categories/* deprecated; use api/admin/categories for Admin.
+    /// Kategori yönetimi. Tek route: api/admin/categories (legacy api/Categories kaldırıldı).
     /// </summary>
     [Authorize]
     [ApiController]
-    [Route("api/[controller]")]
     [Route("api/admin/categories")]
     public class CategoriesController : ControllerBase
```

### PosAdminTagsAndDeprecationFilter.cs

```diff
-    private static readonly string[] LegacyPrefixes = { "api/Product", "api/Cart", "api/Payment", "api/Categories" };
+    // api/Categories kaldırıldı (sadece api/admin/categories kullanılıyor)
+    private static readonly string[] LegacyPrefixes = { "api/Product", "api/Cart", "api/Payment" };
```

---

## 4) Kaldırılamayan legacy route'lar (TODO)

Aşağıdaki route'lar client tarafında hâlâ kullanıldığı için kaldırılmadı; ilgili controller'lara TODO yorumu eklendi.

### api/Product

| Nerede kullanılıyor | Dosya | Neden kaldırılamıyor |
|---------------------|-------|----------------------|
| Ürün create/update/delete, stok, modifier-groups | `frontend-admin/src/api/admin/products.ts` (`LEGACY_PRODUCT = '/api/Product'`) | Admin mutation'lar henüz `api/admin/products` altında yok |
| Katalog, list, search, by-id | `frontend/services/api/apiPaths.ts` (LIST, ALL, CATALOG, SEARCH, BY_ID, MODIFIER_GROUPS, STOCK, vb.) | POS tüm ürün çağrılarını bu path'lerle yapıyor |

**Backend TODO (ProductController.cs):**  
`TODO: api/Product route kaldırılamıyor — FE-Admin (admin/products.ts mutations) ve POS (apiPaths) hâlâ kullanıyor. Tüm client'lar api/pos veya api/admin/products'a geçince kaldır.`

---

### api/Cart

| Nerede kullanılıyor | Dosya | Neden kaldırılamıyor |
|---------------------|-------|----------------------|
| getCurrentCart, createCart, addItemToCart, getCart, vb. | `frontend/services/api/cartService.ts` — `apiClient.get('/cart/current?...')`, `apiClient.post('/cart/...')` | POS baseURL + `/cart/...` ile api/Cart'a gidiyor |

**Backend TODO (CartController.cs):**  
`TODO: api/Cart route kaldırılamıyor — POS cartService /cart/current vb. ile hâlâ kullanıyor. POS api/pos/cart'a geçince kaldır.`

---

### api/Payment

| Nerede kullanılıyor | Dosya | Neden kaldırılamıyor |
|---------------------|-------|----------------------|
| getPaymentMethods, processPayment, getPaymentById, qr.png, refund, cancel, vb. | `frontend/services/api/paymentService.ts` — `baseUrl = '/Payment'` | POS tüm ödeme çağrıları api/Payment kullanıyor |
| QR PNG | `frontend/services/receiptPrinter.ts`, `frontend/components/PaymentModal.tsx` | `/api/Payment/{id}/qr.png` referansı |
| Payment methods fetch | `frontend/hooks/useOptimizedDataFetching.ts` — `fetch('/api/Payment/methods')` | Doğrudan legacy path |
| Signature debug | `frontend-admin/src/features/receipts/api/signature-debug.ts` — `url: \`/api/Payment/${paymentId}/signature-debug\`` | Admin receipt debug |

**Backend TODO (PaymentController.cs):**  
`TODO: api/Payment route kaldırılamıyor — POS paymentService ve FE-Admin signature-debug hâlâ kullanıyor. Client'lar api/pos/payment'a geçince kaldır.`

---

## 5) Özet

- **Kaldırılan:** `api/Categories` (tek route attribute; Swagger'dan da düştü).
- **Kaldırılmayan:** `api/Product`, `api/Cart`, `api/Payment` — kullanım yerleri yukarıda; breaking olmaması için route'lar korundu ve controller'lara TODO eklendi.

Sonraki adım: POS ve FE-Admin'i sırayla `api/pos/*` ve `api/admin/*` path'lerine taşıyıp, tüm client'lar geçtikten sonra bu legacy route'ları kaldırmak.
