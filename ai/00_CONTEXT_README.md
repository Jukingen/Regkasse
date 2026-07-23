# Regkasse AI Context Pack

Bu klasör, AI destekli geliştirme için repo-gerçeklerine dayalı kısa operasyon rehberidir.  
Index / read order: [`README.md`](README.md). Çakışmada **`AGENTS.md` ve kod** kazanır.

## Read this first
- **Ana AI özeti / proje özü:** Repo kökündeki `REGKASSE_AI_ONBOARDING.md` dosyasını önce oku; hedef kullanıcı, RKSV özel fişler, ödeme/offline kuyruk, voucher kuralları, FinanzOnline durumu ve guardrail’ler orada toplanır.
- Bu `/ai` paketi: backend sözleşmesi, veritabanı, API boundary, güvenlik ve “dokunma” listeleri için derinleştirme sağlar.

## Multi-tenant (özet)
- Tek backend, çok kiracı. **Production hosts:** POS `pos.regkasse.at`, FA `admin.regkasse.at`, API `api.regkasse.at` — kiracı **JWT `tenant_id`** (Single POS UI). `{slug}.regkasse.at` **POS girişi değildir**.
- Müşteri web siteleri: `frontend-sites` (`/[slug]` veya verified `TenantDomain` custom Host).
- İzolasyon: `ITenantEntity` + EF global query filter (`ICurrentTenantAccessor`); çapraz kiracı erişim **404**.
- Geliştirme: `X-Tenant-Id` / `?tenant=` (slug), FA’da dev tenant seçici, `admin.regkasse.local` / opsiyonel `dev.regkasse.local`.
- **Singleton + EF:** `AppDbContext` scoped; singleton’lar (`LicenseService`) `IServiceScopeFactory` kullanır — root’tan `IDbContextFactory` çağırma.
- Tam metin: `REGKASSE_AI_ONBOARDING.md` → **Multi-Tenant Architecture**; `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`.

## Monorepo özeti
- `backend/`: ASP.NET Core API (`net10.0`), EF Core + PostgreSQL, Swagger (`backend/swagger.json`).
- `frontend/`: Expo Router mobil POS (React Native, Expo SDK 56).
- `frontend-admin/`: Next.js 16 (App Router) + Ant Design 6 + TanStack Query; auth gate `src/proxy.ts` (eski `middleware.ts` değil).
- `frontend-sites/`: Shared multi-tenant storefront (Next.js 16).
- `localization/`: i18n doğrulama/import-export scriptleri.
- `scripts/`: OpenAPI/Orval ve kritik contract doğrulama scriptleri.

## AI için çalışma önceliği
1. `REGKASSE_AI_ONBOARDING.md` (repo kökü) — güncel ürün ve fiscal özet.
2. En yakın uygulama kodu ve testler.
3. Paket config dosyaları (`*.csproj`, `package.json`, `orval.config.ts`).
4. CI workflow’ları (`.github/workflows/*`).
5. Bu `/ai` dokümanları (dar alan sözleşmeleri).

## Değişiklik güvenlik kuralları
- Küçük ve geri alınabilir değişiklik yap.
- `Cart → Payment → Receipt → DailyClosing` akışını yüksek riskli kabul et.
- TSE/RKSV/FinanzOnline alanlarında davranış değişikliği yapma; sadece isteneni uygula.
- API boundary: Admin `/api/admin/*`, POS `/api/pos/*`, Sites `/api/public/*` + `/api/sites/*` (istisnalar dokümante).
- İki offline sistemi birleştirme; working hours ile POS/FA kapatma.

## Multi-tenant local test (özet)

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
# or: curl "http://localhost:5184/api/health?tenant=dev"
```

`Development` ortamı + slug; bkz. `REGKASSE_AI_ONBOARDING.md` (Development Setup for Multi-Tenant Testing).

## Hızlı doğrulama komutları
- `node scripts/verify-api-client.mjs`
- `node scripts/validate-critical-openapi-paths.mjs`
- `node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error`
- `node localization/scripts/check-translation-boundary.mjs --app frontend-admin`
- `node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json`
