# Backend Contract (ASP.NET Core)

## Teknik gerçekler
- Framework: ASP.NET Core Web API, controller-based yapı.
- Hedef framework: `net10.0`.
- Veri: EF Core + Npgsql (`AppDbContext : IdentityDbContext<ApplicationUser>`).
- AuthN/AuthZ: JWT + policy/permission sistemi (`HasPermission`, `AddAppAuthorization`).
- OpenAPI: Swashbuckle ile üretilen `backend/swagger.json` contract kaynağıdır.

## Controller ve route kuralları
- Repo hem canonical hem legacy route barındırır.
- Canonical hedefler:
  - Admin: `/api/admin/*`
  - POS: `/api/pos/*`
- Legacy alias içeren kritik controller’lar: `PaymentController`, `CartController`, `ProductController`.
- Yeni endpointlerde legacy prefix açma; canonical prefix kullan.

## Response/contract kuralları
- Contract değişikliği varsa aynı PR’da `backend/swagger.json` güncel olmalı.
- Kritik endpointlerde named DTO + `ProducesResponseType` kullan.
- Payment v2 sözleşmesi header opt-in ile desteklenir: `X-Regkasse-Payment-Contract: v2`.

## Güvenlik ve uyumluluk
- Varsayılan yaklaşım: endpoint’leri authorize et, permission ile sınırla.
- Yüksek riskli alanlar: ödeme, fiş, günlük kapanış, TSE imza, FinanzOnline.
- Bu alanlarda davranışsal değişiklik yapmadan önce kapsam ve risk notu açık yazılmalıdır.

## Backend değişiklik checklist
1. İlgili controller/service/DTO dosyalarını tara.
2. Authz etkisini kontrol et (`HasPermission`, role matrix).
3. Contract etkisi varsa swagger diff üret.
4. Gerekliyse migration ekle ve mevcut modelleme stilini koru.
5. İlgili testleri ve script kontrollerini çalıştır.
