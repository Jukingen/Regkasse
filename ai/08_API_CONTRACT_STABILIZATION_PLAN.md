# API Contract Stabilization Plan

**Status:** Active, incremental (no big-bang rewrite).

**Context:** Üst seviye davranış ve RKSV/voucher kuralları için `REGKASSE_AI_ONBOARDING.md` ana özettir; bu dosya route/OpenAPI stabilize iş kuyruğuna odaklanır.

## Multi-Tenant Architecture

- Yeni admin uçları kiracı bağlamını bozmamalı; Super Admin yüzeyi `/api/admin/tenants` altında kalır.
- OpenAPI/swagger değişikliklerinde `tenant_id` claim ve admin tenant DTO’ları diff’te kontrol edilir.

## Current repository facts
- Canonical boundaries exist: `/api/admin/*` and `/api/pos/*`.
- Legacy aliases for `Payment`, `Cart`, `Product` were **hard-removed** (2026-08-13). See `docs/API_LEGACY_DEPRECATION.md`.
- OpenAPI contract checks run via `scripts/validate-critical-openapi-paths.mjs` and `scripts/verify-api-client.mjs`.

## Stabilization goals
1. Stop legacy expansion.
2. Keep OpenAPI and implementation aligned.
3. Move consumers to canonical paths with minimal risk.
4. Preserve fiscal/compliance behavior during migration.

## Practical rules
- New endpoint: canonical route only.
- Do not reintroduce `/api/Payment|/api/Cart|/api/Product`.
- Contract değişikliği: `backend/swagger.json` + ilgili consumer güncellemesi aynı değişim setinde.

## Near-term work queue
1. **Payment contract hardening:** v2 envelope kullanımını takip et; legacy parse dallarını metriklerle küçült.
2. **OpenAPI governance:** critical-path scriptleri CI’de yeşil tut; yeni retired prefix eklenmesini engelle.
3. **Route inventory upkeep:** `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` güncel kalsın.

## Validation baseline
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
- `dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~PaymentApiContractTests|FullyQualifiedName~OpenApiCriticalPathsContractTests"`
- `cd frontend-admin && npm run test:contract`
- `cd frontend && npm run test:contract`
