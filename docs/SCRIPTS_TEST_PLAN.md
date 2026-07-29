# Scripts test plan (Windows `.bat` / `.ps1`)

> **Last updated:** 2026-07-29  
> **Automated dry-run:** [`scripts/test-scripts.ps1`](../scripts/test-scripts.ps1) · [`scripts/test-scripts.bat`](../scripts/test-scripts.bat)  
> **Pairing CI:** `npm run verify:bat-ps1`  
> **Full catalog:** [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)

This plan separates **safe automated checks** (existence, structure, targets) from **manual interactive runs** (servers, Docker, smoke).

---

## 1. Automated dry-run (every PR / local)

```batch
scripts\validate-scripts.bat
npm run validate:scripts
```

Or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-scripts.ps1
```

What it checks:

| Check | Scope |
|-------|--------|
| Pairing | `node scripts/verify-bat-ps1-pairing.mjs` (allowlists) |
| Root `.bat` docs | Each root `*.bat` named in `docs/SCRIPTS_REFERENCE.md` |
| `scripts/*.ps1` docs | Each PowerShell script named in `SCRIPTS_REFERENCE.md` |
| Alias `.bat` docs | `clean-backend.bat`, `smoke-test.bat`, … |
| Test plan present | `docs/SCRIPTS_TEST_PLAN.md` |

CI: [`.github/workflows/scripts-bat-ps1-pairing.yml`](../.github/workflows/scripts-bat-ps1-pairing.yml).

---

## 1b. Structural dry-run

```batch
scripts\test-scripts.bat
```

Or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-scripts.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-scripts.ps1 -Strict
```

What it checks:

| Check | Scope |
|-------|--------|
| File exists | All root convenience + listed `scripts\` wrappers |
| Structure | `@echo off`, `ERRORLEVEL` / pause where expected |
| Target wired | e.g. `npm run …`, `docker compose …`, `.ps1` / `.mjs` paths |
| Target present | Matching `.ps1` / `.mjs` / alias target on disk |
| Pairing | Invokes `node scripts/verify-bat-ps1-pairing.mjs` |

What it **does not** do: start `npm run dev`, Compose up, deploy, or interactive purge/mail.

---

## 2. Manual checklist — Development

| # | Action | Expected |
|---|--------|----------|
| 1 | Double-click `start-backend.bat` | API on :5184; Ctrl+C → `[OK]`/`[FAILED]` + pause |
| 2 | `start-admin.bat` (API already up) | FA on :3000 |
| 3 | `start-pos.bat` | Expo / Metro |
| 4 | `start-dev.bat` | All four surfaces |
| 5 | `test-all.bat` | Workspace tests; exit code matches failures |
| 6 | `clean-all.bat` | Removes artifacts; no crash if nothing to delete |

---

## 3. Manual checklist — Docker

| # | Action | Expected |
|---|--------|----------|
| 1 | Docker Desktop running | `docker info` OK |
| 2 | `docker-up.bat` | Containers start; URLs printed |
| 3 | `docker-status.bat` | Lists `regkasse-*` containers |
| 4 | `docker-down.bat` | Containers stopped |
| 5 | `docker-clean.bat` → `n` | Cancelled, no wipe |
| 6 | `docker-clean.bat` → `y` | Volumes removed (**data loss**) |

---

## 4. Manual checklist — Maintenance / helpers

| # | Action | Expected |
|---|--------|----------|
| 1 | `scripts\clean-backend.bat` | Removes `backend\bin`/`obj` (or no-op) |
| 2 | `scripts\ensure-bmf-prueftool.bat` | JARs under `backend\Tests\` |
| 3 | `scripts\generate-dep-export.bat` | Fixtures under `fixtures\prueftool\` |
| 4 | `scripts\fix-antd.bat --dry-run` | JSON report, no write |
| 5 | `scripts\dev-mail.bat` | Env created/used; interactive email prompt |
| 6 | `scripts\dev-purge-tenant.bat …` | **Development only**; catalog purge |
| 7 | `scripts\smoke-test.bat` | With stack up; PASS/FAIL table |
| 8 | `scripts\run-with-log.bat echo hello` | Log under `logs\run_*.log` |

---

## 5. Manual checklist — Deployment

| # | Action | Expected |
|---|--------|----------|
| 1 | `deploy.bat` | confirm → smoke → backup gate → prod compose build/up → health |
| 2 | Fail Docker deliberately | Clear `[ERROR] Docker is not running!` |
| 3 | `rollback.bat` → `n` | Cancelled |
| 4 | `rollback.bat` → `y` | **Only on disposable branch** — hard reset + rebuild |

---

## 6. CI / pairing

```bash
npm run verify:bat-ps1
npm run validate:scripts
```

Workflow: [`.github/workflows/scripts-bat-ps1-pairing.yml`](../.github/workflows/scripts-bat-ps1-pairing.yml).

After adding a new `.ps1`:

```batch
scripts\create-bat-wrappers.bat
npm run validate:scripts
scripts\test-scripts.bat
```

Document the new script in [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) (PowerShell catalog + section if user-facing) or CI will fail.
---

## 7. Pass criteria

- Automated: `validate-scripts.ps1` exit **0** (`npm run validate:scripts`)
- Automated: `test-scripts.ps1` exit **0**; `verify:bat-ps1` exit **0**
- Manual smoke: `scripts\smoke-test.bat` exit **0** with API/FA (and POS if checked) up
- No silent flash-close: every user-facing `.bat` pauses on failure
- New scripts must be listed in [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) or documentation validation fails in CI