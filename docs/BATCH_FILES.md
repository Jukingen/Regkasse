# Batch files (Windows)

Double-click helpers for common Regkasse tasks on Windows. Prefer these when you do not want to open a PowerShell session. Source of truth for behavior remains the underlying `npm` / `powershell` / `docker` commands.

**Full reference** (purpose, prerequisites, examples, troubleshooting for every `.bat` / `.ps1`): [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md).

**Do not gitignore `.bat` files** — they are shared team tooling.

Regenerate missing wrappers for `.ps1` scripts:

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\create-bat-wrappers.ps1
```

Use `-Force` only when you intentionally want to overwrite auto-generated wrappers (hand-crafted DEP / purge bats are usually better left alone).

---

## Root shortcuts

| File | Runs | When to use |
|------|------|-------------|
| [`start-dev.bat`](../start-dev.bat) | `npm run dev` | Daily local stack (API + Admin + POS + Sites) |
| [`start-backend.bat`](../start-backend.bat) | `npm run dev:backend` | API only |
| [`start-admin.bat`](../start-admin.bat) | `npm run dev:admin` | Admin (FA) only |
| [`start-pos.bat`](../start-pos.bat) | `npm run dev:pos` | POS (Expo) only |
| [`start-sites.bat`](../start-sites.bat) | `npm run dev:sites` | Tenant Sites only |
| [`test-all.bat`](../test-all.bat) | Backend → Admin → POS tests (sequential) | All package tests before commit |
| [`clean-all.bat`](../clean-all.bat) | Confirm + remove `bin`/`obj`/`.next`/`.expo`/… | Remove shared build artifacts |
| [`docker-up.bat`](../docker-up.bat) | `docker compose up -d` | Start Compose stack detached |
| [`docker-down.bat`](../docker-down.bat) | `docker compose down` | Stop Compose stack |
| [`docker-status.bat`](../docker-status.bat) | Formatted `docker ps` | List running containers |
| [`docker-clean.bat`](../docker-clean.bat) | `compose down -v` + `system prune` | **Destructive** local Docker cleanup (confirms first) |
| [`deploy.bat`](../deploy.bat) | Confirm → smoke → backup confirm → `docker-compose.prod.yml` | Production-style Compose deploy |
| [`rollback.bat`](../rollback.bat) | Confirm → `git reset --hard HEAD~1` + prod Compose rebuild | **Destructive** last-commit undo + redeploy |

### Typical day

1. `start-dev.bat` (or `docker-up.bat` + host apps)
2. Change code
3. `test-all.bat` or package-specific tests
4. `docker-down.bat` when finished with containers

### Cleanup

1. `clean-all.bat`
2. Optional: `scripts\clean-backend.bat` if backend `bin`/`obj` is corrupted
3. Optional: `docker-clean.bat` (wipes volumes)

---

## `scripts/` helpers

### Convenience aliases

| File | Target | Notes |
|------|--------|-------|
| [`clean-backend.bat`](../scripts/clean-backend.bat) | `clean-backend-build.ps1` | Stop API process + remove backend build dirs |
| [`dev-purge-tenant.bat`](../scripts/dev-purge-tenant.bat) | confirm → `dev-purge-tenant-catalog.ps1` | **Development only** — hard-deletes catalog |
| [`generate-dep-export.bat`](../scripts/generate-dep-export.bat) | `generate-dep-export-fixtures.ps1` | Regenerates Prüftool fixtures |
| [`ensure-bmf-prueftool.bat`](../scripts/ensure-bmf-prueftool.bat) | `ensure-bmf-prueftool.ps1` | Downloads BMF JARs into `backend/Tests/` |
| [`fix-antd.bat`](../scripts/fix-antd.bat) | `node fix-antd-deprecations.mjs` | Ant Design 6 deprecation fixer |
| [`dev-mail.bat`](../scripts/dev-mail.bat) | ensures `dev-mail.local.env` + `dev-mail-test.bat` | Local forgot-username mail capture |
| [`smoke-test.bat`](../scripts/smoke-test.bat) | curl API/Admin/POS | Lightweight smoke (full suite: `run-comprehensive-smoke.bat`) |

### Shared utilities

| File | Purpose |
|------|---------|
| [`_common.bat`](../scripts/_common.bat) | `call _common.bat check_error\|success\|fail\|info\|warn` helpers |
| [`run-with-log.bat`](../scripts/run-with-log.bat) | Run any command; append output under `logs/` (gitignored) |
| [`create-bat-wrappers.ps1`](../scripts/create-bat-wrappers.ps1) (+ `.bat`) | Auto-create sibling `.bat` for each `.ps1` |

### Auto-generated / existing `.ps1` wrappers

Sibling `.bat` next to each PowerShell script under `scripts/` (skipped if a hand-crafted `.bat` already exists), including:

| Wrapper | Script |
|---------|--------|
| `clean-backend-build.bat` | `clean-backend-build.ps1` |
| `run-comprehensive-smoke.bat` | `run-comprehensive-smoke.ps1` |
| `smoke-tenant-isolation.bat` | `smoke-tenant-isolation.ps1` |
| `start-redis-dev.bat` | `start-redis-dev.ps1` |
| `verify-rksv-receipt-qr.bat` | `verify-rksv-receipt-qr.ps1` |
| `run_fiscal_go_live_validation.bat` | `run_fiscal_go_live_validation.ps1` |
| `run-testsprite-pos-smoke.bat` | `run-testsprite-pos-smoke.ps1` |
| `test-forgot-username-email.bat` | `test-forgot-username-email.ps1` |

Hand-crafted (richer UX / logging — do not overwrite lightly):

| Wrapper | Notes |
|---------|-------|
| `verify-rksv-dep-export.bat` | BMF DEP Prüftool; fixtures or explicit paths |
| `generate-dep-export-fixtures.bat` | Fixture regeneration |
| `run-verify-dep-export-complete.bat` | Full DEP validation pipeline |
| `dev-purge-tenant-catalog.bat` | Catalog purge with temp log |
| `dev-mail-test.bat` | Interactive email prompt |

---

## Safety notes

- **`rollback.bat`** rewrites git history of the current branch tip (`reset --hard`). Prefer `git revert` on shared branches.
- **`docker-clean.bat`** removes Compose volumes (DB data) and runs `docker system prune -f`.
- **`dev-purge-tenant*.bat`** is Development-only and destroys catalog data.
- Fiscal / DEP bats need JDK 17+ and Prüftool JARs (`ensure-bmf-prueftool.bat`).

---

## Related docs

- [`DEVELOPMENT.md`](../DEVELOPMENT.md) — local setup
- [`docs/DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) — Docker Desktop on Windows
- [`docs/DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) — DEP / Prüftool
- [`scripts/README.md`](../scripts/README.md) — full script inventory
