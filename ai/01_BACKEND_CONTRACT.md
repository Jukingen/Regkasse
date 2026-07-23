# Backend Contract (ASP.NET Core)

## Multi-Tenant Architecture

- **Tanımlama:** Üretim hedefi **Single POS UI** — `pos.regkasse.at` / `api.regkasse.at` / `admin.regkasse.at`; kiracı JWT `tenant_id` (`docs/POS_PRODUCTION_ARCHITECTURE.md`). `{slug}.regkasse.at` **POS girişi değildir** (legacy / geçiş veya müşteri sitesi Host’u olabilir). Geliştirmede `X-Tenant-Id` / `?tenant=` (slug).
- **Middleware:** `TenantResolutionMiddleware` — Dev override → reserved `pos`/`api`/`admin`/`www` için pre-auth bind atlanır → aksi halde Host (`SubdomainTenantProvider`, `TenantDomain` / custom site host) → `CurrentTenantService` → accessor; ardından auth sonrası `TenantContextMiddleware` (JWT `tenant_id` — POS/API için otoriter).
- **Veri:** `AppDbContext` içinde `ITenantEntity` için global query filter; çapraz kiracı kaynak erişimi **404**.
- **Super Admin:** `Roles.SuperAdmin`, `/api/admin/tenants`, impersonation `POST .../impersonate` — `AdminTenantsController`, `AdminTenantService`.
- **Testler:** `TenantIsolationTests`, `SubdomainTenantProviderTests`, `SettingsTenantResolverTests`.

**Güvenlik:** çapraz kiracı IDOR → 404; Production’da `X-Tenant-Id` yok; reserved hostlar (`pos`/`api`/`admin`/`www`) slug değildir. Ayrıntı: `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`.

**Migration:** dalga migration zinciri; `LegacyDefaultTenantIds.Primary` default Guid — `ai/02_DATABASE_CONTRACT.md`, `docs/MULTI_TENANT.md`.

Ayrıntı: `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`, `REGKASSE_AI_ONBOARDING.md`, `backend/README.md`.

### Scoped service resolution (singleton + DbContext)

- `AppDbContext` ve `ICurrentTenantAccessor` **scoped** kayıtlıdır.
- Singleton servisler (`LicenseService`) veritabanı için **`IServiceScopeFactory.CreateScope()`** kullanmalı; scope içinden `IDbContextFactory<AppDbContext>` veya `AppDbContext` çözülmeli.
- Root provider üzerinden `CreateDbContext()` → `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider`.
- `AppDbContext`: design-time ctor (`options` only, migrations); runtime ctor `[ActivatorUtilitiesConstructor]` + `ICurrentTenantAccessor`.
- `OnConfiguring`: yapılandırılmış options varsa provider çağrısı yok (`IsConfigured` guard).

### Tenant / startup (background)

- HTTP yokken accessor `TenantId` null olabilir → `ITenantEntity` filtreleri kapalı (kasıtlı yollar).
- `activated_licenses` kiracı-scoped değil; makine fingerprint ile okunur.
- `LicenseService`: singleton snapshot + scope ile DB; startup DB hatası trial’a düşer, host’u durdurmaz.
- **Billing mandant license:** `Services.Billing.TenantLicenseService` — `IDbContextFactory` (scoped factory pattern); iki adet `ITenantLicenseService` (AdminTenants vs Billing) — DI alias zorunlu. Audit: `IBillingAuditService`; reminders: `IReminderService` + `BillingReminderHostedService`. Bkz. `docs/BILLING_TENANT_LICENSE.md`, `ai/modules/billing_license.md`.

## API Headers

- Production (POS/API): JWT `tenant_id` after login; reserved hosts `pos` / `api` / `admin` / `www` are not tenant slugs.
- Host slug / `TenantDomain`: optional legacy slug host or **customer websites** (`frontend-sites`) — not the POS production entry.
- Development: `X-Tenant-Id` / `?tenant=` (slug only; `IsDevelopment()`).
- `/api/admin/tenants`: `[Authorize(Roles = SuperAdmin)]`; global `tenants` table; impersonation for scoped business data.
- Public / sites (non-POS, non-admin storefront): `/api/public/*`, `/api/sites/*` — see `docs/DIGITAL_SERVICES.md`, `docs/WORKING_HOURS.md`.

## Deployment Requirements

### DNS Configuration

- `pos.regkasse.at`, `admin.regkasse.at`, `api.regkasse.at`; optional `*.regkasse.at` for legacy; preserve `Host` at proxy. See `docs/POS_PRODUCTION_ARCHITECTURE.md`.

### Environment Variables

- `ASPNETCORE_ENVIRONMENT=Development` → header/query tenant overrides allowed.
- `Production` / non-Development → no `X-Tenant-Id` / `?tenant=`; POS tenant from JWT.

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
- `/api/pos/payment*`, TSE ve kasa oturumu ile ilgili POS uçları.
- Offline **iki sistem** (birleştirme): `/api/offline-transactions/*` (legacy TSE intents) vs `/api/pos/offline-orders/*` + `/api/admin/offline-orders/*` (full snapshots).
- `/api/rksv/*` (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg).
- `/api/admin/fiscal-export*`, `/api/admin/rksv/dep-export*`, FinanzOnline/outbox.
- Backup/restore: `/api/admin/backup*` (Tenant vs System; no production restore).

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
