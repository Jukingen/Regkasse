# Database migration strategy

**Status:** Binding for all schema changes in Regkasse (EF Core 10 + PostgreSQL).  
**Related:** [`ai/02_DATABASE_CONTRACT.md`](../ai/02_DATABASE_CONTRACT.md) · [`backend/docs/MIGRATION_SQUASH.md`](../backend/docs/MIGRATION_SQUASH.md) · [`DEPLOYMENT.md`](../DEPLOYMENT.md) · AGENTS.md § Database Baseline Rules

**Last updated:** 2026-07-29

---

## Principle

**All migrations must be additive and backward-compatible** with the previously deployed API binary.

| Do | Do not |
|----|--------|
| Add nullable columns, new tables, new indexes | Drop columns/tables in the same release that removes code usage |
| Add FKs with care (nullable first, backfill, then tighten) | Rename columns in place without a dual-write period |
| Keep old API code reading/writing both old and new shapes during transition | Edit committed migration files after they shipped |
| Prefer expand → migrate data → contract | Use `IgnoreQueryFilters()` casually in data scripts |

Cross-tenant and fiscal rules still apply (`ai/02_DATABASE_CONTRACT.md`, `ai/07_DO_NOT_TOUCH.md`).

---

## Migration types

### 1. Schema migration (expand)

- Add tables / columns / indexes only.
- New columns: **nullable** or with a safe **server default** so old app versions keep working.
- No irreversible deletes.
- Ship with (or before) code that *optionally* uses the new shape.

```bash
cd backend
dotnet ef migrations add AddSomethingDescriptive \
  --project KasseAPI_Final.csproj \
  --startup-project KasseAPI_Final.csproj
```

Review the generated `Up`/`Down`. Prefer hand-written additive SQL when EF would drop/recreate.

### 2. Data migration (backfill)

- Separate step from schema introduce when possible (second migration or hosted/one-shot job).
- Idempotent: safe to re-run.
- Batch large tables; avoid long locks on hot fiscal tables.
- Can run **after** the new binary is live if the app tolerates null/empty until backfill finishes.

### 3. Schema removal (contract) — delayed

- Only after a **deprecation period of at least two releases** (code no longer reads/writes the old shape).
- Announce in release notes; confirm Staging + Canary for a full cycle.
- Prefer soft-delete / `is_deleted` / ignore unused columns over hard drops when unsure.
- Never combine “stop writing column X” and “DROP COLUMN X” in the same release.

---

## Creating safe migrations

1. Read AGENTS.md § Do NOT / Database Baseline Rules and `ai/02_DATABASE_CONTRACT.md`.
2. Sketch expand → backfill → contract; implement **expand** only in the first PR.
3. `dotnet ef migrations add …` then inspect SQL (especially fiscal / Identity / `tenant_id`).
4. Add or extend unit/integration tests that assume the new schema.
5. Document rollback posture in the PR (usually: **roll back the app, leave additive schema**).

### Naming

Use clear, English names: `AddDepExportAuditEntries`, `AddTenantSettingsFeatureFlags` — not `FixStuff2`.

### Multi-tenant

- New tenant-scoped tables: non-null `tenant_id`, index, EF global filter via `ITenantEntity`.
- Cross-tenant access remains HTTP **404**.

---

## Testing on Staging first

| Step | Action |
|------|--------|
| 1 | Merge to `main` → Backend CI deploys **Staging** and runs migrations automatically |
| 2 | Confirm `GET /health/migrations` → `pendingCount: 0`, status Healthy |
| 3 | Smoke + FA `/admin/database/migrations` shows new migration as applied |
| 4 | Promote `release/*` → **Canary** (migrations auto; limited tenants) |
| 5 | Tag `v*` / manual production → **migrate job with Environment approval**, then deploy |

Never apply an untested migration SQL directly on Production outside this pipeline.

Local dry-run against a **copy** of Staging (not Production):

```bash
dotnet ef database update \
  --project backend/KasseAPI_Final.csproj \
  --startup-project backend/KasseAPI_Final.csproj \
  --connection "<staging-copy-connection>"
```

Empty-DB / baseline notes: [`backend/docs/MIGRATION_SQUASH.md`](../backend/docs/MIGRATION_SQUASH.md).

---

## CI/CD pipeline (migrations)

| Stage | When | Migrations | Approval |
|-------|------|------------|----------|
| **Staging** | Push `main` / auto promote | Automatic before app deploy webhook | Optional Environment `backend-staging` |
| **Canary** | `release/*` or manual canary | Automatic before deploy; smoke includes `/health/migrations` | Environment `backend-canary` |
| **Production** | Tag `v*` / manual production | Dedicated migrate job | **Required** Environment `backend-production-migrations` reviewers, then app deploy |

Reusable workflow: [`.github/workflows/deploy-backend-stage.yml`](../.github/workflows/deploy-backend-stage.yml)  
Script: [`scripts/ci/backend-run-migrations.sh`](../scripts/ci/backend-run-migrations.sh)

### Secrets / vars

| Name | Purpose |
|------|---------|
| `BACKEND_*_MIGRATE_WEBHOOK_URL` | Preferred: host runs `dotnet ef database update` / migrate job |
| `BACKEND_*_DATABASE_CONNECTION` | Fallback: CI runner runs EF update (use carefully; prefer webhook) |
| GitHub Environments | `backend-staging`, `backend-canary`, `backend-production-migrations`, `backend-production` |

Payload for migrate webhook (illustrative):

```json
{ "action": "migrate", "stage": "staging", "sha": "…", "image": "…" }
```

### Canary rollback plan (migrations)

Additive schema is **not** rolled back on smoke failure:

1. App rollback webhook restores previous image.
2. New columns/tables remain (harmless to old binary if expand rules were followed).
3. Do **not** run `dotnet ef migrations remove` / manual `Down()` on Production.
4. Fix forward with a follow-up additive migration if needed.

---

## Rollback (schema) — last resort only

EF `Down()` / `database update <PreviousMigration>` on Production is **high risk** and usually forbidden when data was written into new columns.

Preferred order:

1. **Roll back application** to previous image (CI rollback webhook / [`scripts/rollback-production.sh`](../scripts/rollback-production.sh)).
2. Keep additive schema; ship a fix-forward migration.
3. Only if a migration destroyed data or violated RKSV integrity: restore from **backup** ([`docs/BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)), never invent a partial schema rewind without DBA review.

If an absolute schema rollback is required ( Staging/dev only):

```bash
# Identify previous migration id from /health/migrations or:
dotnet ef migrations list --project backend/KasseAPI_Final.csproj

# Target that migration (destructive — Staging only unless approved incident)
dotnet ef database update <PreviousMigrationId> \
  --project backend/KasseAPI_Final.csproj \
  --startup-project backend/KasseAPI_Final.csproj \
  --connection "<connection>"
```

Document the incident, correlation IDs, and backup stamp used.

---

## Observability

| Surface | Auth | Purpose |
|---------|------|---------|
| `GET /health/migrations` | Anonymous | Pending/applied counts for orchestrators |
| `GET /api/health/migrations` | Anonymous | Same JSON via HealthController |
| `GET /api/admin/database/migrations` | Super Admin (`system.critical`) | Full list for FA |
| FA `/admin/database/migrations` | Super Admin | UI dashboard |

Healthy = zero pending migrations relative to the **running** binary’s EF model.  
Degraded = pending migrations (deploy/migrate lag).  
Unhealthy = cannot query `__EFMigrationsHistory` / DB.

Startup still applies pending migrations in Development-style bootstrap where configured; Production should prefer **CI migrate-before-traffic** so cold start does not surprise-apply schema. See `StartupBootstrapRunner`.

---

## Checklist (PR)

- [ ] Additive only (or documented two-release removal)
- [ ] Old API binary remains compatible
- [ ] Tenant / fiscal / Identity impact reviewed
- [ ] Staging plan clear; Canary tenants identified if needed
- [ ] Rollback = app rollback + keep schema (stated in PR)
- [ ] After merge: verify `/health/migrations` on Staging
