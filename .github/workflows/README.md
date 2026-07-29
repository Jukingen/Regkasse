# GitHub Actions workflows

Inventory of CI/CD under `.github/workflows/`. Prefer path filters where noted to keep PR feedback fast. Optional Slack alerts use repository secret `SLACK_WEBHOOK_URL` (see [`notify-failure.yml`](notify-failure.yml)); GitHub also emails users who watch the repo / enable Actions failure emails.

| Workflow | Purpose | Triggers |
|----------|---------|----------|
| [`backend-ci.yml`](backend-ci.yml) | Backend build/test + multi-stage deploy (Staging / Canary / Production) | PR (path-filtered); push `main`/`master`/`release/**` / tag `v*`; `workflow_dispatch` |
| [`ci.yml`](ci.yml) | **Umbrella CI** — backend + Admin + POS tests, Docker build (no push), smoke script syntax | `pull_request` → `main`/`master`; `workflow_dispatch` |
| [`deploy.yml`](deploy.yml) | **Deploy orchestration** — multi-image GHCR push; Staging deploy+smoke; Production only with confirm phrase | push `main`/`master`; `workflow_dispatch` |
| [`deploy-backend-stage.yml`](deploy-backend-stage.yml) | Reusable stage deploy + smoke + rollback + status report | `workflow_call` |
| [`deploy-canary.yml`](deploy-canary.yml) | Manual progressive canary: prefer one tenant, soak hours, smoke, auto-rollback | `workflow_dispatch` |
| [`deploy-production.yml`](deploy-production.yml) | Manual production: compliance gate + confirm, migrate approval, smoke | `workflow_dispatch` |
| [`backend-unit-tests.yml`](backend-unit-tests.yml) | `dotnet build` + `dotnet test` (exclude `Category=PostgreSql`) | `pull_request`, `push` → `main`/`master` |
| [`backend-postgres-integration-tests.yml`](backend-postgres-integration-tests.yml) | PostgreSQL-tagged integration tests + service container | `pull_request`, `push` → `main`/`master` |
| [`frontend-admin-ci.yml`](frontend-admin-ci.yml) | Admin `lint` / `typecheck` / `test` / `build` + Playwright E2E | `pull_request`, `push` → `main`/`master` (path-filtered) |
| [`frontend-admin-e2e.yml`](frontend-admin-e2e.yml) | Standalone / reusable Playwright E2E | `workflow_dispatch`, `workflow_call` |
| [`frontend-admin-deploy.yml`](frontend-admin-deploy.yml) | Build/push admin image + staging/prod hooks | After green Admin CI / `workflow_dispatch` |
| [`frontend-ci.yml`](frontend-ci.yml) | POS (`frontend`) `lint` / `typecheck` / `test` | `pull_request`, `push` → `main`/`master` (path-filtered) |
| [`frontend-sites-ci.yml`](frontend-sites-ci.yml) | Sites `lint` / `typecheck` / `test` / `build` | `pull_request`, `push` → `main`/`master` (path-filtered) |
| [`api-client-alignment.yml`](api-client-alignment.yml) | Orval / OpenAPI drift + admin build smoke | `pull_request`, `push` → `main`/`master` |
| [`api-client-auto-generate.yml`](api-client-auto-generate.yml) | On `swagger.json` push: `generate:api` + commit generated client | `push` → `main`/`master` (`backend/swagger.json`), `workflow_dispatch` |
| [`api-contract.yml`](api-contract.yml) | OpenAPI backward-compat diff + focused backend tests | `pull_request`, `push` → `main`/`master` |
| [`api-contract-tests.yml`](api-contract-tests.yml) | Broader contract suite (OpenAPI + Admin/POS smoke) | `pull_request`, `push` → `main`/`master`, `workflow_dispatch` |
| [`localization-validation.yml`](localization-validation.yml) | i18n hard gate (admin+POS validate/usage) + phased boundary | `pull_request`, `push` → `main`/`master` (path-filtered) |
| [`dep-prueftool.yml`](dep-prueftool.yml) | BMF DEP Prüftool: JDK 17 + fixture smoke + seeded export smoke | `pull_request`, `push` → `main`/`master` (path-filtered), `workflow_dispatch` |
| [`scripts-bat-ps1-pairing.yml`](scripts-bat-ps1-pairing.yml) | Scripts validation: pairing + `SCRIPTS_REFERENCE.md` coverage (`validate-scripts.ps1`) + structural dry-run (`test-scripts.ps1`) | path-filtered PR/push; `workflow_dispatch` |
| [`fiscal-validation.yml`](fiscal-validation.yml) | Fiscal schema migrate + go-live script (manual) | `workflow_dispatch` |
| [`testsprite.yml`](testsprite.yml) | TestSprite YAML ↔ OpenAPI validate (+ optional live smoke) | path-filtered PR/push; `workflow_dispatch` for live |
| [`notify-failure.yml`](notify-failure.yml) | Reusable Slack (optional) failure notifier | `workflow_call` only |

## Caching

- **npm:** `actions/setup-node` lockfile cache + `actions/cache` for `node_modules` where install is heavy.
- **NuGet:** `actions/cache` on `~/.nuget/packages` keyed by backend `*.csproj` / `*.sln`.

## Secrets / variables

| Name | Used by |
|------|---------|
| `SLACK_WEBHOOK_URL` | Failure notifications (optional) |
| `CI_POSTGRES_PASSWORD` | `fiscal-validation.yml` |
| `FA_*_DEPLOY_WEBHOOK_URL` / `FA_*_API_BASE_URL` | Admin deploy |
| `BACKEND_*_DEPLOY_WEBHOOK_URL` / `BACKEND_*_ROLLBACK_WEBHOOK_URL` | Backend multi-stage deploy |
| `BACKEND_*_MIGRATE_WEBHOOK_URL` / `BACKEND_*_DATABASE_CONNECTION` | EF migrations (Staging/Canary auto; Production Environment `backend-production-migrations`) |
| `DEPLOYMENT_STATUS_URL` / `DEPLOYMENT_STATUS_TOKEN` | CI → FA `/admin/deployments` status ingest |
| `ONCALL_WEBHOOK_URL` / `SLACK_WEBHOOK_URL` | Smoke fail / rollback on-call notify |
| `SMOKE_LOGIN_IDENTIFIER` / `SMOKE_LOGIN_PASSWORD` | Optional authenticated smoke |
| `BACKEND_*_API_BASE_URL` / `BACKEND_CANARY_TENANT_IDS` / `BACKEND_FA_BASE_URL` / `BACKEND_POS_BASE_URL` (vars) | Smoke URLs |

Docs: [`DEPLOYMENT.md`](../../DEPLOYMENT.md) · [`docs/CI_CD.md`](../../docs/CI_CD.md) · [`docs/GITHUB_ACTIONS.md`](../../docs/GITHUB_ACTIONS.md) · [`docs/DATABASE_MIGRATION_STRATEGY.md`](../../docs/DATABASE_MIGRATION_STRATEGY.md) · [`docs/DEPLOYMENT_SMOKE_TEST.md`](../../docs/DEPLOYMENT_SMOKE_TEST.md).

Environment checklists (not auto-applied): [`.github/environments/staging.yml`](../environments/staging.yml) · [`production.yml`](../environments/production.yml).

CI helper scripts: [`scripts/ci-build.ps1`](../../scripts/ci-build.ps1) · [`ci-test.ps1`](../../scripts/ci-test.ps1) · [`ci-deploy.ps1`](../../scripts/ci-deploy.ps1).
