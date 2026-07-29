# Scripts Test Checklist

Manual verification for Regkasse Windows `.bat` helpers.  
Automated gates: `npm run validate:scripts` · `npm run test:scripts`  
Related: [`SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md) · [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)

**Session:** 2026-07-29 (agent machine) · Fill **Result** with `PASS` / `FAIL` / `SKIP` and a short note.

---

## Prerequisites

- [x] Docker Desktop installed and running — **SKIP:** Docker CLI not on PATH (not installed / not in shell PATH)
- [x] Node.js 20+ installed — **PASS** (v24.13.0)
- [x] .NET SDK 10+ installed — **PASS** (10.0.302)
- [x] Git installed — **PASS** (2.54.0)
- [x] `npm install` completed at repo root — assumed present
- [ ] PostgreSQL available — backend connected during `npm run dev` but some schema warnings

| Check | Result | Notes |
|-------|--------|-------|
| `npm run validate:scripts` | **PASS** | 13 root bats documented; pairing OK |
| `npm run test:scripts` | **PASS** | PASS=40 FAIL=0 |

---

## Root Scripts

### start-dev.bat

- [x] Run `start-dev.bat` / `npm run dev` (equivalent)
- [x] Verify API starts (http://localhost:5184) — started (DB column warnings in logs)
- [x] Verify Admin starts — Sentry/Next warnings only
- [x] Verify POS starts — Expo started
- [x] Verify Sites starts (http://localhost:3001) — **fixed** turbopack root; Ready in ~1s after fix
- [x] Stopped processes after smoke launch

| Result | Notes |
|--------|-------|
| **PASS** (after fix) | Sites failed until `frontend-sites/next.config.mjs` set `turbopack.root` to monorepo root |

### start-backend.bat / start-admin.bat / start-pos.bat / start-sites.bat

| Result | Notes |
|--------|-------|
| **SKIP** (partial) | Covered via `npm run dev` / `dev:sites`; full per-bat interactive Ctrl+C not repeated |

### test-all.bat

- [x] Run equivalent steps (non-interactive; no `pause`)
- [x] Backend: **FAIL** suite — Failed 58 / Passed 3599 / Skipped 50 (script would abort at step 1 — correct behavior)
- [x] Admin: **FAIL** suite — 302 failed / 1638 passed (Invalid hook call / React duplicates — pre-existing)
- [x] POS: `npm run test` is Jest — **PASS** as bat command; accidental `jest --run` fails (Vitest flag — do not pass `--run` to POS)

| Result | Notes |
|--------|-------|
| **PASS** (wrapper) / **FAIL** (suites) | `.bat` correctly stops on first failure; product test debt is separate from script DX |

### clean-all.bat

| Result | Notes |
|--------|-------|
| **SKIP** | Destructive; not run this session |

---

## Docker Scripts

### docker-up.bat / docker-status.bat / docker-down.bat

- [x] Run without Docker CLI
- [x] Verify clear error: `Docker CLI not found on PATH!`

| Result | Notes |
|--------|-------|
| **PASS** (error path) | Improved message vs “not running”; full up/status/down **SKIP** until Docker Desktop available |

### docker-clean.bat

| Result | Notes |
|--------|-------|
| **SKIP** | Destructive + no Docker |

### deploy.bat / rollback.bat

| Result | Notes |
|--------|-------|
| **SKIP** | Prod Compose only |

---

## Scripts Folder Scripts

### scripts/smoke-test.bat

- [x] Run with API down
- [x] Verify `[ERROR] API health check failed!` (curl exit 7)

| Result | Notes |
|--------|-------|
| **PASS** (fail-closed) | Happy-path **SKIP** (no full stack kept up) |

### scripts/validate-scripts.bat / test-scripts.bat

| Result | Notes |
|--------|-------|
| **PASS** | Via `npm run validate:scripts` / `test:scripts` |

### scripts/clean-backend.bat / ensure-bmf-prueftool.bat

| Result | Notes |
|--------|-------|
| **SKIP** | Not required for this session |

---

## Session log

| # | Script | Result | Issue / fix |
|---|--------|--------|-------------|
| 1 | `validate-scripts.ps1` | **PASS** | — |
| 2 | `test-scripts.ps1` | **PASS** | — |
| 3 | `test-all.bat` (equiv.) | **PASS wrapper / FAIL suites** | Backend 58 fail; Admin 302 fail; POS ok with plain `npm run test` |
| 4 | `docker-up.bat` | **PASS** (no Docker) | Clear PATH error |
| 5 | `docker-status.bat` | **PASS** (no Docker) | Clear PATH error |
| 6 | `docker-down.bat` | **PASS** (no Docker) | Clear PATH error |
| 7 | `start-dev.bat` / `npm run dev` | **PASS** after Sites fix | Turbopack monorepo root |
| 8 | `scripts\smoke-test.bat` | **PASS** (API down) | Fail-closed |

---

## Issues found & fixes

| Issue | Severity | Fix |
|-------|----------|-----|
| Docker bats said “not running” when CLI missing | Low | `where docker` check before `docker info` in `docker-up` / `status` / `down` / `clean` |
| `frontend-sites` Turbopack: wrong root → `src/app`, cannot find `next` | High (blocks `start-dev`) | `turbopack.root` + `outputFileTracingRoot` → monorepo root in `frontend-sites/next.config.mjs` |
| Backend / Admin unit tests failing | Medium (product) | **Not fixed here** — document only; `test-all.bat` correctly exits non-zero |
| Passing `--run` to POS Jest breaks CLI | Low (tester mistake) | Use `npm run test` only in `frontend/` (as `test-all.bat` does) |

---

## Follow-up for teammates

1. Install Docker Desktop and re-run docker-up → status → down → smoke-test with stack up.
2. Investigate Admin “Invalid hook call” / duplicate React and backend DemoProductImport failures separately from script DX.
3. Keep `npm run validate:scripts` green in CI.
