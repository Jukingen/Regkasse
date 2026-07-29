# Cross-repo scripts (`scripts/`)

Node, PowerShell, shell, and SQL helpers for local development, CI gates, fiscal checks, and ops.  
Prefer root `package.json` aliases when available. This folder is **not** an npm workspace publish target.

**Last reviewed:** 2026-07-29

| Doc | Use when |
|-----|----------|
| [`docs/GETTING_STARTED_SCRIPTS.md`](../docs/GETTING_STARTED_SCRIPTS.md) | 5-minute Windows scripts onboarding |
| [`docs/DOCKER_VS_LEGACY.md`](../docs/DOCKER_VS_LEGACY.md) | Legacy host vs Docker Compose mode |
| [`docs/SCRIPTS_REFERENCE.md`](../docs/SCRIPTS_REFERENCE.md) | Full Windows `.bat` / `.ps1` catalog + flowchart |
| [`docs/SCRIPTS_ECOSYSTEM.md`](../docs/SCRIPTS_ECOSYSTEM.md) | Visual “which script?” map |
| [`docs/SCRIPTS_QUICK_REF.md`](../docs/SCRIPTS_QUICK_REF.md) | One-screen icon card |
| [`docs/BATCH_FILES.md`](../docs/BATCH_FILES.md) | Short `.bat` inventory |
| Root [`start.bat`](../start.bat) | Mode chooser (Legacy / Docker) |
| [`scripts/legacy/`](legacy/) · [`scripts/docker/`](docker/) | Mode-specific Windows helpers |

### Quick start (Windows)

```batch
REM From repo root
start-dev.bat
scripts\smoke-test.bat
test-all.bat
npm run validate:scripts
```

---

## Windows `.bat` / `.ps1` in this folder

Most PowerShell scripts have a sibling `.bat` for double-click / `cmd` use. Aliases (different basename) are intentional.

| Script / wrapper | Purpose |
|------------------|---------|
| [`create-bat-wrappers.ps1`](create-bat-wrappers.ps1) (+ `.bat`) | Generate missing sibling `.bat` for each `.ps1` |
| [`test-scripts.ps1`](test-scripts.ps1) (+ `.bat`) | Dry-run structural test plan for Windows bats — [`docs/SCRIPTS_TEST_PLAN.md`](../docs/SCRIPTS_TEST_PLAN.md) |
| [`validate-scripts.ps1`](validate-scripts.ps1) (+ `.bat`) | Pairing + `SCRIPTS_REFERENCE.md` coverage gate (`npm run validate:scripts`) |
| [`verify-bat-ps1-pairing.mjs`](verify-bat-ps1-pairing.mjs) | CI gate: pairing + allowlists (`npm run verify:bat-ps1`) |
| [`_common.bat`](_common.bat) | Shared `call` helpers (not double-click) |
| [`run-with-log.bat`](run-with-log.bat) | Run any command → `logs\run_*.log` |
| [`smoke-test.bat`](smoke-test.bat) | Lightweight curl API/Admin/POS (full: `run-comprehensive-smoke.bat`) |
| [`clean-backend.bat`](clean-backend.bat) | → `clean-backend-build.ps1` |
| [`generate-dep-export.bat`](generate-dep-export.bat) | → `generate-dep-export-fixtures.ps1` |
| [`dev-purge-tenant.bat`](dev-purge-tenant.bat) | → `dev-purge-tenant-catalog.ps1` |
| [`fix-antd.bat`](fix-antd.bat) | → `fix-antd-deprecations.mjs` (Node) |
| [`dev-mail.bat`](dev-mail.bat) | Env setup + `dev-mail-test.bat` |
| [`ensure-bmf-prueftool.ps1`](ensure-bmf-prueftool.ps1) (+ `.bat`) | Download BMF Prüftool JARs |
| [`docker-up.ps1`](docker-up.ps1) / [`docker-down.ps1`](docker-down.ps1) / [`docker-build.ps1`](docker-build.ps1) / [`docker-deploy.ps1`](docker-deploy.ps1) / [`docker-diagnose.ps1`](docker-diagnose.ps1) | Compose helpers (+ `.bat`) |
| [`verify-rksv-dep-export.ps1`](verify-rksv-dep-export.ps1) (+ `.bat`) | BMF DEP verify |
| [`start-redis-dev.ps1`](start-redis-dev.ps1) (+ `.bat`) | Portable Redis |
| [`dev-mail-config.ps1`](dev-mail-config.ps1) | **Library only** — no `.bat` (allowlisted) |

Root convenience bats (`start-dev.bat`, `docker-up.bat`, `deploy.bat`, …) live in the **repo root**, not here — see SCRIPTS_REFERENCE.

### How to create a new script

1. Add `scripts/my-task.ps1` (or `.mjs` for Node).
2. For PowerShell: run `scripts\create-bat-wrappers.bat` to create `my-task.bat` (or write a hand-crafted wrapper like DEP bats).
3. Document in [`docs/SCRIPTS_REFERENCE.md`](../docs/SCRIPTS_REFERENCE.md) if it is user-facing.
4. If the `.bat` basename must differ from the `.ps1` (alias), add it to `PS1_OPTIONAL_BAT` in [`verify-bat-ps1-pairing.mjs`](verify-bat-ps1-pairing.mjs).
5. If a `.ps1` is a library (dot-sourced only), add it to `BAT_OPTIONAL_PS1`.
6. Local check: `npm run verify:bat-ps1` (CI: `.github/workflows/scripts-bat-ps1-pairing.yml`).

```batch
REM From repo root
scripts\create-bat-wrappers.bat
npm run verify:bat-ps1
```

---

## Root aliases

```bash
npm run verify:api-client   # node scripts/verify-api-client.mjs
npm run verify:openapi      # node scripts/validate-critical-openapi-paths.mjs
npm run verify:bat-ps1      # node scripts/verify-bat-ps1-pairing.mjs
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
| [`verify-bat-ps1-pairing.mjs`](verify-bat-ps1-pairing.mjs) | `.bat` / `.ps1` pairing allowlists | `scripts-bat-ps1-pairing.yml`, `npm run verify:bat-ps1` |
| [`generate-backend-openapi.mjs`](generate-backend-openapi.mjs) | Regenerate `backend/swagger.json` (do **not** hand-edit swagger) | Docs / regen workflow |

```bash
node scripts/verify-api-client.mjs
node scripts/verify-api-client.mjs --openapi-only   # no Orval regenerate
node scripts/validate-critical-openapi-paths.mjs
node scripts/verify-bat-ps1-pairing.mjs
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
| [`docker-diagnose.ps1`](docker-diagnose.ps1) | Windows Docker/WSL/Compose/ports diagnose — [`docs/DOCKER_WINDOWS_TROUBLESHOOTING.md`](../docs/DOCKER_WINDOWS_TROUBLESHOOTING.md) ([DE](../docs/DOCKER_WINDOWS_TROUBLESHOOTING.de.md)) |
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
| [`ensure-bmf-prueftool.ps1`](ensure-bmf-prueftool.ps1) | Download official BMF Prüftool V1.1.1 ZIP into `backend/Tests/` |
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
| [`smoke-test.sh`](smoke-test.sh) / [`smoke-test.ps1`](smoke-test.ps1) | **Deploy smoke** (health, FA, POS, DEP) | CI + ops; [`docs/DEPLOYMENT_SMOKE_TEST.md`](../docs/DEPLOYMENT_SMOKE_TEST.md) |
| [`rollback-production.sh`](rollback-production.sh) | Files or Docker image rollback + post-smoke + on-call | `MODE=docker` / `files` |

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
- Keep `.bat` / `.ps1` pairing green (`npm run verify:bat-ps1`).

## Related

- CI inventory: [`.github/workflows/README.md`](../.github/workflows/README.md)
- Agent rules: [`../AGENTS.md`](../AGENTS.md)
- Docs index: [`../docs/README.md`](../docs/README.md)
- Localization scripts: [`../localization/scripts/`](../localization/) (separate from this folder)

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
