# Backend verification — 2026-10-28

Report only. Not a merge gate. Compared with `origin/main` at `84035d9d`.

**Verdict: needs fix.** API contract is green. Backend PostgreSQL integration did not execute.

## Axis 1 — API contract

| Check | Result |
|-------|--------|
| `node scripts/generate-backend-openapi.mjs` | Pass. Swagger written. Build 0 errors, 51 warnings. |
| `node scripts/verify-api-client.mjs` | Pass. Generated client matches `backend/swagger.json`. |
| `node scripts/validate-critical-openapi-paths.mjs` | Pass. Critical paths and payment schemas present. |
| `git diff origin/main -- backend/swagger.json` | No content change. Working-tree noise was CRLF only and was restored. |
| `git diff origin/main -- frontend-admin/src/api/generated` | No content change. Same CRLF-only noise, restored. |

OpenAPI drift: **none**. No additive, breaking, or bug-fix path list.

Orval printed a non-blocking `securitySchemes/Bearer` schema warning and still exited 0. That warning is not a path change.

## Axis 2 — Backend integration

Local `dotnet test --filter Category=PostgreSql` was not run. Docker Desktop is not running (`npipe:////./pipe/dockerDesktopLinuxEngine` missing), so Testcontainers cannot start.

CI substitute: [Backend PostgreSQL integration tests run 40434466308](https://github.com/Jukingen/Regkasse/actions/runs/40434466308) on `main`, conclusion **success**.

| Field | Value |
|-------|--------|
| Commit | `f716cc60` — `feat(seed): vertical-specific demo data for the demo tenant (#157)` |
| Why not `84035d9d` | Later `main` commits do not touch `backend/**`. The workflow is path-filtered and did not re-run. |
| Job | `dotnet build + test (Category=PostgreSql)` |
| Passed | 1 |
| Failed | 0 |
| Skipped | 56 |
| Total | 57 |
| Duration | 55 ms |

No failing class and no assertion error. `--verbosity minimal` does not print the one passing name or the skip reason.

`REGKASSE_TEST_POSTGRES` was set and the Postgres 16 service was healthy. Tests that use `PostgreSqlReplayFixture` call `Skip.IfNot` when migrate fails or Docker/Testcontainers is unavailable. The log does not include that exception text.

## Risks observed

- The PostgreSQL workflow can stay green while 56 of 57 tagged tests skip. That is not evidence the integration suite ran.
- Current `main` (`84035d9d`) has no newer PostgreSQL run than `f716cc60`.
- Orval's Bearer schema warning is noisy. It did not produce client drift.

## Billing start

No. Do not start a billing feature on this evidence until a PostgreSQL run shows the tagged tests passing, not skipping.

## Follow-up — 2026-10-28 skip diagnosis

**Root cause:** `PostgreSqlReplayFixture` caught a failed `MigrateAsync` and every `Skip.IfNot(HasDatabase)` test skipped, so the job stayed green. The one pass was `DeReceiptSequenceTests.FormatDeBelegNr_IsSlugRegisterSequence`, which never opens a database. On an empty database the first real error is `42P01: relation "products" does not exist` because `20250115000000` / `20250115000001` sort before `InitialCreate`. After those two ids are recorded as applied, the next error is `42701: column "device_id" of relation "auth_sessions" already exists` (`20260528120000` repeats `20260527212105`). After that add is idempotent, migrate still fails with `42703: column "UpdatedAt" of relation "AspNetUsers" does not exist`.

**Fix (not yet a green run):**

- `PostgreSqlReplayFixture.InitializeAsync`: a set `REGKASSE_TEST_POSTGRES` that cannot migrate now throws `InvalidOperationException` instead of setting `SkipReason`. `CI=true` does the same when Testcontainers cannot start.
- Workflow connection string uses `Host=127.0.0.1` and `SSL Mode=Disable`.
- Empty-database bootstrap records the two backdated product migration ids before `MigrateAsync`.
- `20260528120000_AddSessionTrackingAndTenantSessionPolicy.Up` uses `ADD COLUMN IF NOT EXISTS` for the columns the previous migration already adds.

**CI:** [run 40677987856](https://github.com/Jukingen/Regkasse/actions/runs/40677987856) on PR [#162](https://github.com/Jukingen/Regkasse/pull/162). Total 57, failed 57, passed 0, skipped 0. The job is red. The tests are not running to completion.

The integration tests do not yet run to completion; the workflow must stay red until a fresh `MigrateAsync` succeeds.
