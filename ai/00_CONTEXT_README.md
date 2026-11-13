# Regkasse AI context pack

Short, repo-true operating notes for AI-assisted development.  
Index / read order: [`README.md`](README.md). On conflict, **`AGENTS.md` and the code win**.

## Read this first

- **Primary AI brief:** Read `REGKASSE_AI_ONBOARDING.md` at the repo root first. Target users, RKSV special receipts, payment/offline queues, voucher rules, FinanzOnline status, and guardrails are summarized there.
- This `/ai` pack goes deeper on backend contracts, database, API boundaries, security, and do-not-touch lists.

## Multi-tenant (summary)

- One backend, many tenants. **Production hosts:** POS `pos.regkasse.at`, FA `admin.regkasse.at`, API `api.regkasse.at`. Tenant comes from JWT `tenant_id` (single POS UI). `{slug}.regkasse.at` is **not** the POS entry point.
- Customer websites: `frontend-sites` (`/[slug]` or a verified `TenantDomain` custom host).
- Isolation: `ITenantEntity` + EF global query filter (`ICurrentTenantAccessor`); cross-tenant access returns **404**.
- Development: `X-Tenant-Id` / `?tenant=` (slug), FA dev tenant switcher, `admin.regkasse.local` / optional `dev.regkasse.local`.
- **Singleton + EF:** `AppDbContext` is scoped. Singletons (`LicenseService`) must use `IServiceScopeFactory`. Do not resolve `IDbContextFactory` from the root provider.
- Full text: `REGKASSE_AI_ONBOARDING.md` → **Multi-Tenant Architecture**; `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`.

## Monorepo summary

- `backend/`: ASP.NET Core API (`net10.0`), EF Core + PostgreSQL, Swagger (`backend/swagger.json`).
- `frontend/`: Expo Router mobile POS (React Native, Expo SDK 56).
- `frontend-admin/`: Next.js 16 (App Router) + Ant Design 6 + TanStack Query; auth gate `src/proxy.ts` (not the old `middleware.ts`).
- `frontend-sites/`: Shared multi-tenant storefront (Next.js 16).
- `localization/`: i18n validation / import-export scripts.
- `scripts/`: OpenAPI/Orval and critical contract checks.

## Work order for agents

1. `REGKASSE_AI_ONBOARDING.md` (repo root) — current product and fiscal summary.
2. Nearest application code and tests.
3. Package config (`*.csproj`, `package.json`, `orval.config.ts`).
4. CI workflows (`.github/workflows/*`).
5. These `/ai` documents (narrow contracts).

## Change safety rules

- Make small, reversible changes.
- Treat `Cart → Payment → Receipt → DailyClosing` as high risk.
- Do not change TSE/RKSV/FinanzOnline behavior unless the task explicitly requires it.
- API boundary: Admin `/api/admin/*`, POS `/api/pos/*`, Sites `/api/public/*` + `/api/sites/*` (exceptions must be documented).
- Do not merge the two offline systems. Do not use working hours to close POS or FA.

## Multi-tenant local test (summary)

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
# or: curl "http://localhost:5184/api/health?tenant=dev"
```

Development environment + slug; see `REGKASSE_AI_ONBOARDING.md` (Development Setup for Multi-Tenant Testing).

## Quick verification commands

- `node scripts/verify-api-client.mjs`
- `node scripts/validate-critical-openapi-paths.mjs`
- `node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error`
- `node localization/scripts/check-translation-boundary.mjs --app frontend-admin`
- `node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json`
