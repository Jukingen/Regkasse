# 07 — API Contracts, CI/CD, Testing and Workflow

> Part 7 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24).

## 1. API boundaries (do not cross)

| Client | Allowed prefixes | Forbidden |
|---|---|---|
| POS (`frontend/`) | `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` (plus the shared endpoints existing POS services already use, such as `/api/rksv/*`, `/api/offline-transactions/*`, `/api/license/*`) | `/api/admin/*` |
| Admin (`frontend-admin/`) | `/api/admin/*`, `/api/Auth/*` | `/api/pos/*` |
| Sites / customer app | `/api/public/*`, `/api/sites/{slug}/status` | Authenticated POS/Admin APIs |
| Shared | `/api/Auth/*`, `/api/user/*`, `/api/rksv/*` | n/a |

- Removed legacy aliases `/api/Payment`, `/api/Cart`, `/api/Product` (hard-removed 2026-08-13) must not be reintroduced. Canonical routes: `/api/pos/payment/*`, `/api/pos/cart/*`, `/api/pos/*`. Admin catalog CRUD is `/api/admin/products`. See `docs/API_LEGACY_DEPRECATION.md`.
- Policy docs: `ai/10_API_BOUNDARY_POLICY.md`, `ai/03_API_CONTRACT.md`, `ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`, `docs/API_CONTRACTS.md`.
- Error conventions: HTTP 404 for cross-tenant, 409 `LIMIT_EXCEEDED` for tenant caps, 403 for missing permission, domain error codes for guardrails (for example RKSV guard codes). English technical messages from the API; clients localize.

## 2. OpenAPI → Orval workflow

```text
backend controllers/DTOs
   → node scripts/generate-backend-openapi.mjs      (writes backend/swagger.json)
   → cd frontend-admin && npm run generate:api       (Orval → src/api/generated/**)
   → node scripts/verify-api-client.mjs              (drift check; Husky + CI)
```

- `backend/swagger.json` is committed. Current size: about 1,245 paths, OpenAPI 3.0.4.
- Other validators: `node scripts/validate-critical-openapi-paths.mjs` (critical paths exist), `scripts/validate-api-contract.mjs`, CI `api-contract.yml` (backward-compat diff) and `api-contract-tests.yml`.
- The POS does not use Orval. POS paths are hand-maintained in `frontend/services/api/apiPaths.ts` and must match the OpenAPI spec exactly.
- When a backend change affects FA: commit `swagger.json` and the generated client together. If it does not affect the API surface, do not regenerate.

## 3. Repository scripts worth knowing

| Command (root) | Purpose |
|---|---|
| `npm install` | Install all workspaces; `prepare` installs Husky hooks |
| `npm run dev` | Backend + admin in parallel (RAM-safe, cleans orphan workers) |
| `npm run dev:all` | Backend + admin + POS + sites |
| `npm run dev:backend|dev:admin|dev:pos|dev:sites|dev:marketing` | Single app |
| `npm run build|test|lint` | All workspaces (`--if-present`) |
| `npm run typecheck` | Admin, POS, Sites, Marketing |
| `npm run verify:api-client` | Orval / OpenAPI drift check |
| `npm run verify:openapi` | Critical OpenAPI paths |
| `npm run verify:permission-keys` / `verify:menu-permissions` | Backend ↔ FA permission and menu alignment |
| `npm run verify:secrets` / `test:secrets` | Tracked-file secret scan |
| `npm run i18n:validate` / `i18n:ci` | Localization gates |
| `npm run seed:test-data` | Seed test data |
| `npm run dev:cleanup` | Kill leftover Next/Expo node workers (Windows RAM leak) |

## 4. Git hooks (Husky)

`.husky/pre-commit` → `scripts/git-hooks/pre-commit.mjs`: secret scan (always); full API client verify only when `backend/swagger.json` or `frontend-admin/src/api/generated/**` is staged; lint for packages with staged files (backend build only for `backend/**`); typecheck per staged source tree; tests off by default (`HUSKY_RUN_TESTS=1` enables fast contract tests). Escape hatches exist (`SKIP_PRECOMMIT`, `SKIP_SECRET_SCAN`, `SKIP_API_CLIENT_VERIFY`, `SKIP_PRECOMMIT_LINT`, `SKIP_PRECOMMIT_TYPECHECK`). Do not recommend bypassing them casually.

## 5. CI/CD (GitHub Actions, about 30 workflows)

Full inventory: `.github/workflows/README.md`. Guide: `docs/CI_CD.md`.

| Group | Workflows |
|---|---|
| Umbrella | `ci.yml` (backend + admin + POS tests, Docker build without push) on pull requests |
| Backend | `backend-ci.yml`, `backend-unit-tests.yml` (excludes `Category=PostgreSql`), `backend-postgres-integration-tests.yml`, `backend-coverage.yml` |
| Admin | `frontend-admin-ci.yml` (lint, typecheck, test, build, Playwright E2E on pull requests; typecheck-only on push to `main`), `frontend-admin-e2e.yml`, `frontend-admin-deploy.yml` |
| POS | `frontend-ci.yml` (+ alias `frontend-pos-ci.yml`), `frontend-eas-build.yml` (manual EAS build) |
| Sites | `frontend-sites-ci.yml` |
| Contracts and i18n | `api-client-alignment.yml`, `api-client-auto-generate.yml`, `api-contract.yml`, `api-contract-tests.yml`, `localization-validation.yml` (hard gate: `--strictMissing true`; admin also `--orphanPolicy error`) |
| Fiscal | `dep-prueftool.yml` (BMF DEP Prüftool with JDK 17), `fiscal-validation.yml` (manual) |
| Security and hygiene | `secret-scan.yml`, `dependabot-auto-merge.yml`, `scripts-bat-ps1-pairing.yml`, `testsprite.yml` |
| Deploy | `deploy.yml` (multi-image GHCR push; staging; production only with a confirm phrase), `deploy-backend-stage.yml` (reusable: migrate → deploy → smoke → rollback), `deploy-canary.yml` (progressive tenant canary), `deploy-production.yml` (compliance gate + migrate approval + smoke; auto-rollback off) |
| Ops | `uptime-check.yml` (every 5 minutes), `notify-failure.yml` (optional Slack via `SLACK_WEBHOOK_URL`) |

Rules:

- A push to `main`/`master` starts a staging deploy. **No direct pushes to `main`/`master`.** Branch protection is active. Never use `--admin` merges to bypass it.
- Production deploy is gated by environments, confirm phrases, and the compliance gate. Rollback on production is manual.
- `.github/workflows/backend-unit-tests.yml` also runs `CountryLayerBypassGateTests` on pull requests that touch country-layer code.
- Dependabot has a guard. Do not run `npm audit fix --force` (it downgrades Expo or jumps the router). See `docs/SECURITY_ADVISORIES.md` (note: that file still describes Expo SDK 57; `docs/AI_CONTEXT.md` says it needs a refresh).

## 6. Testing strategy

| Area | Tool | Where | Typical command |
|---|---|---|---|
| Backend unit and integration | xUnit, FluentAssertions, Moq, Mvc.Testing, Testcontainers (PostgreSQL) | `backend/KasseAPI_Final.Tests/` (about 900 test files) | `dotnet test backend/KasseAPI_Final.sln` |
| Backend focused | filter by name | e.g. `TenantIsolation`, `RksvDepExportServiceTests`, RKSV special receipt service tests | `dotnet test ... --filter "FullyQualifiedName~..."` |
| POS unit/contract | Jest + jest-expo | `frontend/__tests__`, `frontend/tests` | `npm run test:pos` or `npm run test:contract` in `frontend/` |
| FA unit/contract | vitest 4 | `frontend-admin/src/**/__tests__` | `npm run test:admin` |
| E2E | Playwright | `frontend/e2e`, `frontend-admin/tests` | `npm run test:e2e` in each package |
| API/E2E definitions | TestSprite | `testsprite/`, `testsprite_tests/` | via `testsprite.yml` |
| Fiscal verification | BMF Prüftool (JDK 17+) | `scripts/rksv/`, `backend/Tests/*.jar` | `scripts/rksv/verify-rksv-dep-export.ps1` |
| Load test | JS load script (`pos-critical-path.js`) plus tenant/cashier seed helpers | `scripts/load-test/` (see its README), `docs/LOAD_TEST_FINDINGS.md` | Production target 50-200 cashiers; 100 pass a 3 s lab budget, 200 do not, 500 is a stress test |

PR-ready minimum (from `CONTRIBUTING.md` and `105-testing.mdc`): unit tests pass, integration tests pass when applicable, no `console.log` in production code, no TypeScript `any`, no hardcoded UI strings, audit events exist for sensitive operations. Run when relevant:

```bash
npm run test:backend && npm run test:admin && npm run test:pos
npm run verify:api-client && npm run verify:bat-ps1 && npm run validate:scripts && npm run i18n:ci
node scripts/verify-permission-keys.mjs && node scripts/verify-menu-permissions.mjs
node scripts/git-hooks/scan-secrets.mjs --tracked
```

## 7. Workflow rules (commits and pull requests)

- Commit messages in English, explaining why (`feat: add username login support`). No secrets (`.env`, PEMs, production keys).
- Land changes through a focused pull request with a clear description. Never push to `main`/`master`.
- For medium or large changes include: a short plan, affected files, main risks, backward-compatibility impact, and a test strategy.
- After OpenAPI changes: regenerate `swagger.json`, regenerate the FA client, run `verify-api-client`.
- Sensitive operations need audit events; high-risk areas need extra review (`ai/07_DO_NOT_TOUCH.md`).

## 8. Deployment facts

- Hosts: `pos.regkasse.at`, `admin.regkasse.at`, `api.regkasse.at`; wildcard DNS for tenant hosts where used; the reverse proxy must preserve the original `Host` header. TLS must cover the hosts.
- `ASPNETCORE_ENVIRONMENT=Production` is required in production (otherwise tenant headers could spoof tenants).
- Backend container listens on 8080 (multi-stage `Dockerfile`). Compose files and Windows helper scripts exist (`docs/DOCKER*.md`, `scripts/docker-*.ps1`).
- Config and environment details: `backend/CONFIGURATION.md`, `.cursor/rules/201-deploy.mdc`, `docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md`, `docs/PRODUCTION_CUTOVER.md`, `docs/FISKALY_PRODUCTION_CUTOVER.md`, `docs/DATABASE_MIGRATION_STRATEGY.md`.
- Monitoring: Sentry, uptime probes, Slack alerts, FA `/admin/monitoring`, Prometheus metrics, ElmahCore (`docs/MONITORING.md`).
