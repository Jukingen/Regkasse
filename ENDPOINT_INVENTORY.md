# Endpoint Envanteri

**Tarih:** 2025-03-05  
**Amaç:** Backend route listesi ve FE-Admin / POS (Expo) tarafında hangi endpoint’lerin nerede kullanıldığının özeti. Kod değişikliği yok, sadece envanter.

---

## 1) Backend – Tüm Controller Route’ları

Base URL öneki yok; controller’lar `[Route("api/...")]` ile tanımlı. `[controller]` = sınıf adından "Controller" çıkarılmış hali (örn. `CategoriesController` → `api/Categories`).

### Auth — `api/Auth`
| Method | Path | Açıklama |
|--------|------|----------|
| POST | api/Auth/login | Login |
| POST | api/Auth/logout | Logout |
| GET | api/Auth/me | Mevcut kullanıcı |
| POST | api/Auth/refresh | Token yenileme |
| POST | api/Auth/register | Kayıt |

### Product — `api/Product`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Product | Sayfalı liste (base) |
| GET | api/Product/list | Sayfalı liste (categoryId ile) |
| GET | api/Product/all | Tüm aktif ürünler |
| GET | api/Product/catalog | Katalog (kategoriler + ürünler) |
| GET | api/Product/active | Aktif ürünler |
| GET | api/Product/debug/categories-products | Debug |
| GET | api/Product/categories | Kategori listesi |
| GET | api/Product/category/{categoryName} | Kategoriye göre ürünler |
| GET | api/Product/stock/{status} | Stok durumu |
| GET | api/Product/search | Arama |
| GET | api/Product/{id}/modifier-groups | Ürün modifier grupları |
| POST | api/Product/{id}/modifier-groups | Modifier grupları güncelle |
| POST | api/Product | Yeni ürün (base) |
| PUT | api/Product/{id} | Ürün güncelle |
| PUT | api/Product/stock/{id} | Stok güncelle |
| GET | api/Product/{id} | Tekil (EntityController) |

### Categories — `api/Categories`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Categories | Tüm kategoriler |
| GET | api/Categories/{id} | Tekil kategori |
| GET | api/Categories/{id}/products | Kategorinin ürünleri |
| GET | api/Categories/search | Arama |
| POST | api/Categories | Yeni kategori |
| PUT | api/Categories/{id} | Kategori güncelle |
| DELETE | api/Categories/{id} | Kategori sil |

### Payment — `api/Payment`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Payment/methods | Ödeme yöntemleri |
| GET | api/Payment/{id} | Ödeme detayı |
| GET | api/Payment/customer/{customerId} | Müşteri ödemeleri |
| GET | api/Payment/method/{paymentMethod} | Yönteme göre |
| GET | api/Payment/date-range | Tarih aralığı |
| GET | api/Payment/statistics | İstatistikler |
| GET | api/Payment/{id}/qr.png | QR PNG |
| GET | api/Payment/{id}/qr.svg | QR SVG |
| GET | api/Payment/{id}/receipt | Fiş verisi |
| GET | api/Payment/{id}/signature-debug | İmza debug |
| POST | api/Payment | Yeni ödeme |
| POST | api/Payment/{id}/cancel | İptal |
| POST | api/Payment/{id}/refund | İade |
| POST | api/Payment/{id}/tse-signature | TSE imza |
| POST | api/Payment/verify-signature | İmza doğrula |

### Receipts — `api/Receipts`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Receipts/{receiptId} | Fiş detayı |
| POST | api/Receipts/create-from-payment/{paymentId} | Ödemeden fiş oluştur |
| GET | api/Receipts/{receiptId}/signature-debug | İmza debug |

### Invoice — `api/Invoice`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Invoice/list | Sayfalı liste (admin) |
| GET | api/Invoice/pos-list | POS kaynaklı liste |
| GET | api/Invoice/export | Export |
| GET | api/Invoice/{id} | Tekil |
| GET | api/Invoice/{id}/pdf | PDF |
| GET | api/Invoice/search | Arama |
| GET | api/Invoice/status/{status} | Duruma göre |
| POST | api/Invoice | Yeni fatura (backend’de mevcut) |
| PUT | api/Invoice/{id} | Güncelle |
| DELETE | api/Invoice/{id} | Sil |
| POST | api/Invoice/{id}/duplicate | Kopyala |
| POST | api/Invoice/{id}/credit-note | İade faturası |
| POST | api/Invoice/backfill-from-payments | Backfill |

### Cart — `api/Cart`
| Method | Path | Açıklama |
|--------|------|----------|
| GET | api/Cart/current | Güncel sepet (tableNumber query) |
| GET | api/Cart/{cartId} | Sepet detayı |
| GET | api/Cart/history | Sepet geçmişi |
| GET | api/Cart/table-orders-recovery | Masa sipariş kurtarma |
| POST | api/Cart/add-item | Sepete ürün ekle |
| POST | api/Cart/{cartId}/items | Sepete ürün ekle (alternatif) |
| POST | api/Cart/{cartId}/clear-items | Sepet kalemlerini temizle |
| POST | api/Cart/clear | Masa sepetini temizle (tableNumber) |
| POST | api/Cart/clear-all | Tüm sepetleri temizle |
| POST | api/Cart/{cartId}/reset-after-payment | Ödeme sonrası sıfırla |
| POST | api/Cart/{cartId}/complete | Sepeti tamamla |
| POST | api/Cart/force-cleanup | Zorla temizlik |
| POST | api/Cart/items/{itemId}/increment | Adet artır |
| POST | api/Cart/items/{itemId}/decrement | Adet azalt |
| PUT | api/Cart/items/{itemId} | Kalem güncelle |
| PUT | api/Cart/{cartId}/items/{itemId} | Kalem güncelle (alternatif) |
| DELETE | api/Cart/{cartId}/items/{itemId} | Kalem sil |
| DELETE | api/Cart/items/{itemId} | Kalem sil (alternatif) |
| DELETE | api/Cart/{cartId} | Sepeti sil |

### Diğer controller’lar (kısa)
- **ModifierGroups** — `api/modifier-groups`: GET (liste), GET {id}, POST, PUT {id}, DELETE {id}, POST {groupId}/modifiers  
- **UserSettings** — `api/user/settings`: GET, PUT, PUT language, PUT cash-register, PUT tse, PUT security, POST reset  
- **UserManagement** — `api/UserManagement`: GET, GET {id}, GET roles, GET search, POST, POST roles, PUT {id}, PUT {id}/password, PUT {id}/reset-password, DELETE {id}  
- **Table** — `api/Table`: GET, GET {id}, POST {id}/status  
- **Settings** — `api/Settings`: GET tax-rates, PUT tax-rates, GET backup, POST backup/now, GET notifications, PUT notifications, GET export  
- **Orders** — `api/Orders`: GET, GET {id}, GET status/{status}, POST, PUT {id}/status, DELETE {id}  
- **Localization** — `api/Localization`: GET, PUT, GET languages, GET currencies, GET timezones, GET format/{language}, GET currency/{currency}, POST add-language, POST add-currency, DELETE remove-language/{language}, DELETE remove-currency/{currency}, GET export  
- **MultilingualReceipt** — `api/MultilingualReceipt`: GET, GET {id}, GET language/{language}, GET type/{type}, PUT {id}, DELETE {id}, POST generate, GET preview/{id}, GET export  
- **Inventory** — `api/Inventory`: GET, GET {id}, GET low-stock, GET transactions/{id}, POST, PUT {id}, POST {id}/restock, POST {id}/adjust, DELETE {id}  
- **FinanzOnline** — `api/FinanzOnline`: GET config, PUT config, GET status, GET errors, GET history/{invoiceId}, POST submit-invoice, POST test-connection  
- **Customer** — `api/Customer`: GET, GET {id}, POST (EntityController), GET number/{customerNumber}, GET email/{email}, GET tax/{taxNumber}, GET search, PUT {id}  
- **CompanySettings** — `api/CompanySettings`: GET, PUT, GET business-hours, PUT business-hours, GET banking, PUT banking, GET localization, PUT localization, GET billing, PUT billing, GET export  
- **CashRegister** — `api/CashRegister`: GET, GET {id}, POST {id}/open, POST {id}/close, GET {id}/transactions  
- **Tse** — `api/Tse`: GET status, GET devices, POST connect, POST signature, POST disconnect  
- **Tagesabschluss** — `api/Tagesabschluss`: POST daily, POST monthly, POST yearly, GET history, GET can-close/{cashRegisterId}, GET statistics  
- **Reports** — `api/Reports`: GET sales, GET products, GET customers, GET inventory, GET payments, GET export/sales  
- **AuditLog** — `api/AuditLog`: GET (liste/export/statistics vb. action’a göre)  
- **Test** — `api/Test`: POST quick-payment  

### Program.cs (minimal API)
| Method | Path | Açıklama |
|--------|------|----------|
| GET | / | "Kasse API is running!" |
| GET | /health | "OK" (api prefix yok) |

Not: Frontend’de `/api/health` çağrılıyorsa backend’de böyle bir route yok; doğru path `/health`.

---

## 2) FE-Admin – Çağrılan endpoint’ler

### Orval config
- **Dosya:** `frontend-admin/orval.config.ts`  
- **Input:** `../backend/swagger.json`  
- **Output:** `src/api/generated/` (mode: tags-split), client: react-query, mutator: `src/lib/axios.ts` (customInstance)

### Generated hooks (örnek path’ler)
Generated dosyalar `src/api/generated/<tag>/<tag>.ts` içinde `getApi*`, `useGetApi*`, `usePostApi*` vb. export eder. URL’ler `/api/Product`, `/api/Categories`, `/api/Invoice` vb. (Swagger’dan gelir).

### React-query / Orval kullanımı (dosya + kısa not)

| Endpoint (özet) | Nerede kullanılıyor | Dosya |
|-----------------|---------------------|--------|
| GET /api/CompanySettings, PUT /api/CompanySettings | Ayarlar sayfası | `src/app/(protected)/settings/page.tsx` — useGetApiCompanySettings, usePutApiCompanySettings |
| GET /api/AuditLog | RKSV doğrulamaları | `src/app/(protected)/rksv/verifications/page.tsx` — useGetApiAuditLog |
| GET /api/Tse/status, GET /api/FinanzOnline/status | RKSV durum | `src/app/(protected)/rksv/status/page.tsx` — useGetApiTseStatus, useGetApiFinanzOnlineStatus |
| GET /api/FinanzOnline/status, GET /api/FinanzOnline/errors | FinanzOnline kuyruk | `src/app/(protected)/rksv/finanz-online-queue/page.tsx` |
| GET /api/Tse/status, GET /api/Tse/devices | CMC sertifika | `src/app/(protected)/rksv/cmc-certificate/page.tsx` |
| GET /api/Categories, POST /api/Categories, GET /api/Categories/{id}/products, GET /api/Categories/search | Kategoriler | `src/features/categories/hooks/useCategories.ts` — useGetApiCategories, usePostApiCategories, useGetApiCategoriesIdProducts, useGetApiCategoriesSearch |
| GET /api/Product, GET /api/Product/search, GET /api/Product/{id}, POST /api/Product, PUT /api/Product/{id}, PUT /api/Product/stock/{id} | Ürünler | `src/features/products/hooks/useProducts.ts` — useGetApiProduct, useGetApiProductSearch, useGetApiProductId, usePostApiProduct, usePutApiProductId, usePutApiProductIdStock |
| GET /api/Categories (form için) | Ürün formu kategorileri | `src/features/products/components/ProductForm.tsx` — yorumda useGetApiCategories |
| GET /api/Invoice/{id} | Fatura detayı (modal) | `src/features/invoices/components/InvoiceList.tsx` — useGetApiInvoiceId |
| POST /api/FinanzOnline/submit-invoice | Fatura gönderimi | `src/features/invoices/components/InvoiceList.tsx` — usePostApiFinanzOnlineSubmitInvoice, postApiFinanzOnlineSubmitInvoice |
| GET /api/Invoice/list, GET /api/Invoice/pos-list, GET /api/Invoice/export, GET /api/Invoice/{id}/pdf, POST /api/Invoice/{id}/credit-note | Liste, export, PDF, iade | `src/features/invoices/api/invoiceService.ts` — getInvoicesList (list), getInvoicesList (pos-list parametreyle), exportInvoices, getInvoicePdf, createCreditNote; customInstance ile `/api/Invoice/*` |
| POST /api/Auth/logout | (Yorumda, kullanılmıyor) | `src/app/(protected)/layout.tsx` — usePostApiAuthLogout yorumda; logout useAuth ile yapılıyor |

Model/tipler: `Product`, `Category`, `Invoice`, `CompanySettings` vb. birçok yerde `@/api/generated/model` veya `@/api/generated/model/...` üzerinden kullanılıyor (sadece tip; endpoint değil).

---

## 3) POS (Expo) – Çağrılan endpoint’ler

Base URL: `config.ts` → `EXPO_PUBLIC_API_BASE_URL` veya `http://localhost:5183/api` / `http://<ip>:5183/api`.  
`apiClient` (axios) `baseURL: API_BASE_URL` ile istek atar; path’ler buna eklenir (örn. `/Payment/methods` → `{baseURL}/Payment/methods`).

| Method | Path | Nerede kullanılıyor | Dosya |
|--------|------|----------------------|--------|
| GET | /health | Sağlık kontrolü (doğrudan IP:5183) | `utils/networkUtils.ts` — fetch `http://${ipAddress}:5183/api/health` (backend’de /api yok, /health var) |
| GET | /health | useApiManager sağlık | `hooks/useApiManager.ts` — healthUrl = apiRoot + '/health' (apiRoot = baseURL’den /api kaldırılmış) |
| POST | /Auth/login | Giriş | `services/api/authService.ts` — apiClient.post('/auth/login') (küçük; backend api/Auth) |
| GET | /Auth/me | Mevcut kullanıcı | `services/api/authService.ts` — apiClient.get('/auth/me') |
| POST | /Auth/refresh | Token yenileme | `services/api/authService.ts` — apiClient.post('/auth/refresh') |
| POST | /Auth/logout | Logout (hardcoded URL) | `contexts/AuthContext.tsx` — fetch('http://localhost:5183/api/auth/logout') |
| GET | /Payment/methods | Ödeme yöntemleri | `services/api/paymentService.ts` — baseUrl /Payment; `hooks/usePaymentMethods.ts` — fetch(API_BASE_URL + '/Payment/methods') |
| POST | /Payment | Ödeme oluştur | `services/api/paymentService.ts` — processPayment |
| GET | /Payment/{id} | Ödeme detayı | `services/api/paymentService.ts` — getReceipt/createReceipt içinde |
| GET | /Payment/{id}/receipt | Fiş verisi | `services/api/paymentService.ts` — getReceipt; `components/PaymentModal.tsx` — paymentService.getReceipt |
| GET | /Payment/{id}/qr.png | QR PNG (tam URL ile fetch) | `services/api/paymentService.ts` — getQrPngAsBase64: `${API_BASE_URL}/Payment/${paymentId}/qr.png` |
| POST | /Payment/{id}/tse-signature | TSE imza | `services/api/paymentService.ts` — createReceipt içinde |
| GET | /Cart/current | Güncel sepet | `services/api/cartService.ts` — getCurrentCart: `/cart/current?tableNumber=` (küçük; backend api/Cart) |
| GET | /Cart/{cartId} | Sepet detayı | `services/api/cartService.ts` |
| POST | /Cart/add-item | Sepete ürün ekle | `services/api/cartService.ts` |
| POST | /Cart/{cartId}/items | Sepete ürün ekle | `services/api/cartService.ts` |
| PUT | /Cart/items/{itemId} | Kalem güncelle | `services/api/cartService.ts` |
| DELETE | /Cart/items/{itemId} | Kalem sil | `services/api/cartService.ts` |
| POST | /Cart/{cartId}/clear-items | Sepet kalemlerini temizle | `services/api/cartService.ts` |
| POST | /Cart/clear | Masa sepetini temizle | `services/api/cartService.ts` — clearCart |
| POST | /Cart/clear-all | Tüm sepetleri temizle | `services/api/cartService.ts` — clearAllCarts |
| POST | /Cart/{cartId}/reset-after-payment | Ödeme sonrası sıfırla | `services/api/cartService.ts`; `components/PaymentModal.tsx` — cartService.resetCartAfterPayment |
| POST | /Cart/{cartId}/complete | Sepeti tamamla | `services/api/cartService.ts`; `components/PaymentModal.tsx` — cartService.completeCart |
| DELETE | /Cart/{cartId} | Sepeti sil | `services/api/cartService.ts` |
| GET | /Cart/history | Sepet geçmişi | `services/api/cartService.ts` |
| GET | /Cart/table-orders-recovery | Masa kurtarma | `services/api/tableOrdersRecoveryService.ts` — apiClient.get('/cart/table-orders-recovery'); `hooks/useOptimizedDataFetching.ts` — fetch('/api/cart/table-orders-recovery') (baseURL’e bağlı) |
| GET | /Product/all, /active, /categories, /catalog, /category/{name}, /search | Ürün/katalog | `services/api/productService.ts` — API_PATHS.PRODUCT; `hooks/useProductsUnified.ts` — getAllProducts, getAllCategories, getProductCatalog |
| GET | /Product/{id}/modifier-groups | Modifier grupları | `services/api/productModifiersService.ts` — API_PATHS.PRODUCT.MODIFIER_GROUPS(productId); `apiPaths.ts` |
| GET | /Customer (veya /Customer/{id}) | Müşteri / misafir | `services/api/customerService.ts` — baseUrl /Customer; getById(GUEST_CUSTOMER_ID); `components/PaymentModal.tsx` — customerService.getGuestCustomer |
| GET | /user/settings, PUT /user/settings, vb. | Kullanıcı ayarları | `services/api/userSettingsService.ts` — /user/settings (backend api/user/settings) |
| POST | /Tagesabschluss/daily, GET /Tagesabschluss/history, GET /Tagesabschluss/can-close/{id}, GET /Tagesabschluss/statistics | Gün sonu | `services/api/tagesabschlussService.ts` |
| GET | /Tse/status, GET /Tse/devices, POST /Tse/connect, POST /Tse/disconnect, POST /Tse/signature | TSE | `services/api/tseService.ts` |
| GET | /Invoice/{id}/pdf, GET /Invoice/{id}/csv | Fatura PDF/CSV (POS) | `services/api/invoiceService.ts` — fetch(API_BASE_URL + '/invoice/...') (küçük; backend api/Invoice) |
| GET | /api/pending-invoices, POST /api/pending-invoices/submit | Bekleyen faturalar | `services/api/pendingInvoicesService.ts` — backend’de böyle bir controller bulunmadı (muhtemelen farklı servis) |
| GET | /api/orders, POST, GET /api/orders/{id}, PUT /api/orders/{id}/status, DELETE | Siparişler | `services/api/orderService.ts` — secureApiService ile `/api/orders` (backend api/Orders) |

Not: POS’ta path’ler bazen küçük harf (`/cart/`, `/auth/`, `/invoice/`). ASP.NET Core varsayılan routing genelde case-insensitive olduğu için çalışıyor olabilir; yine de backend’de tanımlı olan PascalCase path’ler (api/Cart, api/Auth, api/Invoice) referans alınmalı.

---

## Özet tablo (Product / Categories / Payments / Receipts)

| Backend (method + path) | FE-Admin kullanımı | POS kullanımı |
|------------------------|--------------------|---------------|
| GET api/Product, list, all, catalog, active, categories, category/{name}, search, {id}, {id}/modifier-groups | useProducts (list, search, detail), ProductForm (categories), modifierGroups API | productService (all, active, categories, catalog, category, search), productModifiersService (modifier-groups), useProductsUnified |
| POST api/Product, PUT api/Product/{id}, PUT api/Product/stock/{id} | useProducts (create, update, updateStock) | — |
| GET/POST/PUT/DELETE api/Categories, api/Categories/{id}, api/Categories/{id}/products, api/Categories/search | useCategories, ProductForm | — (POS sadece Product/categories/catalog kullanıyor) |
| GET api/Payment/methods, GET api/Payment/{id}, GET api/Payment/{id}/receipt, GET api/Payment/{id}/qr.png, POST api/Payment, POST api/Payment/{id}/tse-signature | (Orval payment tag’i var; doğrudan sayfa kullanımı bu envanterde listelenmedi) | paymentService, usePaymentMethods, PaymentModal (getReceipt, getQrPngAsBase64, processPayment) |
| GET api/Receipts/{id}, POST api/Receipts/create-from-payment/{paymentId}, GET api/Receipts/{id}/signature-debug | Orval receipts hooks (generated) | Fiş verisi için Payment/{id}/receipt kullanılıyor; doğrudan Receipts controller kullanımı bu envanterde yok |

Bu belge yalnızca envanter amaçlıdır; kod değişikliği yapılmamıştır.
