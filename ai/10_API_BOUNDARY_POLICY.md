# API Boundary Policy — Admin & POS

**Status:** Normative (team-wide).  
**Related:** `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`, `ai/08_API_CONTRACT_STABILIZATION_PLAN.md`  
**Last updated:** 2026-03-25

---

## 1. Policy (canonical boundaries)

| Client | Allowed first path segment (after `/api`) | Intent |
|--------|-------------------------------------------|--------|
| **Admin** (`frontend-admin`) | **`admin`** → **`/api/admin/*`** | Operasyonel yönetim, raporlar, kullanıcı yönetimi (RBAC), ürün CRUD, ödeme inceleme/aksiyonlar, incident/replay, bütünlük, vb. |
| **POS** (`frontend`) | **`pos`** → **`/api/pos/*`** | Kasada satış, sepet, POS ödeme, POS kasa seçimi, POS ürün kataloğu, vb. |

**Kural:** Yeni özellik endpoint’leri **doğrudan** bu öneklerden biri altında tanımlanır. İstemci kodu da yalnızca kendi önekine çağrı yapar (aşağıdaki **istisnalar** hariç).

---

## 2. Explicit “no new legacy routes” rule

**Legacy route** bu repoda: aynı işi gören **ikinci** URL ailesi — özellikle:

- `api/Payment` (canonical: `api/pos/payment`)
- `api/Cart` (canonical: `api/pos/cart`)
- `api/Product` (canonical: `api/pos` katalog + `api/admin/products` yönetim)

**Kurallar:**

1. **Yeni** `[Route("api/[controller]")]` veya yeni action **yalnızca** legacy prefix altında **eklenemez**. Yeni işlev **canonical** path altında yapılır.
2. Mevcut legacy alias’lara **yeni action / yeni query parametresi** eklemek **yasak** (genişleme dondurması). İhtiyaç → canonical route’ta tanımla.
3. Swagger/OpenAPI’da yeni operasyonlar legacy prefix altında **review’da reddedilir**; CI’de allowlist dışı legacy path eklentisi hedeflenir (bkz. governance epic).

---

## 3. Exception register (shared & transitional)

Bu path’ler **Admin veya POS öneki değildir**; bilinçli istisnadır. Yeni endpoint eklerken buraya **ek kayıt** veya mevcut aileyi genişletme kararı gerekir.

| Path / aile | Kim kullanır | Gerekçe | Not |
|-------------|--------------|---------|-----|
| `/api/Auth/*` | Admin + POS | Oturum, token | Auth hardening ayrı workstream; boundary dışı tutulabilir |
| `/api/user/settings/*` | Admin + POS | Profil, kasa ataması, dil, TSE kullanıcı ayarları | Paylaşılan “kullanıcı bağlamı” |
| `/api/Receipts/*` | Admin + POS | Fiş listesi / ödeme ile ilişki | Paylaşılan okuma yüzeyi |
| `/api/Invoice/*` | Örn. POS (PDF) | Fatura PDF / ilgili okuma | Paylaşılan |
| `/api/Orders/*` | Admin + POS | Sipariş yaşam döngüsü | İleride `admin`/`pos` altına bölünmeye aday; şimdilik istisna |
| `/api/offline-transactions` | POS / senkron | Offline intent | İstisna veya ileride `api/pos/offline-*` |
| `/api/modifier-groups` | İstemci doğrulaması gerekir | Controller tek prefix kullanıyor | **Gap:** canonical altına taşınmalı veya istisna kaydı güncellenmeli |
| **PascalCase admin API’ler** (`/api/Tse`, `/api/Tagesabschluss`, `/api/UserManagement`, …) | Çoğunlukla Admin | Tarihsel isimlendirme | **Transitional:** yeni iş **mümkünse** `api/admin/*` altında; rename RKSV riski yüksek olanlar **exception** ile kalır (`ai/09` B bölümü) |
| `/api/health`, `/` | Operasyon | Sağlık | İstisna |

**İstisna ekleme süreci:** PR açıklamasında “Exception: …” + bu tabloya satır veya footnote; mimari review.

---

## 4. Engineering guidance — yeni endpoint

1. **Kim çağıracak?** Yalnız Admin → `api/admin/...`; yalnız POS → `api/pos/...`; ikisi → istisna tablosunu güncelle veya domain’i böl.
2. **Controller:** `Route("api/admin/...")` veya `Route("api/pos/...")` — tek prefix, mümkünse tek controller sorumluluğu.
3. **OpenAPI:** DTO + `ProducesResponseType`; `swagger.json` commit; Admin’de `npm run generate:api`.
4. **POS:** Path’leri `frontend/services/api/apiPaths.ts` / ilgili `*Paths.ts` içinde merkezileştir; `swagger.json` ile aynı path string.
5. **Yetki:** Mevcut `HasPermission` / roller; yeni izin adı dokümante.
6. **Legacy:** Yeni dual-route **yok** (bölüm 2).

---

## 5. İstemci başına kontrol listesi

### Admin (`frontend-admin`)

- API çağrıları: `src/api/generated/**` + `src/lib/axios.ts`.
- **`src/api/legacy/*`** yalnızca taşıma süresi; yeni kod **legacy import etmez**.
- Orval transformer (`orval-strip-legacy-paths.cjs`) — legacy strip listesini **büyütme** (daha fazla path gizleme), hedef **istemciyi canonical’a taşımak**.

### POS (`frontend`)

- Çağrılar `services/api/*` ve canonical `/api/pos/*` (gerekirse bölüm 3 istisnaları).
- **`/api/Payment`, `/api/Cart`, `/api/Product`** doğrudan **yasak** (yeni kod); mevcut guard testleri korunur.

---

## 6. Acceptance criteria (this document)

| Criterion | Addressed in |
|-----------|--------------|
| Boundary policy documented | §1 |
| New endpoint rules documented | §4, §5 |
| Explicit “no new legacy routes” | §2 |
| Existing exceptions listed | §3 |

---

## 7. Rollout / enforcement notes

- **Code review:** Checklist maddeleri PR şablonuna kısa madde olarak eklenebilir.
- **CI (hedef):** Legacy prefix’e yeni path; Admin’de yasak pattern grep (incremental).
- **Drift:** `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` periyodik güncelleme sahibi atanmalı.
