# API Contract Stabilization Plan

**Status:** Active, incremental (no big-bang rewrite).

**Context:** Üst seviye davranış ve RKSV/voucher kuralları için `REGKASSE_AI_ONBOARDING.md` ana özettir; bu dosya route/OpenAPI stabilize iş kuyruğuna odaklanır.

## Multi-Tenant Architecture

- Yeni admin uçları kiracı bağlamını bozmamalı; Super Admin yüzeyi `/api/admin/tenants` altında kalır.
- OpenAPI/swagger değişikliklerinde `tenant_id` claim ve admin tenant DTO’ları diff’te kontrol edilir.

## Current repository facts
- Canonical boundaries exist: `/api/admin/*` and `/api/pos/*`.
- Legacy aliases still exist for `Payment`, `Cart`, `Product` families.
- Legacy alias usage is instrumented by `LegacyRouteDeprecationFilter` (headers + metrics).
- OpenAPI contract checks run via `scripts/validate-critical-openapi-paths.mjs` and `scripts/verify-api-client.mjs`.
- Admin generated client still contains some legacy-tag surfaces (notably `generated/cart`).

## Stabilization goals
1. Stop legacy expansion.
2. Keep OpenAPI and implementation aligned.
3. Move consumers to canonical paths with minimal risk.
4. Preserve fiscal/compliance behavior during migration.

## Practical rules
- New endpoint: canonical route only.
- Legacy aliases: compatibility shim only; no feature growth.
- Contract değişikliği: `backend/swagger.json` + ilgili consumer güncellemesi aynı değişim setinde.

## Near-term work queue
1. **Legacy consumer cleanup (admin):** `generated/cart` ve benzeri legacy path tüketimlerini azalt.
2. **Payment contract hardening:** v2 envelope kullanımını takip et; legacy parse dallarını metriklerle küçült.
3. **OpenAPI governance:** critical-path scriptleri CI’de yeşil tut; yeni legacy prefix eklenmesini engelle.
4. **Route inventory upkeep:** `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` güncel kalsın.

## Validation baseline
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
- `dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~PaymentApiContractTests|FullyQualifiedName~OpenApiCriticalPathsContractTests"`
- `cd frontend-admin && npm run test:contract`
- `cd frontend && npm run test:contract`
