# Cross-repo scripts (`scripts/`)

Node, PowerShell, shell, and SQL helpers for local development, CI gates, fiscal checks, and ops.  
Prefer root `package.json` aliases when available. This folder is **not** an npm workspace publish target.

**Last reviewed:** 2026-07-21

## Root aliases

```bash
npm run verify:api-client   # node scripts/verify-api-client.mjs
npm run verify:openapi      # node scripts/validate-critical-openapi-paths.mjs
npm run seed:test-data      # node scripts/seed-test-data.mjs  (needs running API)
npm run install:git-hooks   # node scripts/install-git-hooks.mjs
npm run clean               # node scripts/clean-artifacts.mjs (also: make/just clean)
npm run dev                 # node scripts/dev-workspaces.mjs (parallel package servers)
```

Also: root [`Makefile`](../Makefile) / [`Justfile`](../Justfile) for `dev`, `build`, `test`, `lint`, `clean`, `docker-up`, …

---

## CI / contract gates (keep these green)

| Script | Purpose | Wired |
|--------|---------|--------|
| [`verify-api-client.mjs`](verify-api-client.mjs) | Orval FA client ↔ `backend/swagger.json`; calls critical-path check | Root npm, Husky, `api-client-alignment.yml`, `api-client-auto-generate.yml` |
| [`validate-critical-openapi-paths.mjs`](validate-critical-openapi-paths.mjs) | Critical Admin/POS/offline/billing paths + schemas in swagger | Root npm, CI `api-contract-tests.yml`, invoked by verify-api-client |
| [`validate-api-contract.mjs`](validate-api-contract.mjs) | Diff committed swagger vs baseline (`swagger-old.json`) for breaking changes | CI `api-contract.yml` |
| [`verify-api-contract.mjs`](verify-api-contract.mjs) | Lightweight swagger endpoint presence smoke | Optional; used by `run-all-tests.sh` |
| [`generate-backend-openapi.mjs`](generate-backend-openapi.mjs) | Regenerate `backend/swagger.json` (do **not** hand-edit swagger) | Docs / regen workflow |

```bash
node scripts/verify-api-client.mjs
node scripts/verify-api-client.mjs --openapi-only   # no Orval regenerate
node scripts/validate-critical-openapi-paths.mjs
node scripts/generate-backend-openapi.mjs
# then: cd frontend-admin && npm run generate:api
```

**Naming tip:** three OpenAPI helpers — *verify-api-client* (Orval+paths), *validate-critical-openapi-paths* (paths only), *validate-api-contract* (diff). Do not confuse with *verify-api-contract* (light smoke).

---

## Git hooks

| Script | Purpose |
|--------|---------|
| [`install-git-hooks.mjs`](install-git-hooks.mjs) | Installs Husky `.husky/pre-commit` |
| [`git-hooks/pre-commit.mjs`](git-hooks/pre-commit.mjs) | Runs `verify-api-client` (skip with `SKIP_API_CLIENT_VERIFY=1`) |
| [`git-hooks/pre-commit`](git-hooks/pre-commit) | Legacy shell alternate (installer prefers `.husky`) |

```bash
npm run install:git-hooks
# or: npm run prepare  (root)
```

---

## Dev runners

| Script | Purpose |
|--------|---------|
| [`dev-workspaces.mjs`](dev-workspaces.mjs) | Parallel `dev` for backend + POS + FA + sites |
| [`start-redis-dev.ps1`](start-redis-dev.ps1) | Local Redis helper |
| [`clean-backend-build.ps1`](clean-backend-build.ps1) | Clean backend build artifacts (manual) |
| [`dev-mail-test.bat`](dev-mail-test.bat) / [`dev-mail-config.ps1`](dev-mail-config.ps1) / [`test-forgot-username-email.ps1`](test-forgot-username-email.ps1) | Dev mail capture — see `docs/EMAIL_CONFIGURATION.md` |
| [`dev-mail.local.env.example`](dev-mail.local.env.example) | Template (local env is gitignored) |
| [`dev-purge-tenant-catalog.ps1`](dev-purge-tenant-catalog.ps1) (+ `.bat`) | **Development-only** catalog purge via API (manual; often gitignored locally) |
| [`beta-preflight.mjs`](beta-preflight.mjs) | Read-only beta env checks — `docs/beta-env-matrix.md` |

---

## Seeds (two mechanisms — do not conflate)

| Script | Mechanism | When |
|--------|-----------|------|
| [`seed-test-data.mjs`](seed-test-data.mjs) | Live HTTP API against running backend | `npm run seed:test-data` |
| [`seed-test-data.sql`](seed-test-data.sql) + [`reset-test-data.sh`](reset-test-data.sh) / `.sql` | Direct Postgres (+ Redis flush) | TestSprite / manual DB reset |
| [`seed-demo-tenant-admins.sql`](seed-demo-tenant-admins.sql) / [`seed-demo-cash-registers.sql`](seed-demo-cash-registers.sql) | Demo SQL referenced by backend tests / migrations | Ops / tests |
| [`seed-tenant-company-profiles.sql`](seed-tenant-company-profiles.sql) / [`seed-tenant-pos-cash-register-features.sql`](seed-tenant-pos-cash-register-features.sql) | Optional one-off seeds | Manual |
| [`backfill-user-tenant-memberships.sql`](backfill-user-tenant-memberships.sql) | Membership backfill | Backend tests + migration notes |

```bash
# HTTP seed (API must be up)
SEED_TENANT_SLUG=dev npm run seed:test-data
SEED_DRY_RUN=1 SEED_SKIP_SALES=1 npm run seed:test-data
node scripts/seed-test-data.mjs --help
```

---

## Offline smoke

| Script | Purpose |
|--------|---------|
| [`test-offline-system.mjs`](test-offline-system.mjs) / [`.sh`](test-offline-system.sh) | Structural offline smoke — see `docs/OFFLINE_SYSTEM_INDEX.md` |

---

## RKSV / DEP (Prüftool)

| Script | Purpose |
|--------|---------|
| [`verify-rksv-dep-export.ps1`](verify-rksv-dep-export.ps1) (+ `.bat`) | BMF DEP format verify (JDK + jar) |
| [`generate-dep-export-fixtures.ps1`](generate-dep-export-fixtures.ps1) (+ `.bat`) | Generate Prüftool fixtures |
| [`verify-rksv-receipt-qr.ps1`](verify-rksv-receipt-qr.ps1) | Receipt QR verify |
| [`run-verify-dep-export-complete.ps1`](run-verify-dep-export-complete.ps1) (+ `.bat`) | Combined DEP runner |
| [`run_fiscal_go_live_validation.sh`](run_fiscal_go_live_validation.sh) / [`.ps1`](run_fiscal_go_live_validation.ps1) | Fiscal SQL gate — CI `fiscal-validation.yml` |
| [`sql/fiscal_go_live_validation.sql`](sql/fiscal_go_live_validation.sql) | Queries for fiscal go-live |

Requires JDK 17+ for Prüftool scripts. See `docs/DEP_EXPORT_DEVELOPMENT.md`, `AGENTS.md`.

---

## Manual QA / smoke (not CI gates)

| Script | Purpose | Notes |
|--------|---------|--------|
| [`e2e-smoke-test.sh`](e2e-smoke-test.sh) | API health/login/RKSV smoke | Used by `run-all-tests.sh` |
| [`run-all-tests.sh`](run-all-tests.sh) | Aggregated backend + FA + contract (+ optional E2E) | Manual aggregator |
| [`smoke-tenant-isolation.ps1`](smoke-tenant-isolation.ps1) | Tenant isolation API smoke | Manual |
| [`run-comprehensive-smoke.ps1`](run-comprehensive-smoke.ps1) | Broad API smoke | Manual |
| [`fa-full-menu-e2e.mjs`](fa-full-menu-e2e.mjs) / [`fa-sections-smoke-e2e.mjs`](fa-sections-smoke-e2e.mjs) | Playwright FA menu/section E2E | Needs FA+API; Playwright via local install or `npx` |
| [`run-tests.sh`](run-tests.sh) | TestSprite runner + reset | Needs `.env.test` |
| [`run-testsprite-pos-smoke.ps1`](run-testsprite-pos-smoke.ps1) | POS TestSprite via `npx @testsprite/testsprite-mcp` | Manual |

---

## Ops / rollback

| Script | Purpose |
|--------|---------|
| [`prepare-rollback-backup.sh`](prepare-rollback-backup.sh) | Pre-deploy backup prep |
| [`rollback-production.sh`](rollback-production.sh) | Production rollback helper |
| [`document-rollback.sh`](document-rollback.sh) | Rollback documentation helper |

**Do not** use these to roll back EF migrations casually. Prefer forward-fix migrations. See `docs/` backup/restore hubs for data restore policy (no production restore via API).

---

## One-off SQL (`sql/` and root `*.sql`)

| Path | Purpose |
|------|---------|
| [`sql/offline_payload_hash_legacy.sql`](sql/offline_payload_hash_legacy.sql) | Legacy hash measurement (release docs) |
| [`sql/deduplicate_open_suspicious_transaction_alerts.sql`](sql/deduplicate_open_suspicious_transaction_alerts.sql) | One-time alert cleanup |
| [`sql/fix_aspnetusers_tax_number_nullable.sql`](sql/fix_aspnetusers_tax_number_nullable.sql) | Manual schema fix (prefer EF migrations) |
| [`sql/fix_user_tenant_memberships_user_id_unique.sql`](sql/fix_user_tenant_memberships_user_id_unique.sql) | Manual unique fix |
| [`db-validate-table-orders.sql`](db-validate-table-orders.sql) | Table-orders recovery DB check |

Run only after review. Prefer checked-in EF migrations for schema changes.

> Note: some SQL paths may be gitignored in local clones (see root `.gitignore`); CI/tests that need them expect them present in the repo checkout you use.

---

## Removed / do not revive

| Former script | Why removed (2026-07) |
|---------------|------------------------|
| `migrate_i18n.js` | One-shot POS locale split — already applied |
| `patch-swagger-backup-dr.cjs` | Hand-patch swagger — use `generate-backend-openapi.mjs` |
| `parse-demo-menu-html.*` | One-off HTML → demo-products importer |
| `ci-smoke-test.sh` | Orphan; hit legacy `/api/Cart/...` (do not extend Cart) |

---

## Conventions

- Prefer English log messages.
- Do not commit secrets (`dev-mail.local.env` is gitignored).
- Destructive DB scripts must be Development-only and clearly named.
- Prefer idempotent scripts where possible.
- Do not extend legacy `/api/Payment`, `/api/Cart`, `/api/Product` for new features.

## Related

- CI inventory: [`.github/workflows/README.md`](../.github/workflows/README.md)
- Agent rules: [`../AGENTS.md`](../AGENTS.md)
- Docs index: [`../docs/README.md`](../docs/README.md)
- Localization scripts: [`../localization/scripts/`](../localization/) (separate from this folder)

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
