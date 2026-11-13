# Development bypass — restore before production

**Status:** active during development (2026-11-10).  
**Remove this bypass before production go-live.**

Direct pushes to `main` are allowed while this bypass is in effect so a fix does not wait on a pull request and the full CI matrix. Fiscal checks stay in the backend unit-test suite. Playwright, i18n, and OpenAPI checks still exist; they run on pull requests and `workflow_dispatch`, not on every push to `main`.

## What still runs on a push to `main`

| Check | Workflow | Why it stays |
| --- | --- | --- |
| Backend unit tests (excludes `Category=PostgreSql`) | `Backend unit tests` / job `dotnet build + test (exclude Category=PostgreSql)` | Fiscal and domain unit tests. Path-filtered to `backend/**`. |
| FA Typecheck | `Frontend Admin CI` / job `FA Typecheck` | Fast admin gate. Path-filtered to admin, permissions, and localization. |
| Backend CI (build, test, Staging deploy) | `Backend CI` | Unchanged. Every push to `main` still builds, tests, and can deploy Staging. |
| Deploy image publish | `Deploy` | Unchanged. Every push to `main` still publishes images. |
| POS lint, typecheck, unit tests | `Frontend POS CI` | Unchanged, except the i18n step is skipped on push. |
| DEP Prüftool, secret scan, PostgreSQL integration | existing workflows | Unchanged. Not part of this bypass. |

`api-client-auto-generate.yml` still runs when `backend/swagger.json` is pushed. It updates the generated client; it is not a blocking OpenAPI diff.

## What does not run on a push to `main`

These workflows and jobs still run on pull requests and when started by hand (Actions → Run workflow):

| Check | Workflow / job |
| --- | --- |
| Playwright | `Frontend Admin CI` / `E2E (Playwright)`. POS Playwright stays manual (`Frontend POS CI`, input `run_e2e`). |
| i18n | `Localization Validation`. Admin i18n steps live in the full Admin quality job, which is pull-request / manual only. The POS i18n step is skipped on push. |
| OpenAPI | `API client alignment (OpenAPI / Orval)`, `API contract validation`, `API contract tests (OpenAPI + backend + Admin/POS smoke)`, `TestSprite specs & API smoke`. |

Admin lint, unit tests, and production build run with that full quality job on pull requests and manual runs. A push to `main` that touches the admin paths runs **FA Typecheck** only.

## Re-enable branch protection (GitHub UI)

Do this before production go-live. Classic branch protection is not configured on `main`. The repository ruleset is **Protect main** (ruleset id `25855694`).

1. Open [Rulesets for Jukingen/Regkasse](https://github.com/Jukingen/Regkasse/settings/rules).
2. Open the ruleset **Protect main**.
3. Set **Enforcement status** to **Active**.
4. Under **Target branches**, include the default branch `main` (and `master` if that branch still exists).
5. Enable **Require a pull request before merging**.
   - Set the required approving reviews your release process needs (at least 1).
   - Turn off **Allow specified actors to bypass** for day-to-day pushes, or leave bypass only for a break-glass admin.
6. Enable **Block force pushes**.
7. Enable **Require status checks to pass**.
   - Require branches to be up to date before merging.
   - Add every check below. Names are `Workflow name / Job name` as GitHub shows them in the ruleset picker.
8. Save the ruleset.

### Checks to require again

Development-only gate (optional while the bypass is on):

- `Backend unit tests / dotnet build + test (exclude Category=PostgreSql)`
- `Frontend Admin CI / FA Typecheck`

Add these back before production, in addition to the two above:

- `Frontend Admin CI / Lint · Typecheck · Test · Build`
- `Frontend Admin CI / E2E (Playwright)`
- `Localization Validation / localization`
- `API client alignment (OpenAPI / Orval) / Orval drift check + admin smoke`
- `API contract validation / Swagger diff + backend tests`
- `API contract tests (OpenAPI + backend + Admin/POS smoke) / Schema validation + contract + smoke`
- `TestSprite specs & API smoke / Validate API YAML vs OpenAPI`
- `Frontend POS CI / Lint · Typecheck · Test`
- `Backend CI` jobs you already required for Staging (build/test)
- `Backend Postgres integration tests` if that check was required before

If a check name in the picker differs by a suffix, pick the job that matches the workflow file in `.github/workflows/`. Do not delete the workflow files. This bypass only changes **when** they run.

## Re-enable the push triggers in git

After the ruleset is active, put the slow checks back on `push` to `main` / `master`:

1. `frontend-admin-ci.yml` — remove `if: github.event_name != 'push'` from the `quality` and `e2e` jobs so lint, tests, build, i18n, and Playwright run on push again. Keep the `FA Typecheck` job.
2. `frontend-ci.yml` — remove `if: github.event_name != 'push'` from the i18n step.
3. Restore the `push: branches: [main, master]` block (with the previous `paths:` filters) on:
   - `localization-validation.yml`
   - `api-client-alignment.yml`
   - `api-contract.yml`
   - `api-contract-tests.yml`
   - `testsprite.yml`
4. Update the trigger column in `.github/workflows/README.md` to match.
5. Delete this file, or replace the status line with **retired** and the date, so nobody treats the bypass as the production policy.

Search the workflows for `docs/DEVELOPMENT_BYPASS.md` to find every bypass comment.
