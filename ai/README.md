# AI / agent contracts (`ai/`)

Internal implementation contracts and guardrails for coding agents and humans.  
**Not** a substitute for reading the nearest source code. On conflict: **code → package config → CI → `AGENTS.md` → these files**.

**Last reviewed:** 2026-07-21

## Purpose

- Keep agents aligned on **multi-tenant**, **API boundaries**, **fiscal/RKSV**, and **high-risk** surfaces.
- Provide short, repo-true contracts — not operator runbooks (those live in [`../docs/`](../docs/README.md)).

## Read order

1. [`../REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) — product brief  
2. [`../AGENTS.md`](../AGENTS.md) — always-applied workspace rules (wins over stale `ai/` text)  
3. [`00_CONTEXT_README.md`](00_CONTEXT_README.md) — short context pack  
4. Domain file / module below for the area you change  
5. [`07_DO_NOT_TOUCH.md`](07_DO_NOT_TOUCH.md) before fiscal / auth / tenancy edits  

## Product truths (do not unlearn)

| Truth | Detail |
|-------|--------|
| Single POS UI | `pos.regkasse.at` + JWT `tenant_id` — **not** `{slug}.regkasse.at` as POS entry |
| Shared FA / API | `admin.regkasse.at` / `api.regkasse.at` |
| Dev tenant | `X-Tenant-Id` / `?tenant=` only in Development |
| Cross-tenant | HTTP **404** (not 403) |
| Boundaries | POS → `/api/pos/*`; Admin → `/api/admin/*`; Sites → `/api/public/*`, `/api/sites/*` |
| Dual offline | `offline_transactions` ≠ `offline_orders` |
| Working hours | Gate **website/app order intake only** — never POS/FA |
| Backup FA | `/backup` (+ costs, compliance); Mandanten-Admin `backup.manage` |
| Stack | .NET / EF **10**, Next.js **16** (FA), Expo SDK **56** — see `AGENTS.md` |

## Core contracts

| File | Use when |
|------|----------|
| [`00_CONTEXT_README.md`](00_CONTEXT_README.md) | Quick monorepo + multi-tenant summary |
| [`01_BACKEND_CONTRACT.md`](01_BACKEND_CONTRACT.md) | Backend / host / DI / high-risk routes |
| [`02_DATABASE_CONTRACT.md`](02_DATABASE_CONTRACT.md) | Entities, migrations, tenancy columns |
| [`02_DATABASE_OVERVIEW.md`](02_DATABASE_OVERVIEW.md) | Data-region map (not schema authority) |
| [`03_API_CONTRACT.md`](03_API_CONTRACT.md) | HTTP shapes, OpenAPI workflow |
| [`04_FRONTEND_CONTRACT.md`](04_FRONTEND_CONTRACT.md) | POS / FA / Sites client conventions |
| [`05_SECURITY_COMPLIANCE.md`](05_SECURITY_COMPLIANCE.md) | Security, tenancy, fiscal compliance posture |
| [`06_TASK_TEMPLATE.md`](06_TASK_TEMPLATE.md) | Task framing |
| [`07_DO_NOT_TOUCH.md`](07_DO_NOT_TOUCH.md) | High-risk surfaces |
| [`08_FILE_MAP.md`](08_FILE_MAP.md) | Where code lives |
| [`08_API_CONTRACT_STABILIZATION_PLAN.md`](08_API_CONTRACT_STABILIZATION_PLAN.md) | Historical contract stabilization plan |
| [`09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`](09_LEGACY_CANONICAL_ROUTE_INVENTORY.md) | Legacy vs canonical routes |
| [`09_AUTH_SCHEMA_RECOVERY.md`](09_AUTH_SCHEMA_RECOVERY.md) | Auth schema recovery notes (same `09_` prefix — different topic) |
| [`10_API_BOUNDARY_POLICY.md`](10_API_BOUNDARY_POLICY.md) | POS vs Admin vs Sites boundaries |
| [`11_OPENAPI_CONTRACT_GOVERNANCE.md`](11_OPENAPI_CONTRACT_GOVERNANCE.md) | Swagger / Orval governance |
| [`12_ADMIN_LEGACY_WRAPPER_REMOVAL.md`](12_ADMIN_LEGACY_WRAPPER_REMOVAL.md) | Admin legacy wrapper removal |
| [`13_POS_RESPONSE_NORMALIZATION_INVENTORY.md`](13_POS_RESPONSE_NORMALIZATION_INVENTORY.md) | POS response normalization |

## Modules (`modules/`)

| Module | Topic |
|--------|--------|
| [`modules/offline_orders.md`](modules/offline_orders.md) | Full order offline snapshots |
| [`modules/offline_transactions_legacy.md`](modules/offline_transactions_legacy.md) | Legacy TSE payment intents |
| [`modules/backup_permissions.md`](modules/backup_permissions.md) | Backup RBAC / tenant scoping |
| [`modules/billing_license.md`](modules/billing_license.md) | Mandant license sales |
| [`modules/payments.md`](modules/payments.md) | Payment domain notes |
| [`modules/tse_finanzonline.md`](modules/tse_finanzonline.md) | TSE / FinanzOnline |

Digital services / working hours / Sites: prefer [`../docs/DIGITAL_SERVICES.md`](../docs/DIGITAL_SERVICES.md), [`../docs/WORKING_HOURS.md`](../docs/WORKING_HOURS.md), [`../frontend-sites/README.md`](../frontend-sites/README.md).

## File inventory (`ai/`)

```text
00_CONTEXT_README.md
01_BACKEND_CONTRACT.md
02_DATABASE_CONTRACT.md
02_DATABASE_OVERVIEW.md
03_API_CONTRACT.md
04_FRONTEND_CONTRACT.md
05_SECURITY_COMPLIANCE.md
06_TASK_TEMPLATE.md
07_DO_NOT_TOUCH.md
08_API_CONTRACT_STABILIZATION_PLAN.md
08_FILE_MAP.md
09_AUTH_SCHEMA_RECOVERY.md
09_LEGACY_CANONICAL_ROUTE_INVENTORY.md
10_API_BOUNDARY_POLICY.md
11_OPENAPI_CONTRACT_GOVERNANCE.md
12_ADMIN_LEGACY_WRAPPER_REMOVAL.md
13_POS_RESPONSE_NORMALIZATION_INVENTORY.md
README.md
modules/backup_permissions.md
modules/billing_license.md
modules/offline_orders.md
modules/offline_transactions_legacy.md
modules/payments.md
modules/tse_finanzonline.md
```

## Rules of thumb

- Prefer small, reversible changes.
- Cross-tenant → HTTP **404**.
- Do not weaken fiscal, audit, or tenant isolation guarantees.
- Do not invent parallel architectures or extend legacy `/api/Payment|Cart|Product`.

Human docs index: [`../docs/README.md`](../docs/README.md) · CI: [`../.github/workflows/README.md`](../.github/workflows/README.md).
