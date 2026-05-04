# Backend Contract (ASP.NET Core)

## Teknik gerçekler
- Framework: ASP.NET Core Web API, controller-based yapı.
- Hedef framework: `net10.0`.
- Veri: EF Core + Npgsql (`AppDbContext : IdentityDbContext<ApplicationUser>`).
- AuthN/AuthZ: JWT + policy/permission sistemi (`HasPermission`, `AddAppAuthorization`).
- OpenAPI: Swashbuckle ile üretilen `backend/swagger.json` contract kaynağıdır.

## Controller ve route kuralları
- Repo hem canonical hem legacy route barındırır.
- **Canonical hedefler (tercih edilen):**
  - Admin: `/api/admin/*`
  - POS: `/api/pos/*`
- RKSV özel fişler ve ilgili admin yüzeyleri: `/api/rksv/*` (yüksek risk; permission ile korunur).
- Legacy alias içeren kritik controller’lar: `PaymentController`, `CartController`, `ProductController`.
- Yeni endpointlerde legacy prefix açma; canonical prefix kullan.

## Yüksek riskli route aileleri (değişiklik öncesi ekstra dikkat)
- `/api/pos/payment*`, `/api/offline-transactions/*`, TSE ve kasa oturumu ile ilgili POS uçları.
- `/api/rksv/*` (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg).
- `/api/admin/fiscal-export*`, FinanzOnline/outbox ile ilgili admin uçları.

## Response/contract kuralları
- Contract değişikliği varsa aynı PR’da `backend/swagger.json` güncel olmalı.
- **Admin client:** `backend/swagger.json` değiştiyse `frontend-admin` Orval üretimini yenile ve `node scripts/verify-api-client.mjs` çalıştır (`ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`).
- Kritik endpointlerde named DTO + `ProducesResponseType` kullan.
- Payment v2 sözleşmesi header opt-in ile desteklenir: `X-Regkasse-Payment-Contract: v2`.

## Güvenlik ve uyumluluk
- Varsayılan yaklaşım: endpoint’leri authorize et, permission ile sınırla.
- **Fiscal otorite:** POS guardrail’leri UX içindir; nihai RKSV/TSE/ödeme kuralları backend’de uygulanır (`REGKASSE_AI_ONBOARDING.md`).
- Yüksek riskli alanlar: ödeme, fiş, imza zinciri, günlük kapanış, TSE imza, RKSV özel fişler, voucher, FinanzOnline/outbox.
- Bu alanlarda davranışsal değişiklik yapmadan önce kapsam ve risk notu açık yazılmalıdır; yasal uyumluluk garantisi iddia edilmez.

## Backend değişiklik checklist
1. İlgili controller/service/DTO dosyalarını tara.
2. Authz etkisini kontrol et (`HasPermission`, role matrix).
3. Contract etkisi varsa swagger diff üret.
4. Gerekliyse migration ekle ve mevcut modelleme stilini koru.
5. İlgili testleri ve script kontrollerini çalıştır.
