# Backend Contract (ASP.NET Core)

## Multi-Tenant Architecture

- **Tanımlama:** `SubdomainTenantProvider` + `TenantHostNames` — üretimde `{slug}.regkasse.at`; geliştirmede `X-Tenant-Id` / `?tenant=` (slug).
- **Middleware:** `TenantResolutionMiddleware` (host → `CurrentTenantService` → accessor), ardından auth sonrası `TenantContextMiddleware` (JWT `tenant_id`).
- **Veri:** `AppDbContext` içinde `ITenantEntity` için global query filter; çapraz kiracı kaynak erişimi **404**.
- **Super Admin:** `Roles.SuperAdmin`, `/api/admin/tenants`, impersonation `POST .../impersonate` — `AdminTenantsController`, `AdminTenantService`.
- **Testler:** `TenantIsolationTests`, `SubdomainTenantProviderTests`, `SettingsTenantResolverTests`.

**Güvenlik:** çapraz kiracı IDOR → 404; üretimde yalnızca subdomain; JWT↔host zorunlu eşleşme henüz yok (`docs/MULTI_TENANT.md`).

**Migration:** dalga migration zinciri; `LegacyDefaultTenantIds.Primary` default Guid — `ai/02_DATABASE_CONTRACT.md`, `docs/MULTI_TENANT.md`.

Ayrıntı: `docs/MULTI_TENANT.md`, `REGKASSE_AI_ONBOARDING.md`, `backend/README.md`.

## API Headers

- Production: subdomain → slug → `CurrentTenantService` → accessor Guid.
- Development: `X-Tenant-Id` / `?tenant=` (slug only; `IsDevelopment()`).
- `/api/admin/tenants`: `[Authorize(Roles = SuperAdmin)]`; global `tenants` table; impersonation for scoped business data.

## Deployment Requirements

### DNS Configuration

- Wildcard A: `*.regkasse.at` → server IP; wildcard TLS; preserve `Host` header at proxy.

### Environment Variables

- `ASPNETCORE_ENVIRONMENT=Development` → header/query tenant overrides allowed.
- `Production` / non-Development → subdomain resolution only (`SubdomainTenantProvider`).

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
