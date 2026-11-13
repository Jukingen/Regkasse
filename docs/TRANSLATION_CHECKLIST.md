# Documentation translation checklist

Scan date: **2026-08-23**. Method: unique Turkish letters (`ğşşııİ`) plus common Turkish function words. German umlauts alone do **not** count as Turkish.

**Standards:** [`DOCUMENTATION_STANDARDS.md`](DOCUMENTATION_STANDARDS.md)

**Totals (source markdown, excluding `node_modules` / `.git` / build trees):** about **287** files — **227 English**, **50 Turkish**, **6 mixed**, **4 German** (`*.de.md`).

Effort is engineering-hours of careful translation (not machine dump). Fiscal docs need a second pass against code.

---

## Policy (do not “fix” these)

| Item | Action |
|------|--------|
| POS UI German strings | Keep |
| `AGENTS.md` IDE explanations = Turkish | Keep |
| `docs/*.de.md` | Keep as German operator twins |
| Product name **POS** | Keep (do not globally replace with “cash register”) |
| RKSV receipt type names | Keep German proper nouns |
| `frontend/archive/`, `testsprite` tmp | Low priority / skip |

The template names `frontend-sites`, `tools`, `localization` are **not** this repo. Use `frontend-sites`, `tools`, `localization`. There is no Fiskaly-only product rename; TSE cutover lives in [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md).

---

## Root files

| File | Language | Type | Priority | Effort | Status |
|------|----------|------|----------|--------|--------|
| `README.md` | English | User / technical | High | — | Done (already EN) |
| `CONTRIBUTING.md` | English | User | High | — | Done |
| `DEPLOYMENT.md` | English | Technical | High | — | Done |
| `DEVELOPMENT.md` | English | Technical | High | — | Done |
| `API_CONTRACT.md` | English | API | High | — | Done |
| `REGKASSE_AI_ONBOARDING.md` | English | Technical | High | — | Done |
| `AGENTS.md` | English (mixed: TR IDE rule + DE UI examples) | Technical | High | 0.5 h | Done — EN body; language table updated 2026-08-23 |
| `SECURITY.md` | English | Technical | Medium | — | Done |
| `CHANGELOG.md` | English | User | Low | — | Done |

---

## Packages

| File | Language | Type | Priority | Effort | Status |
|------|----------|------|----------|--------|--------|
| `backend/README.md` | English | Technical | High | — | Done |
| `frontend/README.md` | English | Technical | High | — | Done |
| `frontend-admin/README.md` | English (tiny mixed) | Technical | High | 0.2 h | Done enough — spot-check leftover TR |
| `frontend-sites/README.md` | English | Technical | High | — | Done |
| `scripts/README.md` | English | Technical | Medium | — | Done |
| `tools/README.md` | English | Technical | Medium | — | Done |
| `localization/README.md` | English | Technical | Medium | — | Done |
| `ai/README.md` | English | Technical | High | — | Done |
| `docs/README.md` | English | User | High | — | Done |
| `monitoring/README.md` | English | Technical | Medium | — | Done |
| `.github/workflows/README.md` | English | Technical | Medium | — | Done |

---

## Docs folder (named hubs)

| File | Language | Type | Priority | Effort | Status |
|------|----------|------|----------|--------|--------|
| `docs/README.md` | English | User | High | — | Done |
| `docs/MULTI_TENANT.md` | English | Technical | High | — | Done |
| `docs/BACKUP_SYSTEM.md` | English | Technical | High | — | Done |
| `docs/FISKALY_PRODUCTION_CUTOVER.md` | English | Technical | High | — | Done |
| `docs/TENANT_LIMITS.md` | English | Technical | High | — | Done |
| `docs/ALERTING.md` | English | Technical | Medium | — | Done |
| `docs/MONITORING.md` | English | Technical | Medium | — | Done |
| `docs/DOCKER.md` | English | Technical | Medium | — | Done |
| `docs/DOCKER.de.md` (and other `*.de.md`) | German | User | — | — | **Keep German** (intentional) |
| `docs/DOCUMENTATION_STANDARDS.md` | English | User | High | — | Done (created) |
| `docs/TRANSLATION_CHECKLIST.md` | English | User | High | — | Done (this file) |

Most other `docs/*.md` files are already English. Remaining **Turkish** docs are listed below.

---

## Remaining Turkish → English (work queue)

### High — `ai/` contracts (agent-facing)

| File | Type | Effort | Status |
|------|------|--------|--------|
| `ai/00_CONTEXT_README.md` | Technical | 0.4 h | Done (2026-08-23) |
| `ai/01_BACKEND_CONTRACT.md` | Technical | 0.5 h | Done (2026-08-23) |
| `ai/02_DATABASE_CONTRACT.md` | Technical | 0.5 h | Done (2026-08-23) |
| `ai/02_DATABASE_OVERVIEW.md` | Technical | 0.3 h | Done (2026-08-23) |
| `ai/03_API_CONTRACT.md` | API | 0.3 h | Done (2026-08-23) |
| `ai/04_FRONTEND_CONTRACT.md` | Technical | 0.3 h | Done (2026-08-23) |
| `ai/05_SECURITY_COMPLIANCE.md` | Technical | 0.5 h | Done (2026-08-23) |
| `ai/06_TASK_TEMPLATE.md` | Technical | 0.2 h | Done (2026-08-23) |
| `ai/07_DO_NOT_TOUCH.md` | Technical | 0.4 h | Done (2026-08-23) |
| `ai/08_API_CONTRACT_STABILIZATION_PLAN.md` | API | 0.2 h | Done (2026-08-23) |
| `ai/08_FILE_MAP.md` | Technical | 0.2 h | Done (2026-08-23) |
| `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` | API | 0.2 h | Done (2026-08-23) |
| `ai/10_API_BOUNDARY_POLICY.md` | API | 0.2 h | Done (2026-08-23) |
| `ai/11_OPENAPI_CONTRACT_GOVERNANCE.md` | API | 0.2 h | Done (2026-08-23) |
| `ai/12_ADMIN_LEGACY_WRAPPER_REMOVAL.md` | Technical | 0.2 h | Done (2026-08-23) |
| `ai/13_POS_RESPONSE_NORMALIZATION_INVENTORY.md` | Technical | 0.2 h | Done (2026-08-23) |
| `ai/modules/payments.md` | Technical | 0.1 h | Done (2026-08-23) |
| `ai/modules/tse_finanzonline.md` | Technical | 0.1 h | Done (2026-08-23) |

### High — operator / fiscal `docs/`

| File | Type | Effort | Status |
|------|------|--------|--------|
| `docs/AUSFALL_BENACHRICHTIGUNG_PLAN.md` | Technical | 1.5 h | Done (2026-08-23) |
| `docs/FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md` | Technical | 1.5 h | Done (2026-08-23) |
| `docs/RKSV_CASH_REGISTER_OPERATIONS.md` | Technical | 2.5 h | Done (2026-08-23) |
| `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md` | Technical | 2.0 h | Done (2026-08-23) |
| `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md` | Technical | 1.5 h | Done (2026-08-23) |
| `docs/RKSV_COMPLIANCE_ASSESSMENT.md` | Technical | 1.5 h | Done (2026-08-23) |
| `docs/RKSV_ACTION_PLAN.md` | Technical | 1.0 h | Done (2026-08-23) |
| `docs/RKSV_IMPLEMENTATION_READINESS.md` | Technical | 1.0 h | Done (2026-08-23) |
| `docs/RKSV_FINAL_VALIDATION_CHECKLIST.md` | Technical | 0.8 h | Done (2026-08-23) |
| `docs/RKSV_PRODUCTION_CUTOVER_CHECKLIST.md` | Technical | 0.5 h | Done (2026-08-23) |
| `docs/TSE_PRODUCTION_CONFIG_LOCK.md` | Technical | 1.5 h | Done (2026-08-23) |
| `docs/MAI_2027_SIGNATURKARTE_PLAN.md` | Technical | 1.0 h | Done (2026-08-23) |
| `docs/MONATSBELEG_FINANZONLINE_DECISION.md` | Technical | 0.5 h | Done (2026-08-23) |

### Medium — `docs/release/` and other `docs/`

| File | Type | Effort | Status |
|------|------|--------|--------|
| `docs/release/STRUCTURAL_FALLBACK_REMOVAL_PLAN.md` | Technical | 1.0 h | Done (2026-08-23) |
| `docs/release/OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md` | Technical | 0.8 h | Done (2026-08-23) |
| `docs/release/FINANZONLINE_RECONCILIATION.md` | Technical | 1.2 h | Done (2026-08-23) |
| `docs/release/RISK_CONTROL_EVIDENCE_TABLE.md` | Technical | 1.0 h | Done (2026-08-23) |
| `docs/release/CORE_METRICS_PROMETHEUS.md` | Technical | 0.6 h | Done (2026-08-23) |
| `docs/release/DEVICE_SEQUENCE_COVERAGE.md` | Technical | 0.6 h | Done (2026-08-23) |
| `docs/release/COVERAGE_GUARD_ACTIONABLE.md` | Technical | 0.3 h | Done (2026-08-23) |
| `docs/release/FINANZONLINE_RETRY_AND_ALERTING.md` | Technical | 0.4 h | Done (2026-08-23) |
| `docs/release/OFFLINE_REPLAY_ADVISORY_LOCK_TIMEOUT.md` | Technical | 0.3 h | Done (2026-08-23) |
| `docs/inventory-lager-optional.md` | Technical | 0.4 h | Done (2026-08-23) |
| `docs/REGKASSE_APK_INSTALLATIONSANLEITUNG.md` | User | — | **Keep German** (operator install guide) |
| `docs/FISKALY_PRODUCTION_CUTOVER.md` | Technical | 0.2 h | Done — remaining TR headings (2026-08-23) |
| `docs/SCRIPTS_REFERENCE.md` | Technical | 0.3 h | Review — likely EN with few false-positive hits |

### Medium / low — FA, POS, testsprite

| File | Type | Effort | Status |
|------|------|--------|--------|
| `frontend/i18n/README.md` | Technical | 0.5 h | Done (2026-08-23) |
| `frontend-admin/src/i18n/README.md` | Technical | 0.3 h | Done (2026-08-23) |
| `frontend-admin/docs/FE_ADMIN_MIGRATION.md` | Technical | 0.8 h | Done (2026-08-23) |
| `frontend-admin/docs/tagesabschluss-schema-quality.md` | Technical | 0.3 h | Done (2026-08-23) |
| `frontend-admin/src/features/users/*` (TR READMEs) | Technical | 0.5 h | Done (2026-08-23) |
| `frontend/archive/DEVELOPMENT.md` | Technical | — | Skip (archive) |
| testsprite leftover PRD copies | Technical | — | Skip |

---

## Mixed (spot-check, not full rewrite)

| File | Notes |
|------|--------|
| `AGENTS.md` | English; Turkish only in the IDE-explanation rule |
| `ai/08_FILE_MAP.md` | Translated 2026-08-23 |
| `frontend-admin/README.md` | Almost all English |
| `frontend-admin/ROLE_MANAGEMENT_UI.md` | Mostly English |

---

## Proofreading (step 7)

| Pass | Tool | Status |
|------|------|--------|
| Author pass on new EN files | Human | This change (`ai/` + standards) |
| Grammarly / LanguageTool | Not run in-repo | Optional locally |
| VS Code spell checker | Optional | Enable `cSpell` for `en` on `docs/` + `ai/` |

---

## Estimated remaining effort

| Bucket | Hours |
|--------|-------|
| `ai/` contracts | Done |
| High fiscal `docs/` | Done |
| `docs/release/` Turkish set | Done |
| FA leftover READMEs | Done |
| German operator twins (`*.de.md`, APK install guide) | Keep DE |
| Archive / testsprite | Skip |
| Optional proofreading / `SCRIPTS_REFERENCE.md` spot-check | ~1–2 |
| **Remaining** | **~1–2 h optional polish** |
