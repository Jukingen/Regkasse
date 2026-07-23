# GitHub Actions workflows

Inventory of CI/CD under `.github/workflows/`. Prefer path filters where noted to keep PR feedback fast. Optional Slack alerts use repository secret `SLACK_WEBHOOK_URL` (see [`notify-failure.yml`](notify-failure.yml)); GitHub also emails users who watch the repo / enable Actions failure emails.

| Workflow | Purpose | Triggers |
|----------|---------|----------|
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
