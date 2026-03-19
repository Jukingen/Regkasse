# Fiscal go-live validation — CI/release gate

Pipeline integration for `scripts/sql/fiscal_go_live_validation.sql`: migration runs first, then validation; **FAIL** fails the pipeline, **WARN** is saved as an artifact. Validation semantics are unchanged; only execution discipline is enforced.

## Workflow changes

| Item | Description |
|------|-------------|
| **Workflow file** | `.github/workflows/fiscal-validation.yml` |
| **Trigger** | Push/PR to `main`, or `workflow_dispatch`. |
| **Job** | `fiscal-validation`: checkout → setup .NET → install `psql` → apply EF migrations (service Postgres) → run wrapper script. |
| **On FAIL** | Script exits 1 → job fails → pipeline fails. |
| **On WARN** | Script exits 0; full report is in `fiscal_validation_report.txt` → uploaded as artifact `fiscal-validation-report`. |
| **On OK** | Script exits 0; report still uploaded for audit. |

No automatic fix or rollback; the SQL remains read-only.

## Wrapper script

| Script | Use |
|--------|-----|
| `scripts/run_fiscal_go_live_validation.sh` | Linux/macOS and CI. |
| `scripts/run_fiscal_go_live_validation.ps1` | Windows (local/staging). |

**Required env:** `DATABASE_URL` (PostgreSQL URL, e.g. `postgresql://user:pass@host:5432/dbname`).

**Optional env:**  
- `REPO_ROOT` — repo root (default: parent of `scripts/`).  
- `FISCAL_VALIDATION_REPORT_PATH` — report file path (default: `fiscal_validation_report.txt` in repo root).

**Behaviour:**

1. Run `psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql`, write stdout to the report file.
2. If `psql` exits non-zero (connection/SQL error) → print report, exit 1.
3. If report contains `RESULT: FAIL` → exit 1 (pipeline fail).
4. If report contains `RESULT: WARN` → exit 0, report path printed (in CI, upload as artifact).
5. Otherwise → exit 0 (OK).

## Failure semantics

| Condition | Exit code | Pipeline | Artifact |
|-----------|-----------|----------|----------|
| `DATABASE_URL` unset | 1 | Fail | — |
| SQL file missing | 1 | Fail | — |
| `psql` error (e.g. connection) | 1 | Fail | — |
| Script output contains `RESULT: FAIL` | 1 | Fail | — |
| Script output contains `RESULT: WARN` | 0 | Pass | Report uploaded |
| Script output `RESULT: OK` | 0 | Pass | Report uploaded |

## Commands (local / staging / prod-like)

**Local (Linux/macOS, after migration):**

```bash
export DATABASE_URL="postgresql://user:pass@localhost:5432/regkasse"
bash scripts/run_fiscal_go_live_validation.sh
```

**Local (Windows PowerShell):**

```powershell
$env:DATABASE_URL = "postgresql://user:pass@localhost:5432/regkasse"
./scripts/run_fiscal_go_live_validation.ps1
```

**Staging / prod-like (run after migration on that DB):**

```bash
export DATABASE_URL="postgresql://user:pass@staging-host:5432/regkasse"
bash scripts/run_fiscal_go_live_validation.sh
# Inspect fiscal_validation_report.txt; on FAIL fix and re-run.
```

**Direct psql (unchanged, no wrapper):**

```bash
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql
```

## Example output

**OK:**

```
Running fiscal go-live validation (read-only)...
  SQL: /repo/scripts/sql/fiscal_go_live_validation.sql
  Report: /repo/fiscal_validation_report.txt

 check_id                                      | severity | metric | detail
-----------------------------------------------+----------+--------+--------
 ...
RESULT: OK — no FAIL/WARN flags.

Fiscal validation: OK — no FAIL/WARN.
```

**WARN (pipeline passes, artifact has report):**

```
...
RESULT: WARN — review before go-live.

Fiscal validation: WARN — review before go-live. Full report saved to: /repo/fiscal_validation_report.txt
  (In CI, upload this file as an artifact.)
```

**FAIL (pipeline fails):**

```
...
RESULT: FAIL — do not go-live until resolved.

Fiscal validation: FAIL — do not go-live until resolved. Pipeline failed.
```

## Checklist reference

See `docs/release/GO_LIVE_FISCAL_CHECKLIST.md` for the full go-live checklist and when to run validation (before/after production migration, staging, CI).
