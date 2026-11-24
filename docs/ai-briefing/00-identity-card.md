# 00 — Identity Card (paste into ChatGPT "Instructions")

> Part 0 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified against the repository at commit `e2be4d5b` (branch `docs/recent-changes-2026-11-24`, 2026-11-24).
> This card is short on purpose. It is the always-on context. Details live in parts 01-09.

## What Regkasse is

Regkasse is a multi-tenant SaaS point-of-sale (POS / "Kasse") platform for Austria. The production fiscal path is **RKSV** (Registrierkassensicherheitsverordnung): TSE signing, a signature chain, sequential receipt numbers, DEP export, FinanzOnline. The product also supports several business verticals (restaurant, retail, hair salon, taxi, tickets, lodging, and others) and non-fiscal digital services (tenant websites, online orders, marketplace intake).

## Monorepo map

| Package | Tech | Role |
|---|---|---|
| `backend/` | ASP.NET Core 10, EF Core 10, PostgreSQL | API, fiscal/TSE/RKSV, tenancy, billing, backup. Dev: `http://localhost:5184` |
| `frontend/` (`cash-register`) | Expo SDK 58, React Native 0.88, TypeScript | POS app for cashiers. German UI. Dev: `http://localhost:8081` |
| `frontend-admin/` (`registrierkasse-admin`) | Next.js 16, Ant Design 6, TanStack Query, Orval | Admin panel ("FA") for Manager and SuperAdmin. Dev: `http://admin.regkasse.local:3000` |
| `frontend-sites/` | Next.js 16 | Shared tenant storefronts (`/[slug]`), non-fiscal. Dev port 3001 |
| `frontend-marketing/` | Next.js 16 | Marketing site (SEO, de/en/tr). Dev port 3002 |
| `localization/` | Node scripts | i18n validate / export / usage gates |

Production hosts: POS `https://pos.regkasse.at`, FA `https://admin.regkasse.at`, API `https://api.regkasse.at`.

## Ten rules that must never be broken

1. **Tenant isolation.** Tenant-scoped entities implement `ITenantEntity` and are filtered by EF global query filters. A cross-tenant access returns **HTTP 404**, never 403. Never use `IgnoreQueryFilters()` except in deliberate Super Admin paths.
2. **API boundaries.** POS calls `/api/pos/*` (plus `/api/Auth/*`, `/api/Receipts/*`). Admin calls `/api/admin/*` (plus `/api/Auth/*`). Do not cross.
3. **Fiscal path is high risk.** Cart → Payment → Receipt → DailyClosing, TSE signature chain, receipt numbers, RKSV special receipts. Change only with explicit scope, a plan first, and extra tests.
4. **Migrations are additive only.** Never edit a committed migration. Never delete columns. Backfill in a separate step.
5. **Singletons + EF.** Use `IServiceScopeFactory.CreateScope()` before resolving `IDbContextFactory<AppDbContext>`. Never resolve it from the root provider.
6. **Vouchers.** Never store or log a raw voucher code. Never queue a voucher payment offline.
7. **No client flag may bypass TSE signing.** The backend is the final authority; frontend guards are UX only.
8. **UI language.** POS UI is German (de-DE). Admin UI uses i18n (de/en/tr) from locale files. No hardcoded user-facing strings.
9. **Admin Ant Design 6.** Never use static `message`, `notification`, or `Modal.confirm` from `antd`. Use `useNotify()` and `useAntdApp()`.
10. **Workflow.** Land changes via pull request, never push to `main`/`master`. English commit messages. No secrets in commits. After a backend API change: regenerate `swagger.json`, run `npm run generate:api`, run `node scripts/verify-api-client.mjs`.

## How to work on this project

- Make the smallest safe change. Follow the nearest existing pattern. Do not mix refactors into feature work.
- For anything medium or large: give a short plan, affected files, main risks, backward-compatibility impact, and a test strategy.
- Do not invent endpoints, roles, permissions, or fiscal behavior. If the briefing does not say, mark it "to verify" and tell the coding agent to inspect the code first.
- Nothing here is a legal compliance guarantee. Fiscal export output is diagnostic, not official proof.
- Language convention: IDE explanations in Turkish, code identifiers in English, repository documentation in English (US).

## Roles (backend names)

`SuperAdmin`, `Manager` (UI label "Mandanten-Admin"), `Cashier`, `Waiter`, `Kitchen`, `Accountant`, `ReportViewer`, `ComplianceOfficer`. The source of truth for permissions is `backend/Authorization/RolePermissionMatrix.cs`.
