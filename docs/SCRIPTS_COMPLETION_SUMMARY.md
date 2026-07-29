# Scripts ecosystem — completion summary

> **Date:** 2026-07-29  
> **Status:** Complete for Windows operator DX (batch wrappers + docs + CI gates)  
> **Validation:** `npm run validate:scripts` + `npm run test:scripts` → **pass**

---

## 1. Scripts created / completed

### Totals

| Scope | Count | Notes |
|-------|------:|-------|
| Root convenience `.bat` | **13** | Start / test / clean / docker / deploy |
| `scripts/` user-facing aliases | **8** | Maintenance helpers listed below |
| `scripts/*.ps1` (documented) | **23** | Pairing + `SCRIPTS_REFERENCE` CI gate |
| `scripts/*.bat` (wrappers + aliases) | **30** | Includes siblings for `.ps1` |

### Root convenience (13)

| File | Role |
|------|------|
| `start-dev.bat` | `npm run dev` (API + Admin + POS + Sites) |
| `start-backend.bat` | `npm run dev:backend` |
| `start-admin.bat` | `npm run dev:admin` |
| `start-pos.bat` | `npm run dev:pos` |
| `start-sites.bat` | `npm run dev:sites` |
| `test-all.bat` | Backend → Admin → POS (sequential) |
| `clean-all.bat` | Confirm + remove build artifacts |
| `docker-up.bat` | `docker compose up -d` (+ Docker Desktop check) |
| `docker-down.bat` | `docker compose down` |
| `docker-clean.bat` | `down -v` + prune (confirm) |
| `docker-status.bat` | Formatted `docker ps` |
| `deploy.bat` | Prod Compose deploy (confirm + smoke + backup gate) |
| `rollback.bat` | `git reset --hard HEAD~1` + `docker-compose.prod.yml` rebuild |

### `scripts/` user-facing wrappers (8)

| File | Role |
|------|------|
| `clean-backend.bat` | → `clean-backend-build.ps1` |
| `dev-purge-tenant.bat` | Confirm → `dev-purge-tenant-catalog.ps1` |
| `generate-dep-export.bat` | → `generate-dep-export-fixtures.ps1` |
| `ensure-bmf-prueftool.bat` | → `ensure-bmf-prueftool.ps1` |
| `fix-antd.bat` | → `fix-antd-deprecations.mjs` |
| `dev-mail.bat` | Env + `dev-mail-test.bat` |
| `smoke-test.bat` | Lightweight curl (API / Admin / POS) |
| `run-with-log.bat` | Log any command → `logs\run_*.log` |

### Validation / tooling

| File | Role |
|------|------|
| `_common.bat` | Shared `call` helpers |
| `create-bat-wrappers.ps1` (+ `.bat`) | Auto sibling `.bat` generator |
| `test-scripts.ps1` (+ `.bat`) | Dry-run structure tests |
| `validate-scripts.ps1` (+ `.bat`) | Pairing + docs gate |
| `verify-bat-ps1-pairing.mjs` | Allowlisted pairing (CI) |
| `run-comprehensive-smoke.ps1` (+ `.bat`) | Full HTTP / FA / RKSV smoke |

Sibling `.bat` files also exist for other `scripts/*.ps1` (DEP verify, Redis, docker-*.ps1, etc.).

---

## 2. Documentation delivered

| Doc | Purpose |
|-----|---------|
| [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) | Full catalog (purpose / when / prerequisites / output / errors) |
| [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) | Mermaid decision map |
| [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) | Icon pocket card |
| [`SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md) | Automated + manual script test checklist |
| [`SCRIPTS_TEST_CHECKLIST.md`](SCRIPTS_TEST_CHECKLIST.md) | Filled PASS/FAIL session checklist |
| [`BATCH_FILES.md`](BATCH_FILES.md) | Short inventory |
| [`SCRIPTS_COMPLETION_SUMMARY.md`](SCRIPTS_COMPLETION_SUMMARY.md) | This summary |
| Root [`README.md`](../README.md) | Scripts (Windows) section |
| [`CONTRIBUTING.md`](../CONTRIBUTING.md) | Scripts section + PR gates |
| [`DEVELOPMENT.md`](../DEVELOPMENT.md) | Prefer scripts table |
| [`scripts/README.md`](../scripts/README.md) | Folder conventions + how to add scripts |

**Documented for CI:** every root `*.bat` and every `scripts/*.ps1` must appear in `SCRIPTS_REFERENCE.md` (`validate-scripts.ps1`).

---

## 3. Testing status

| Check | How | Result |
|-------|-----|--------|
| Pairing | `npm run verify:bat-ps1` | Pass |
| Pairing + docs | `npm run validate:scripts` | Pass |
| Structural dry-run | `npm run test:scripts` | Pass (does not start servers) |
| Manual start/docker/smoke | Team checklist below | Env-dependent |

CI workflow: [`.github/workflows/scripts-bat-ps1-pairing.yml`](../.github/workflows/scripts-bat-ps1-pairing.yml) runs pairing + `validate-scripts.ps1` + `test-scripts.ps1`.

---

## 4. Remaining gaps

| Gap | Notes |
|-----|--------|
| Interactive manual matrix | Full `start-dev` / Docker / `deploy` on every teammate machine still needed |
| `smoke-test.bat` vs `smoke-test.ps1` | **Name collision:** `.bat` = lightweight curl; `.ps1` = deployment smoke (`API_BASE`). Documented; consider rename later |
| PowerShell tab-completion | Not implemented |
| Script usage analytics | Optional; not implemented |
| macOS / Linux parity | `.sh` wrappers exist for some ops; root `.bat` is Windows-first |
| LicenseGenerator dirs in `clean-all.bat` | Cross-platform `npm run clean` still covers tools build dirs |

---

## 5. Team checklist

- [ ] Share [`SCRIPTS_TEAM_ANNOUNCEMENT.md`](SCRIPTS_TEAM_ANNOUNCEMENT.md) (Slack / Teams / email — optional)
- [ ] All developers have run `scripts/validate-scripts.ps1` (or `npm run validate:scripts`)
- [ ] All developers have run `npm run test:scripts`
- [ ] All developers have read [`docs/SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)
- [ ] All developers have tried `start-dev.bat`
- [ ] All developers have tried `docker-up.bat` and `docker-down.bat`
- [ ] Optional: skim [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) and pin [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md)

---

## 6. What’s next

1. **Add more scripts as needed** — follow `scripts/README.md` (create `.ps1` → `create-bat-wrappers` → document → `validate:scripts`).
2. **PowerShell completion** — optional `Register-ArgumentCompleter` for common `scripts\*.ps1` parameters.
3. **Resolve smoke naming** — e.g. rename deployment script to `deployment-smoke.ps1` to avoid confusion with `smoke-test.bat`.
4. **Script usage analytics (optional)** — lightweight opt-in log of which `.bat` was launched.

---

## 7. Final validation commands

```batch
npm run verify:bat-ps1
npm run validate:scripts
npm run test:scripts
```

```batch
scripts\validate-scripts.bat
scripts\test-scripts.bat
```

Manual (when Docker / stack available):

```batch
start-dev.bat
docker-up.bat
docker-status.bat
scripts\smoke-test.bat
docker-down.bat
```
