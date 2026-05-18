# OpenAPI Contract Governance

**İlişkili:** Proje davranış özeti `REGKASSE_AI_ONBOARDING.md` (API listeleri ve guardrail’ler); bu dosya üretim/senkron sürecine odaklanır.

## Source of truth
- `backend/swagger.json` API contract kaynağıdır.
- Backend controller/DTO implementation bu dosya ile uyumlu olmalıdır.
- Admin generated client (`frontend-admin/src/api/generated/**`) bu dosyadan türetilir.

## Required workflow
1. API davranışını backend’de güncelle.
2. `backend/swagger.json` güncelle.
3. Admin için Orval üretimini yenile.
4. Contract scriptlerini çalıştır.

**Tenancy / DI (backend-only):** Singleton servislerde EF erişimi `IServiceScopeFactory` ile — `LicenseService`; contract değişikliği değil, `REGKASSE_AI_ONBOARDING.md` mimari notu.

## Required checks
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
- CI: `.github/workflows/api-client-alignment.yml`
- CI: `.github/workflows/api-contract-tests.yml`

## Multi-Tenant Architecture

- Swagger’da admin tenant ve auth `tenant_id` alanları contract parçasıdır; breaking change etiketle.
- Orval sonrası admin Super Admin tipleri (`adminTenants`, tenant DTO’lar) senkron kalmalı.

## Review rules
- Contract etkili PR’larda swagger diff incelemesi zorunlu.
- Breaking change açıkça etiketlenmeli (hangi consumer etkileniyor belirtilmeli).
- Legacy prefix altına yeni operasyon eklenmesi reddedilmeli (istisna dokümantasyonu yoksa).

## Admin Orval notes
- Input: `../backend/swagger.json`
- Config: `frontend-admin/orval.config.ts`
- Transformer: `frontend-admin/scripts/orval-strip-legacy-paths.cjs`
- Generated dosyalara elle müdahale edilmez.
