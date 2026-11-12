# AI context

One-page start for a new chat. Canonical rules stay in `AGENTS.md` and `.cursor/rules/`.

**Last updated:** 2026-11-12

## What this project is

Regkasse is a multi-tenant SaaS point-of-sale system for Austria. The production fiscal path is RKSV (TSE, signature chain, DEP), and the product covers 11 verticals.

## Repo layout

- `backend/` — ASP.NET Core API, fiscal/TSE/RKSV, EF Core, PostgreSQL
- `frontend/` — Expo POS; UI text is German (de-DE)
- `frontend-admin/` — Next.js admin (FA); i18n de/en/tr
- `frontend-sites/` — Next.js tenant storefronts
- `frontend-marketing/` — marketing site; SEO and de/en/tr
- `localization/` — shared i18n tooling

## Canonical rules

- Fiscal path (TSE/RKSV/DEP) is high-risk; never touch it unless asked.
- Cross-tenant access returns HTTP 404, not 403.
- Migrations are additive only. Do not edit a committed migration.
- POS UI is German; FA is i18n (de/en/tr).
- No direct push to `main`; branch protection is active.

## Where to read more

- `AGENTS.md` — GitHub-visible index of the rules
- `.cursor/rules/*.mdc` — modular Cursor rules (`000-core.mdc` always applies)
- `docs/` — human documentation
- `ai/` — domain contracts and guardrails
- Cutover checklist: `docs/PRODUCTION_CUTOVER.md`.

## Recent milestones

- Fiskaly SIGN AT is per tenant (`tenant_fiskaly_credentials`). Mandanten-Admin enters API key, secret, and SCU on FA `/settings`. Production does not use one shared host key. See `docs/FISKALY_PRODUCTION_CUTOVER.md`.
- Marketplace intake is implemented for Wolt (OAuth), Foodora (`client_credentials`), and Lieferando (JET Connect). Tables: `online_order_integration_providers`, `online_order_integration_tenant_credentials`. FA: `/admin/online-order-integrations`. See `docs/ONLINE_ORDER_INTEGRATIONS.md`.
- GDPR purge anonymizes non-fiscal PII and keeps payment/invoice amounts and TSE signatures (`DataDeletionService` / `TenantPiiPurge`).
- Public customer dashboard requires the full phone number and an order number.
- Demo tenant seed expanded (46 products, 22 customers, vertical-specific rows).
- Marketing site at `frontend-marketing` with SEO and de/en/tr.
- Monitoring: Sentry, uptime, Slack alerts, `/admin/monitoring`.
- ImageSharp replaced by SkiaSharp (MIT).
- Dependabot guard and branch protection are active.

## High-risk do-not

- Never bypass branch protection.
- Never edit committed migrations.
- Never log raw voucher codes or TSE secrets.
- Never use `--admin` merge.
