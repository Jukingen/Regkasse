# AGENTS.md - Regkasse POS System Development Rules

This file is the GitHub-visible index. Cursor applies [`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc) on every task. Other rules attach by glob, or manually for workflow and deploy. Edit the matching `.mdc` file. Do not paste the full rule set back here.

**Last updated:** 2026-11-24

## Stack pins

`package.json` and `backend/KasseAPI_Final.csproj` are the source of truth. This table is a summary.

| Component | Pin |
|-----------|-----|
| .NET / ASP.NET Core / EF Core | .NET 10 (`net10.0`), EF Core 10.0.10 |
| Node.js | 22+ (CI runs 22). The root `package.json` `engines` still says `>=20`. |
| Next.js (FA, Sites, Marketing) | 16.4.6 (`^16.4.6`) |
| React (web) | 19.3.x |
| Ant Design (FA) | 6 (`^6.4.3`) |
| Expo (POS) | SDK 58 (`expo` `^58.0.0`, lockfile 58.0.26) |
| React Native / React (POS) | 0.88.1 / 19.3.0 |

## Rule files

| File | Applies | Contents |
|------|---------|----------|
| [`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc) | Always | Repo layout, source of truth, working style, language, tenancy, API boundaries, high-risk list |
| [`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc) | `backend/**/*.cs` | EF Core, no `IgnoreQueryFilters()` except Super Admin, auth, limits, backup, caching, security |
| [`.cursor/rules/101-frontend-admin.mdc`](.cursor/rules/101-frontend-admin.mdc) | `frontend-admin/**/*.{ts,tsx}` | FA conventions, `useNotify()`, `useAntdApp()`, no static Ant Design APIs |
| [`.cursor/rules/102-frontend-pos.mdc`](.cursor/rules/102-frontend-pos.mdc) | `frontend/**/*.{ts,tsx}` | German POS UI, offline queue, vouchers, working hours |
| [`.cursor/rules/103-database.mdc`](.cursor/rules/103-database.mdc) | `**/Migrations/**/*.cs`, `**/*.sql` | Additive migrations only; do not edit committed migrations |
| [`.cursor/rules/104-fiscal.mdc`](.cursor/rules/104-fiscal.mdc) | `backend/Services/Tse*`, `backend/Services/Rksv*`, `backend/Tse/**` | Signature chain, receipt numbers, DEP, country regimes |
| [`.cursor/rules/105-testing.mdc`](.cursor/rules/105-testing.mdc) | `**/*.test.*`, `**/*.spec.*`, `**/*Tests.cs` | Test conventions and validation commands |
| [`.cursor/rules/200-workflow.mdc`](.cursor/rules/200-workflow.mdc) | Manual | Pull requests, commit messages, no direct push to `main` / `master` |
| [`.cursor/rules/201-deploy.mdc`](.cursor/rules/201-deploy.mdc) | Manual | Deployment checklist and environment variables |

For medium or large tasks, also read [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md), optionally [`docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`](docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md), and the relevant file under [`ai/`](ai/).

## Recent milestones

- Per-tenant Fiskaly credentials (BYOK) and the production cutover note in [`docs/FISKALY_PRODUCTION_CUTOVER.md`](docs/FISKALY_PRODUCTION_CUTOVER.md).
- Wolt, Foodora, and Lieferando online-order intake. Operator steps: [`docs/MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md`](docs/MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md).
- GDPR purge keeps fiscal amounts, TSE signatures, and the RKSV issuer snapshot on payments, and strips other tenant PII.
- `PaymentService` refactor: split into 14 injected services plus one static helper (`PaymentCommitFailureMapper`), under `backend/Services/Payment*.cs`. `PaymentService.cs` went from 5,487 lines (before the first extraction, `926ac2c1`) to 1,271. It is still a high-risk flow. Change the extracted service that owns the step, not the orchestrator. List: [`docs/AI_CONTEXT.md`](docs/AI_CONTEXT.md).
- Audit log export: `GET /api/admin/audit-logs/export?format=csv|xlsx` (`audit.view`). It uses the same filters and PostgreSQL full-text `search` as `api/AuditLog`, and rejects more than 100,000 rows with `EXPORT_TOO_LARGE`. Not a new boundary: it stays under `/api/admin/*`.

## Where to read more

- [`docs/AI_CONTEXT.md`](docs/AI_CONTEXT.md) — one-page start for a new chat
- [`docs/SECURITY_ADVISORIES.md`](docs/SECURITY_ADVISORIES.md) — written for Expo SDK 57. On the SDK 58 lockfile `npm audit` reports 0 high and 26 moderate (checked 2026-11-24), so that note needs a refresh. Do not run `npm audit fix --force`.

## Section index

Headings below keep existing doc anchors. The rule text lives in the linked file.

## Purpose
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Cursor / AI Agent Integration
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Language Rules
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Working Style
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Source of Truth
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Read Before Changing Code
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Recent Improvements (2026-07)
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Repo Map
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

### Updated Stack Versions
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Multi-Tenant Architecture (CRITICAL)
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## API Boundaries (DO NOT CROSS)
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## License Sales (Super Admin Only)
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Trial / Demo system
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Tenant Limits
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Self-service portal (Mein Konto)
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Unified license
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Authentication & User Management
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

#### Expired license — customer data management (RKSV)
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Audit & Compliance
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Fiscal Rules (RKSV/TSE)
[`.cursor/rules/104-fiscal.mdc`](.cursor/rules/104-fiscal.mdc)

### DEP §7 Export (Datenerfassungsprotokoll / Signaturjournal)
[`.cursor/rules/104-fiscal.mdc`](.cursor/rules/104-fiscal.mdc)

## Country & Fiscal Regimes (Multi-Country)
[`.cursor/rules/104-fiscal.mdc`](.cursor/rules/104-fiscal.mdc)

## Backup & Disaster Recovery
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Activity Feed & Notifications
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Security Rules
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Directory Hints
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## AI Docs Routing Hints
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Code Quality Rules
[`.cursor/rules/105-testing.mdc`](.cursor/rules/105-testing.mdc)

## High-Risk Flows (DO NOT TOUCH WITHOUT CAREFUL VALIDATION)
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Do NOT
[`.cursor/rules/000-core.mdc`](.cursor/rules/000-core.mdc)

## Database Baseline Rules
[`.cursor/rules/103-database.mdc`](.cursor/rules/103-database.mdc)

## Caching (Backend)
[`.cursor/rules/100-backend.mdc`](.cursor/rules/100-backend.mdc)

## Frontend-Admin (FA) Conventions
[`.cursor/rules/101-frontend-admin.mdc`](.cursor/rules/101-frontend-admin.mdc)

## Frontend-POS (FE) Conventions
[`.cursor/rules/102-frontend-pos.mdc`](.cursor/rules/102-frontend-pos.mdc)

## Quick Reference
[`.cursor/rules/200-workflow.mdc`](.cursor/rules/200-workflow.mdc)

## Validation Commands
[`.cursor/rules/105-testing.mdc`](.cursor/rules/105-testing.mdc)

## Development Workflow
[`.cursor/rules/200-workflow.mdc`](.cursor/rules/200-workflow.mdc) (local setup) and [`.cursor/rules/201-deploy.mdc`](.cursor/rules/201-deploy.mdc) (environment variables)
