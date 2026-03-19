# Go-live: fiscal schema checklist

1. **Staging DB** = production copy (anonymized if needed).
2. Run `scripts/sql/fiscal_go_live_validation.sql` **before** final `dotnet ef database update` on production (or immediately after on staging that already has all migrations).
3. Resolve every **FAIL** row; review **WARN** rows with finance/compliance.
4. Apply migrations including `EnsureFiscalSequenceAndChainUniqueIndexes` (idempotent index guard).
5. Re-run validation SQL; expect summary `RESULT: OK`.

**Connection example**

```bash
psql "postgresql://user:pass@host:5432/dbname" -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql
```

See `FISCAL_MIGRATION_TIMELINE.md` for migration-level risks (destructive dedup, `KassenId` removal, invoice register gaps).
