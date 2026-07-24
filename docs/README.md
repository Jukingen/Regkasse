# Regkasse documentation (`docs/`)

Human-facing guides for operators and developers.  
**AI / agent rules:** [`../AGENTS.md`](../AGENTS.md) · **Project brief:** [`../REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) · **Contracts:** [`../ai/`](../ai/README.md) · **CI:** [`../.github/workflows/README.md`](../.github/workflows/README.md)

**Production hosts:** POS `https://pos.regkasse.at` · FA `https://admin.regkasse.at` · API `https://api.regkasse.at`  
**Local:** POS `:8081` · FA `:3000` · Sites `:3001` · API `:5184`

---

## Start here

| Doc | Audience | Topic |
|-----|----------|--------|
| [`MULTI_TENANT.md`](MULTI_TENANT.md) | Dev / ops | Tenant resolution, isolation, hosts, Dev headers |
| [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md) | Dev / ops | Single POS UI (`pos.regkasse.at`), JWT tenant |
| [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md) | Super Admin / FA | Mandant CRUD, switcher, provisioning |
| [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md) | FA | Platform vs tenant users, Access hub |
| [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) | FA / security | Role × permission defaults |
| [`PERMISSIONS_MENU_MAPPING.md`](PERMISSIONS_MENU_MAPPING.md) | FA / security | Sidebar menu ↔ permission catalog groups |
| [`PROJECT_SUMMARY.md`](PROJECT_SUMMARY.md) | All | High-level product summary |
| [`PROJECT_COMPREHENSIVE_DOCUMENTATION.md`](PROJECT_COMPREHENSIVE_DOCUMENTATION.md) | Dev / ops | Full improvement inventory, TSE ops wave, deploy checklist |
| [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md) | Ops | New mandant onboarding |

---

## Architecture & tenancy

| Doc | Topic |
|-----|--------|
| [`MULTI_TENANT.md`](MULTI_TENANT.md) | Pipeline, EF filters, 404 cross-tenant, reserved hosts |
| [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md) | Shared POS; not `{slug}` POS hosts |
| [`IMPERSONATION.md`](IMPERSONATION.md) / [`IMPERSONATION_FLOW.md`](IMPERSONATION_FLOW.md) | Super Admin → mandant session |
| [`CASH_REGISTER_LIFECYCLE.md`](CASH_REGISTER_LIFECYCLE.md) | Register open/close/decommission |
| [`WORKING_HOURS.md`](WORKING_HOURS.md) | Website/app hours only (never POS/FA) |

---

## Digital services & online orders

| Doc | Topic |
|-----|--------|
| [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) | Website / app generators, requests |
| [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) | Non-fiscal order inbox & status |
| [`CHANGELOG.md`](CHANGELOG.md) | Digital / orders feature wave |

Runtime storefront app: [`../frontend-sites/README.md`](../frontend-sites/README.md) (`/[slug]`, public catalog APIs). Custom hosts: verified `TenantDomain` (`website.manage`).

---

## Backup & disaster recovery

| Doc | Topic |
|-----|--------|
| [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) | **Hub** (start here) |
| [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md) | Full system guide |
| [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) | Mandanten-Admin vs Super Admin |
| [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md) | Tenant vs System content / cost |
| [`restore-boundary-notes.md`](restore-boundary-notes.md) | No production restore via API |
| [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md) | Restore/backup RKSV guardrails |
| [`backup-phase1-runbook.md`](backup-phase1-runbook.md) / [`backup-phase2-runbook.md`](backup-phase2-runbook.md) | Phase runbooks |
| [`backup-orchestrator-distributed-lock.md`](backup-orchestrator-distributed-lock.md) | Orchestrator lock |
| [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md) | Restore drills |
| [`restore-verification-distributed-lock.md`](restore-verification-distributed-lock.md) | Drill lock |

FA hub: `/backup` (+ `/backup/costs`, `/backup/compliance`, dashboard, runs, configuration, audit). Legacy aliases `/settings/backup-dr`, `/admin/backup` redirect to `/backup`.

---

## Fiscal / RKSV / FinanzOnline

| Doc | Topic |
|-----|--------|
| [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md) | Backup/restore compliance notes |
| [`RKSV_CASH_REGISTER_OPERATIONS.md`](RKSV_CASH_REGISTER_OPERATIONS.md) | Register operations |
| [`RKSV_AFTER_TAGESABSCHLUSS.md`](RKSV_AFTER_TAGESABSCHLUSS.md) | After daily closing |
| [`RKSV_RECEIPT_INVOICE_REQUIREMENTS.md`](RKSV_RECEIPT_INVOICE_REQUIREMENTS.md) | Receipt/invoice requirements |
| [`RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md) | Official BMF references |
| [`RKSV_BMF_BELEGCHECK_WORKFLOW.md`](RKSV_BMF_BELEGCHECK_WORKFLOW.md) | Belegcheck workflow |
| [`RKSV_EIGENZERTIFIZIERUNG_TEMPLATE.md`](RKSV_EIGENZERTIFIZIERUNG_TEMPLATE.md) | Self-certification template |
| [`DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) / [`DEP_EXPORT_COMPLETION.md`](DEP_EXPORT_COMPLETION.md) | DEP §7 export |
| [`BACKDATED_TAGESABSCHLUSS.md`](BACKDATED_TAGESABSCHLUSS.md) | Backdated closing |
| [`OFFLINE_SYSTEM_INDEX.md`](OFFLINE_SYSTEM_INDEX.md) | Two offline systems (intents vs orders) |
| [`OFFLINE_PRODUCTION_DEPLOYMENT.md`](OFFLINE_PRODUCTION_DEPLOYMENT.md) | Offline prod deploy |
| [`OFFLINE_MANUAL_TEST_CHECKLIST.md`](OFFLINE_MANUAL_TEST_CHECKLIST.md) / [`OFFLINE_SYSTEM_TEST_PLAN.md`](OFFLINE_SYSTEM_TEST_PLAN.md) | Offline testing |
| [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | FO production cutover |
| [`finanzonline-*.md`](finanzonline-pilot-go-no-go-gate.md) | Pilot / BMF test runbooks |

Release notes under [`release/`](release/) (FO reconciliation, offline separation, fiscal export, go-live checklists).

---

## Auth, users, license, billing

| Doc | Topic |
|-----|--------|
| [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md) | SuperAdmin TOTP / Dev bypass |
| [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md) | Users, Access & Roles hub |
| [`LICENSE_SYSTEM.md`](LICENSE_SYSTEM.md) / [`LICENSE_MANAGEMENT_DESIGN.md`](LICENSE_MANAGEMENT_DESIGN.md) | Deployment vs mandant license |
| [`BILLING_TENANT_LICENSE.md`](BILLING_TENANT_LICENSE.md) | License sales |
| [`BILLING_TESTING.md`](BILLING_TESTING.md) / [`BILLING_E2E_TEST_PLAN.md`](BILLING_E2E_TEST_PLAN.md) | Billing tests |
| [`EMAIL_CONFIGURATION.md`](EMAIL_CONFIGURATION.md) | SMTP / activity email |
| [`API_CONTRACTS.md`](API_CONTRACTS.md) | API contract notes (detail) — see also root [`../API_CONTRACT.md`](../API_CONTRACT.md) |
| [`API_LEGACY_DEPRECATION.md`](API_LEGACY_DEPRECATION.md) | Legacy routes |

---

## Deploy, mobile, ops

| Doc | Topic |
|-----|--------|
| [`ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md) | Admin deploy |
| [`ANDROID_RELEASE_SIGNING.md`](ANDROID_RELEASE_SIGNING.md) | Android signing |
| [`REGKASSE_APK_INSTALLATIONSANLEITUNG.md`](REGKASSE_APK_INSTALLATIONSANLEITUNG.md) | APK install (de) |
| [`beta-env-matrix.md`](beta-env-matrix.md) / [`beta-smoke-checklist.md`](beta-smoke-checklist.md) | Beta |
| [`ops-known-gaps.md`](ops-known-gaps.md) | Known ops gaps |
| [`backend-postgresql-integration-tests.md`](backend-postgresql-integration-tests.md) | PG integration tests |
| [`acceptance-authoritative-simulation-tests.md`](acceptance-authoritative-simulation-tests.md) | Acceptance simulation |
| [`inventory-lager-optional.md`](inventory-lager-optional.md) | Optional inventory |
| [`contributing-i18n.md`](contributing-i18n.md) | i18n contribution notes |

i18n tooling: [`../localization/README.md`](../localization/README.md).

---

## Changelogs

| Doc | Topic |
|-----|--------|
| [`CHANGELOG.md`](CHANGELOG.md) | Digital services / online orders wave |
| [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md) | Recent engineering waves |
| [`CHANGELOG_TENANT_MANAGEMENT.md`](CHANGELOG_TENANT_MANAGEMENT.md) | Tenant/license FA changes |
| [`../CHANGELOG.md`](../CHANGELOG.md) | Root Keep a Changelog |

---

## `release/` (engineering notes)

Focused release/runbook docs (FinanzOnline, offline systems, fiscal export, metrics, go-live). Start from [`release/OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md) or the FO reconciliation notes when working those areas. Prefer linking from a hub doc above rather than duplicating.

---

## Conventions

- Prefer updating an existing hub over inventing a parallel guide.
- Production POS is **not** `{slug}.regkasse.at` — see [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md).
- Cross-tenant access → HTTP **404** (not 403).
- Link package READMEs for setup; keep fiscal/RKSV claims diagnostic unless citing official sources.

---

## Full file inventory

Alphabetical list of Markdown under `docs/` (excluding nested package docs). Topic hubs above are the preferred entry points; `release/` holds engineering notes.

### Root `docs/`

| File | Notes |
|------|--------|
| [`acceptance-authoritative-simulation-tests.md`](acceptance-authoritative-simulation-tests.md) | Acceptance simulation tests |
| [`ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md) | Admin FA deploy |
| [`ANDROID_RELEASE_SIGNING.md`](ANDROID_RELEASE_SIGNING.md) | Android signing |
| [`API_CONTRACTS.md`](API_CONTRACTS.md) | API contract notes |
| [`API_LEGACY_DEPRECATION.md`](API_LEGACY_DEPRECATION.md) | Legacy route policy |
| [`AUTH_TWO_FACTOR.md`](AUTH_TWO_FACTOR.md) | SuperAdmin 2FA |
| [`BACKDATED_TAGESABSCHLUSS.md`](BACKDATED_TAGESABSCHLUSS.md) | Backdated daily closing |
| [`backend-postgresql-integration-tests.md`](backend-postgresql-integration-tests.md) | PG integration tests |
| [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) | Backup hub |
| [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md) | Tenant vs System content |
| [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) | Backup RBAC |
| [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md) | Full backup guide |
| [`backup-orchestrator-distributed-lock.md`](backup-orchestrator-distributed-lock.md) | Orchestrator lock |
| [`backup-phase1-runbook.md`](backup-phase1-runbook.md) / [`backup-phase2-runbook.md`](backup-phase2-runbook.md) | Phase runbooks |
| [`beta-env-matrix.md`](beta-env-matrix.md) / [`beta-smoke-checklist.md`](beta-smoke-checklist.md) | Beta |
| [`BILLING_*.md`](BILLING_TENANT_LICENSE.md) | License sales + tests |
| [`CASH_REGISTER_LIFECYCLE.md`](CASH_REGISTER_LIFECYCLE.md) | Register lifecycle |
| [`CHANGELOG*.md`](CHANGELOG.md) | Docs changelogs |
| [`contributing-i18n.md`](contributing-i18n.md) | i18n notes |
| [`CUSTOMER_ONBOARDING.md`](CUSTOMER_ONBOARDING.md) | Mandant onboarding |
| [`DEP_EXPORT_*.md`](DEP_EXPORT_DEVELOPMENT.md) | DEP §7 |
| [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) | Website / app generators |
| [`EMAIL_CONFIGURATION.md`](EMAIL_CONFIGURATION.md) | SMTP |
| [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | FO cutover |
| [`finanzonline-*.md`](finanzonline-pilot-go-no-go-gate.md) | FO pilot / BMF templates |
| [`IMPERSONATION.md`](IMPERSONATION.md) / [`IMPERSONATION_FLOW.md`](IMPERSONATION_FLOW.md) | Super Admin impersonation |
| [`inventory-lager-optional.md`](inventory-lager-optional.md) | Optional inventory |
| [`LICENSE_*.md`](LICENSE_SYSTEM.md) | License system / design |
| [`MULTI_TENANT.md`](MULTI_TENANT.md) | **Key** — tenancy |
| [`OFFLINE_*.md`](OFFLINE_SYSTEM_INDEX.md) | Offline systems |
| [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) | Online orders inbox |
| [`ops-known-gaps.md`](ops-known-gaps.md) | Ops gaps |
| [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) | Role × permission |
| [`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md) | **Key** — single POS UI |
| [`PROJECT_SUMMARY.md`](PROJECT_SUMMARY.md) | Product summary |
| [`README.md`](README.md) | This index |
| [`REGKASSE_APK_INSTALLATIONSANLEITUNG.md`](REGKASSE_APK_INSTALLATIONSANLEITUNG.md) | APK install (de) |
| [`restore-*.md`](restore-boundary-notes.md) | Restore boundary / drills |
| [`RKSV_*.md`](RKSV_COMPLIANCE.md) | RKSV / BMF / operations |
| [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md) | **Key** — FA mandant CRUD |
| [`USER_MANAGEMENT.md`](USER_MANAGEMENT.md) | **Key** — users / Access hub |
| [`WORKING_HOURS.md`](WORKING_HOURS.md) | Website hours only |

### `docs/release/`

Engineering / go-live notes (FO reconciliation, offline separation, fiscal export, metrics, coverage). Prefer hub docs for day-to-day ops; use `release/` when implementing or verifying a specific wave.

| Area | Examples |
|------|----------|
| Offline | [`OFFLINE_SYSTEMS_SEPARATION.md`](release/OFFLINE_SYSTEMS_SEPARATION.md), [`POS_OFFLINE_QUEUE_UX.md`](release/POS_OFFLINE_QUEUE_UX.md), … |
| FinanzOnline | [`FINANZONLINE_RECONCILIATION.md`](release/FINANZONLINE_RECONCILIATION.md), retry/alerting, test-mode E2E |
| Fiscal export | [`FISCAL_EXPORT_DIAGNOSTICS.md`](release/FISCAL_EXPORT_DIAGNOSTICS.md), terminology, migration timeline |
| Go-live / review | [`GO_LIVE_FISCAL_CHECKLIST.md`](release/GO_LIVE_FISCAL_CHECKLIST.md), [`TECH_REVIEW_BACKLOG.md`](release/TECH_REVIEW_BACKLOG.md) |

**Last reviewed:** 2026-07-21 — key hubs aligned with Single POS UI, `/backup` FA routes, and backup auto-delete audit.