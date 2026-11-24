# AI context

One-page start for a new chat. Canonical rules stay in `AGENTS.md` and `.cursor/rules/`.

**Last updated:** 2026-11-24

## What this project is

Regkasse is a multi-tenant SaaS point-of-sale system for Austria. The production fiscal path is RKSV (TSE, signature chain, DEP), and the product covers 11 verticals.

## Stack at a glance

.NET 10 / EF Core 10.0.10. Expo SDK 58 (`expo` 58.0.26, React Native 0.88.1, React 19.3.0) for the POS. Next.js 16.4.6 and Ant Design 6 for FA, Sites, and Marketing. Node 22+ (CI runs 22). Exact pins: `AGENTS.md` § Stack pins.

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
- GDPR purge anonymizes non-fiscal PII and keeps payment/invoice amounts, TSE signatures, and the RKSV issuer snapshot (company name, address, Steuernummer) on payments (`DataDeletionService` / `TenantPiiPurge`).
- Audit log search is PostgreSQL full-text (`SearchVector`, `PlainToTsQuery`) over action, entity type, description, and actor email. Export: `GET /api/admin/audit-logs/export?format=csv|xlsx` (`audit.view`, same filters as the list, capped at 100,000 rows → HTTP 400 `EXPORT_TOO_LARGE`). FA: activity log export.
- `PaymentService` was split into focused services (4,723 → 1,271 lines in `PaymentService.cs`, plus two `CreatePaymentCore` partials). The refactor was meant to preserve behavior, with golden-master tests added first. The sale order is sale gate → fiscal transaction → settlement → post-commit. Extracted units, all in `backend/Services/`:
  1. `PaymentQueryService` — reads
  2. `PaymentReceiptReadService` — receipt reads (reprint creates no fiscal document)
  3. `PaymentFinanzOnlineSubmissionService` — FinanzOnline submit, after commit
  4. `PaymentOfflineIntentService` — offline intents
  5. `PaymentBenefitService` — benefits
  6. `PaymentVoucherLedgerService` — voucher ledger writes
  7. `PaymentSaleGateService` — checks before the fiscal transaction
  8. `PaymentSettlementService` — settlement inside the open transaction
  9. `PaymentDocumentPersistenceService` — fiscal document staging
  10. `PaymentPostCommitService` — post-commit audit and dispatch
  11. `PaymentCommitFailureHandler` — commit failure handling
  12. `PaymentCommitFailureMapper` — pure static failure mapping (not in DI)
  13. `PaymentStornoRefundGate` — storno/refund admission
  14. `PaymentStornoService` — fiscal storno
  15. `PaymentRefundService` — partial refund
- Public customer dashboard requires the full phone number and an order number.
- Demo tenant seed expanded (46 products, 22 customers, vertical-specific rows).
- Marketing site at `frontend-marketing` with SEO and de/en/tr.
- Monitoring: Sentry, uptime, Slack alerts, `/admin/monitoring`.
- ImageSharp replaced by SkiaSharp (MIT).
- Dependabot guard and branch protection are active.
- Expo SDK 58 upgrade (PR #256) and Next.js 16.4.6 (security bump). `npm audit` on the SDK 58 lockfile reports 0 high and 23 moderate (checked 2026-11-24). `docs/SECURITY_ADVISORIES.md` still describes SDK 57 and needs a refresh. Do not run `npm audit fix --force`.
- Load test: production target is 50-200 cashiers. 100 cashiers pass the 3 s lab budget, 200 do not, 500 is a stress test. See `docs/LOAD_TEST_FINDINGS.md`.

## High-risk do-not

- Never bypass branch protection.
- Never edit committed migrations.
- Never log raw voucher codes or TSE secrets.
- Never use `--admin` merge.
