# Batch files (Windows)

Double-click helpers for common Regkasse tasks on Windows. Prefer these when you do not want to open a PowerShell session. Source of truth for behavior remains the underlying `npm` / `powershell` / `docker` commands.

**Full reference** (purpose, prerequisites, examples, troubleshooting for every `.bat` / `.ps1`): [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md).

**Do not gitignore `.bat` files** — they are shared team tooling.

All Windows entry points live under `scripts/<category>/`. **Repo root has no `.bat` files.**

Regenerate missing wrappers for `.ps1` scripts:

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\lib\create-bat-wrappers.ps1
```

Use `-Force` only when you intentionally want to overwrite auto-generated wrappers (hand-crafted DEP / purge bats are usually better left alone).

---

## Category map

| Folder | Use for |
|--------|---------|
| [`scripts/dev/`](../scripts/dev/) | Mode chooser, npm start, clean, redis, mail |
| [`scripts/docker/`](../scripts/docker/) | PowerShell Compose (+ [`host/`](../scripts/docker/host/) chooser bats) |
| [`scripts/legacy/`](../scripts/legacy/) | Multi-window host starters |
| [`scripts/ci/`](../scripts/ci/) | CI build/test/deploy helpers |
| [`scripts/rksv/`](../scripts/rksv/) | DEP / BMF / fiscal |
| [`scripts/test/`](../scripts/test/) | Smoke + script self-tests |
| [`scripts/ops/`](../scripts/ops/) | Deploy / rollback / monitoring |
| [`scripts/lib/`](../scripts/lib/) | Shared bat helpers + validate/wrappers |

---

## Daily entry points

| File | Runs | When to use |
|------|------|-------------|
| [`scripts/dev/start.bat`](../scripts/dev/start.bat) | Chooser → Legacy or Docker | Preferred entry: pick mode |
| [`scripts/dev/start-dev.bat`](../scripts/dev/start-dev.bat) | `npm run dev` | Daily local stack in one terminal |
| [`scripts/dev/start-backend.bat`](../scripts/dev/start-backend.bat) | `npm run dev:backend` | API only |
| [`scripts/dev/start-admin.bat`](../scripts/dev/start-admin.bat) | `npm run dev:admin` | Admin (FA) only |
| [`scripts/dev/start-pos.bat`](../scripts/dev/start-pos.bat) | `npm run dev:pos` | POS only |
| [`scripts/dev/start-sites.bat`](../scripts/dev/start-sites.bat) | `npm run dev:sites` | Sites only |
| [`scripts/test/test-all.bat`](../scripts/test/test-all.bat) | Backend → Admin → POS tests | Before commit |
| [`scripts/dev/clean-all.DANGER.bat`](../scripts/dev/clean-all.DANGER.bat) | Confirm + wipe build artifacts | Cleanup |
| [`scripts/docker/host/up.bat`](../scripts/docker/host/up.bat) | Compose up (POS + Sites) | Docker chooser stack |
| [`scripts/docker/host/down.bat`](../scripts/docker/host/down.bat) | Compose down | Stop containers |
| [`scripts/docker/host/status.bat`](../scripts/docker/host/status.bat) | Status | “Is the stack up?” |
| [`scripts/docker/host/logs.bat`](../scripts/docker/host/logs.bat) | Follow logs | Debug |
| [`scripts/docker/host/clean.DANGER.bat`](../scripts/docker/host/clean.DANGER.bat) | Destructive cleanup | Wipe volumes (confirms) |
| [`scripts/ops/deploy.DANGER.bat`](../scripts/ops/deploy.DANGER.bat) | Smoke + backup gate + prod compose | Host prod-style deploy |
| [`scripts/ops/rollback.DANGER.bat`](../scripts/ops/rollback.DANGER.bat) | `git reset --hard HEAD~1` + rebuild | **Destructive** |

**Legacy / Docker comparison:** [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md). Logs for Legacy + `docker/host`: `C:\Scripts\logs`.

### Typical day

1. `scripts\dev\start.bat` → Legacy or Docker (or `scripts\dev\start-dev.bat` for npm)
2. Change code
3. `scripts\test\test-all.bat`
4. `scripts\docker\host\down.bat` when finished with containers

---

## Helpers by category

### `scripts/dev/`

| File | Notes |
|------|--------|
| `clean-backend.bat` | → `clean-backend-build.ps1` |
| `dev-purge-tenant.bat` | Development catalog purge |
| `fix-antd.bat` | → `scripts/fix-antd-deprecations.mjs` |
| `dev-mail.bat` | Local forgot-username mail capture |
| `start-redis-dev.bat` | Portable Redis |

### `scripts/rksv/`

| File | Notes |
|------|--------|
| `generate-dep-export.bat` | Prüftool fixtures |
| `ensure-bmf-prueftool.bat` | Download BMF JARs |
| `verify-rksv-dep-export.bat` | BMF DEP verify |

### `scripts/test/`

| File | Notes |
|------|--------|
| `smoke-test.bat` | Lightweight curl smoke |
| `run-comprehensive-smoke.bat` | Full suite |
| `test-scripts.bat` | Structural dry-run (`npm run test:scripts`) |

### `scripts/lib/`

| File | Purpose |
|------|---------|
| `_common.bat` | Shared `check_error` / `success` helpers |
| `run-with-log.bat` | Run any command → `logs/` |
| `create-bat-wrappers.ps1` | Auto sibling `.bat` for each `.ps1` |
| `validate-scripts.ps1` | Pairing + docs gate (`npm run validate:scripts`) |

### `scripts/docker/` (PowerShell)

| File | Purpose |
|------|---------|
| `docker-up.ps1` / `docker-down.ps1` / `docker-build.ps1` | Compose with `-Dev` / `-Prod` |
| `docker-deploy.ps1` / `docker-*-prod.ps1` | Prod build / push / deploy / logs |
| `docker-diagnose.ps1` | Engine / compose diagnostics |

Pairing CI: `npm run verify:bat-ps1` · `npm run validate:scripts`.
