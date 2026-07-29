# New Script Ecosystem Available!

We've added `.bat` files for all common Windows tasks — double-click from the repo root (or run from `cmd` / PowerShell). No need to remember long `npm` / `docker` commands for daily work.

## Quick Start

| Script | What it does |
|--------|----------------|
| `start-dev.bat` | Start everything (API + Admin + POS + Sites) |
| `docker-up.bat` | Start Docker Compose |
| `docker-down.bat` | Stop Docker Compose |
| `docker-status.bat` | See what's running |
| `test-all.bat` | Run Backend → Admin → POS tests |

## More helpers

- **Single surface:** `start-backend.bat` · `start-admin.bat` · `start-pos.bat` · `start-sites.bat`
- **Cleanup:** `clean-all.bat` · `scripts\clean-backend.bat`
- **Smoke:** `scripts\smoke-test.bat` (quick curl) · `scripts\run-comprehensive-smoke.bat` (full suite)
- **Deploy host:** `deploy.bat` / `rollback.bat` (prod Compose — confirm carefully; prefer `git revert` on shared branches)

## Docs

| Doc | Use when |
|-----|----------|
| [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) | **Full reference** (every script) |
| [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) | One-screen pocket card |
| [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) | “Which script?” decision map |
| [`SCRIPTS_COMPLETION_SUMMARY.md`](SCRIPTS_COMPLETION_SUMMARY.md) | Delivery checklist + gaps |

Also linked from the root [`README.md`](../README.md#scripts-windows) and [`CONTRIBUTING.md`](../CONTRIBUTING.md#scripts).

## Validate (optional)

```batch
npm run validate:scripts
npm run test:scripts
```

Questions or missing wrappers → open a PR and follow `scripts/README.md` (document in `SCRIPTS_REFERENCE.md`, keep `npm run verify:bat-ps1` green).
