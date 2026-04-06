# Lager / Inventory — isteğe bağlı kullanım (kısa rehber)

Bu doküman, stok ve Lager yüzeylerini **kapatılmış** dağıtımlar için operasyon özeti verir. Şema ve ürün alanları silinmez; sadece davranış ve UI yapılandırılır.

## Önerilen “Lager kapalı” paketi

1. **API (satış blokajı yok):** `Inventory__EnforceStockAvailability=false`  
   - veya `appsettings` / ortamda `Inventory:EnforceStockAvailability`: `false`  
   - Ödeme sırasında stok kontrolü ve stok düşümü/geri yazımı yapılmaz; fiş akışı stoktan etkilenmez.

2. **Admin — ürün listesi:** `NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER=false`  
   - Ürünler tablosunda Lager kolonu, stok butonu ve düşük stok etiketleri gizlenir.  
   - `frontend-admin` için **build öncesi** ayarlanmalıdır (`next build`).

3. **Admin — Lager modülü:** `NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV=false`  
   - Kenar çubuğunda “Lager” girişi gizlenir; `/inventory` doğrudan açılırsa bilgilendirme mesajı gösterilir, envanter API çağrıları tetiklenmez.  
   - Yine **build öncesi** ayar.

API değişikliğinden sonra API’yi yeniden başlatın; admin tarafında env değiştiyse admin uygulamasını yeniden derleyin.

### Uygulama yenileme gereksinimi (önemli)

| Değişiklik | Ne zaman etkili olur |
|------------|----------------------|
| `Inventory__EnforceStockAvailability` (veya `appsettings`) | API süreci **yeniden başlatıldığında** (ve yapılandırmanın yüklendiğinden emin olun). |
| `NEXT_PUBLIC_ADMIN_*` | **Sonraki** `next dev` / `next build` ile üretilen istemci paketinde; çalışan konteynıra sadece runtime env enjekte etmek **yetmez**. |

## Smoke test checklist (Lager kapalı paket)

Önkoşul: API’de `EnforceStockAvailability=false`, admin `.env.local` veya CI’da `NEXT_PUBLIC_ADMIN_PRODUCTS_SHOW_LAGER=false` ve `NEXT_PUBLIC_ADMIN_SHOW_INVENTORY_NAV=false`; ardından **admin yeniden build**, **API restart**.

- [ ] **Satış:** Stok `0` olan normal ürün (add-on değil) ile POS’tan ödeme tamamlanır; API “Insufficient stock” dönmez.
- [ ] **Ürünler:** `/products` tablosunda **Lager** kolonu yok; satırda **Lager/stock** aksiyon butonu yok.
- [ ] **Sidebar:** Katalog grubunda **Lager / Inventory** menü kalemi yok.
- [ ] **Dashboard:** “Hospitality” hızlı linkler kartında **Stok / Lager** linki yok (env kapalıyken).
- [ ] **Doğrudan URL:** Tarayıcıda `/inventory` açılınca bilgilendirme mesajı gelir; Network’te `/api/Inventory` isteği **yok** (veya sayfa yüklenir yüklenmez tetiklenmez).
- [ ] **API-only:** `EnforceStockAvailability` tekrar `true` yapılıp API restart sonrası aynı stok-0 senaryosu beklenen şekilde reddedilir (regresyon kontrolü).

## Varsayılanlar (geriye dönük uyumluluk)

- API: `EnforceStockAvailability` **true** (önceki stok davranışı).  
- Admin: `NEXT_PUBLIC_*` tanımsız veya `true` → Lager yüzeyleri görünür.

## İlgili dosyalar (geliştirici)

- `backend/Configuration/InventoryOptions.cs`, `PaymentService` stok dalları  
- `frontend-admin/src/shared/config/adminInventoryNavUi.ts`, `buildAdminSidebar.tsx`  
- `frontend-admin/src/features/products/utils/adminProductsLagerUi.ts`, `products/page.tsx`  
- `backend/appsettings.example.json`, `backend/CONFIGURATION.md`
