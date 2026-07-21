# EF Core migration squash (high risk)

> **Status (2026-07-21):** Squash is **planned and dry-run validated**, not applied on `main`.
> Do **not** delete `backend/Migrations/` on production branches until Staging and a production DB copy both pass the checklist below.

## Why squash?

| Metric (local inventory) | Value |
|--------------------------|-------|
| Migration `Up` classes | ~240 |
| Designer companions | ~197 |
| `AppDbContextModelSnapshot.cs` | ~474 KB / ~9k lines |
| `Migrations/` folder size | ~50 MB |
| EF-discovered migrations (`dotnet ef migrations list`) | ~221 |
| Pending on fully migrated DBs | 0 (when healthy) |

Threshold in ops guidance: consider squashing above **20–30** migrations. This repo is far past that. Costs: slow builds/CI, hard reviews, duplicate/`CreateTable` footguns, orphan files (e.g. `CleanupOrphanedCarts.cs` without Designer).

Known history issues that squash removes:

- Duplicate timestamp IDs: `20260528120000_AddActivityEvents` vs `20260528120000_AddSessionTrackingAndTenantSessionPolicy`
- Duplicate `CreateTable` migrations (later ones no-op’d for greenfield; see recent fixes)
- Hand-written SQL migrations without Designer

## Non-goals

- No automatic production cutover from this document alone.
- No schema/data change: squash rewrites **migration source + history bookkeeping** only.
- Do **not** run `dotnet ef database update` against an already-current production DB after squash unless history was rewritten first (otherwise EF will try to recreate the entire schema).

## Prerequisites (all must be true)

1. **Every environment** that will consume the squashed branch is on the **same** latest migration id (today: `20260721140000_AddAdminUserFeedback` or newer if more were added).
2. Full **PostgreSQL backup** (logical `pg_dump` + verified restore) of Staging and of a **production copy**.
3. Maintenance window + rollback owner.
4. Team freeze: no parallel `dotnet ef migrations add` until cutover finishes.
5. EF tools aligned with runtime (`Microsoft.EntityFrameworkCore*` **10.0.10** in this repo; prefer matching `dotnet-ef`).
6. **Model greenfield-safe:** invoice btree unique vs GIN trigram indexes use **distinct** `HasDatabaseName` values (see `AppDbContext` Invoice config). Confirm empty-DB `database update` succeeds on a throwaway database before Staging.
7. Empty databases have **`pg_trgm`** available (`CREATE EXTENSION IF NOT EXISTS pg_trgm;`) before GIN trigram indexes are created — bake into baseline `Up` if ops DBs do not enable it by default.

## Official pattern

EF Core has no built-in squash command. Microsoft documents [resetting / squashing](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#resetting-all-migrations):

1. Ensure DBs are up to date.
2. Remove old migration files (+ snapshot).
3. `dotnet ef migrations add <BaselineName>`.
4. On **existing** databases: clear `__EFMigrationsHistory` and insert **one** row for the new baseline (do **not** apply `Up()` — schema already matches).
5. On **empty** databases: `dotnet ef database update` applies the baseline once.

## Controlled rollout order

```text
1) Local dry-run DB copy
2) Staging (app + DB)
3) Production DB *copy* (restore from prod dump → squash rehearsal)
4) Production (only after 2+3 green + explicit approval)
```

Never skip Staging. Never first-try on live Production.

## Step-by-step runbook

Use scripts under `backend/scripts/migration-squash/` when possible.

### A. Preflight

```powershell
cd backend
dotnet ef migrations list --project KasseAPI_Final.csproj --configuration Release
dotnet ef migrations has-pending-model-changes --project KasseAPI_Final.csproj --configuration Release
```

- `has-pending-model-changes` must report **no** pending model changes.
- Target DB must show **no** `(Pending)` migrations.

Capture the current tip migration id for the release notes.

### B. Backup

```powershell
# Example — adjust host/user/db
pg_dump -Fc -h $env:PGHOST -U $env:PGUSER -d kasse_prod -f "kasse_prod_pre_squash_$(Get-Date -Format yyyyMMddHHmm).dump"
```

Restore into an isolated database for rehearsal (`kasse_squash_rehearsal`).

### C. Create baseline branch (code)

Work on a dedicated branch. Prefer an isolated git worktree so `main` stays untouched until review:

```powershell
# From repo root
git worktree add ..\Regkasse-mig-squash -b chore/ef-migration-squash
cd ..\Regkasse-mig-squash\backend
```

Archive then clear migrations (keep the archive outside git or in a release artifact):

```powershell
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$archive = "artifacts\migration-squash-archive-$stamp"
New-Item -ItemType Directory -Force -Path $archive | Out-Null
Copy-Item -Recurse Migrations\* $archive\
Remove-Item -Recurse -Force Migrations\*
```

Generate baseline (after `dotnet restore`). Prefer starting baseline `Up` with:

```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
```

(only if not already guaranteed by the server image / init scripts).

Record the generated file prefix, e.g. `20260721183000_SquashedBaseline_20260721183000` → **MigrationId**.

Optional: mark baseline `Up` as documented baseline (still keep full `CreateTable` for empty DBs). Do **not** strip `Up` — empty environments need it.

Commit **only** the new Migrations folder on the squash branch.

### D. Rewrite history on an existing DB (Staging / prod copy)

Connect to the **copy** (never live prod until final cutover). PostgreSQL identifiers are case-sensitive when quoted:

```sql
BEGIN;

-- Safety: abort if tip is not what you expect (edit expected id)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260721140000_AddAdminUserFeedback'
  ) THEN
    RAISE EXCEPTION 'Database is not on expected tip migration — abort squash history rewrite';
  END IF;
END $$;

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory_backup_pre_squash" AS
TABLE "__EFMigrationsHistory";

DELETE FROM "__EFMigrationsHistory";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('REPLACE_WITH_BASELINE_MIGRATION_ID', '10.0.10');

COMMIT;
```

Then:

```powershell
dotnet ef migrations list --project KasseAPI_Final.csproj --configuration Release --connection "<copy-connection>"
dotnet ef migrations has-pending-model-changes --project KasseAPI_Final.csproj --configuration Release
```

Expect: **one** migration, applied; **no** pending model changes.

**Do not** run `database update` on this DB after the INSERT (would be a no-op if history is correct; if history is wrong it may attempt a full recreate and fail destructively).

### E. Empty-database path

```powershell
# Fresh DB only
dotnet ef database update --project KasseAPI_Final.csproj --configuration Release --connection "<empty-db-connection>"
```

Smoke: app boot, login, one admin read, one POS health/payment smoke as appropriate.

### F. Application deploy order

1. Deploy API build that contains **only** the baseline migration (same commit as history rewrite).
2. Rewrite history (or run verified SQL job) **before** or **in lockstep** with the first process that runs EF migrate-on-startup (if any). Regkasse typically uses explicit `dotnet ef` / ops migrate — confirm `ApplicationHost` does not auto-migrate Production.
3. If migrate-on-startup exists anywhere, disable it for the cutover window.

### G. Rollback

| Failure | Action |
|---------|--------|
| Code bad, DB history not rewritten | Redeploy previous API; old Migrations folder still required |
| History rewritten, need old chain | Restore `"__EFMigrationsHistory"` from `"__EFMigrationsHistory_backup_pre_squash"` **and** redeploy previous API commit that still has the old migration files |
| Schema damage | Restore `pg_dump` — history-only ops should not alter schema; if someone ran baseline `Up` on a full DB, restore from dump |

Keep the pre-squash Migrations archive until at least one full release cycle after production cutover.

## Dry-run evidence (local, 2026-07-21)

Executed via worktree + `kasse_db_ef_verify` template → `kasse_db_squash_dry` (not Production).

| Step | Result |
|------|--------|
| Preflight (`has-pending-model-changes`, no Pending) | Pass on source DB |
| `CREATE DATABASE … TEMPLATE` copy | Pass |
| Generate `SquashedBaseline_*` in git worktree | Pass |
| History rewrite (221 rows → 1 baseline row) | Pass — `migrations list` shows single applied baseline |
| Empty DB `database update` from baseline | **Blocked** — see prerequisites below |
| Squash applied on `main` | **No** (intentional) |

### Blockers discovered during dry-run (fix before real squash)

1. **Invoice `InvoiceNumber` unique + GIN merge** — two `HasIndex(InvoiceNumber)` calls without distinct `HasDatabaseName` produced a **UNIQUE GIN** index; PostgreSQL error `access method "gin" does not support unique indexes`. Fixed in `AppDbContext` with separate btree unique vs `*_trgm` GIN names; ship that as a normal migration **before** squash (or include in baseline after fix).
2. **`pg_trgm`** — GIN trigram indexes require `CREATE EXTENSION IF NOT EXISTS pg_trgm;` on empty databases (ensure baseline `Up` or a pre-step includes it).
3. **Worktree restore** — dry-run script must `dotnet restore` in the worktree before `migrations add`.
4. **Duplicate timestamp migration ids** and orphan `CleanupOrphanedCarts.cs` — removed by squash; until then, avoid adding more hand-written collisions.

Re-run dry-run after the invoice index fix lands and model/migrations are in sync again.

### Script usage

```powershell
cd backend
.\scripts\migration-squash\Invoke-MigrationSquashPreflight.ps1
.\scripts\migration-squash\Invoke-MigrationSquashDryRun.ps1
# Optional: -KeepWorktree to inspect the generated baseline
```

**Never** point those scripts at Production.

## Checklist (copy into the change ticket)

- [ ] All envs on same tip migration
- [ ] `has-pending-model-changes` clean
- [ ] Backups taken and restore-tested
- [ ] Baseline generated on squash branch
- [ ] Dry-run DB: history rewrite OK
- [ ] Empty DB: `database update` OK
- [ ] Staging app smoke OK
- [ ] Production **copy** rehearsal OK
- [ ] Explicit Production approval
- [ ] Production history rewrite + deploy
- [ ] Archive retained; freeze lifted

## Related

- `DesignTimeDbContextFactory` — `backend/Data/DesignTimeDbContextFactory.cs`
- Config / secrets — `backend/CONFIGURATION.md`
- Tenant / schema contracts — `ai/02_DATABASE_CONTRACT.md`
