# Go-live: fiscal schema checklist (repo-current)

1. **Staging DB** = production copy (anonymized if needed).
2. Run fiscal validation **before** final `dotnet ef database update` on production (or **immediately after** on staging that already has all migrations).
3. Resolve every **FAIL**; review **WARN** with finance/compliance.
4. Apply migrations (including `EnsureFiscalSequenceAndChainUniqueIndexes`).
5. Re-run validation; expect **RESULT: OK**.

## CI / release gate

- **Workflow:** `.github/workflows/fiscal-validation.yml` — on push/PR to `main` (and `workflow_dispatch`).
- **Steps:** Checkout → apply EF migrations (service Postgres) → run `scripts/run_fiscal_go_live_validation.sh`.
- **FAIL** → pipeline fails. **WARN** → pipeline passes; full report is uploaded as artifact `fiscal-validation-report`.
- See `FISCAL_VALIDATION_CI.md` for failure semantics and example output.

## Commands

**Local (from repo root, after DB is migrated):**

```bash
export DATABASE_URL="postgresql://user:pass@host:5432/dbname"
bash scripts/run_fiscal_go_live_validation.sh
```

**Windows (PowerShell):**

```powershell
$env:DATABASE_URL = "postgresql://user:pass@host:5432/dbname"
./scripts/run_fiscal_go_live_validation.ps1
```

**Direct psql (no wrapper):**

```bash
psql "postgresql://user:pass@host:5432/dbname" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql
```

**Staging / prod-like:** Use the same wrapper with `DATABASE_URL` pointing at the target DB; run **after** migrations on that DB.

## References

- `FISCAL_MIGRATION_TIMELINE.md` — migration-level risks (destructive dedup, `KassenId` removal, invoice register gaps).
- `FISCAL_VALIDATION_CI.md` — pipeline integration, wrapper, failure semantics, example output.
