# TestSprite test suite for Regkasse

Human-readable **YAML contracts** under `api/` and `e2e/`, plus **executable Node runners** that work without a proprietary `testsprite-cli` (that package is not on npm).

**Last reviewed:** 2026-07-21

## Inventory

| Path | Role |
|------|------|
| [`api/*.yml`](api/) | API behaviour specs (validated vs `backend/swagger.json` in CI) |
| [`e2e/*.yml`](e2e/) | Admin UI flow specs (manual / MCP / Playwright reference) |
| [`validate-specs.mjs`](validate-specs.mjs) | **CI gate** — every `endpoint:` must exist in OpenAPI |
| [`run-api-smoke.mjs`](run-api-smoke.mjs) | Live HTTP smoke against a running API |
| [`PROMPTS.md`](PROMPTS.md) | Cursor / TestSprite MCP prompt pack |
| [`../testsprite.config.json`](../testsprite.config.json) | Base URLs + default credentials (dev) |

Related MCP/generated artefacts (separate): `testsprite_tests/` at repo root.

## Quick start

```bash
# Always (no server required)
npm run testsprite:validate
# or: node testsprite/validate-specs.mjs

# Live smoke (API must be up)
npm run testsprite:smoke
# or: node testsprite/run-api-smoke.mjs
```

Env for live smoke:

| Variable | Default |
|----------|---------|
| `TESTSPRITE_API_URL` | `http://localhost:5184` |
| `TESTSPRITE_LOGIN` | from `testsprite.config.json` (`admin@admin.com`) |
| `TESTSPRITE_PASSWORD` | from config |
| `TESTSPRITE_TENANT` | `dev` (`X-Tenant-Id`, Development only) |

## API suites (`api/`)

| File | Focus | Tags |
|------|--------|------|
| `health.yml` | Health + `loginIdentifier` auth | smoke |
| `tenant-isolation.yml` | Switcher, 404 cross-tenant, impersonate | smoke, tenant |
| `backup.yml` | Backup status/settings/compliance/costs | smoke |
| `users.yml` | Admin users | regression |
| `pos.yml` | `/api/pos/*` cart/payment | fiscal |
| `offline-orders.yml` | Offline **orders** (not TSE intents) | fiscal |
| `sites-public.yml` | Sites status + public online orders | regression |

## E2E suites (`e2e/`)

| File | Focus |
|------|--------|
| `admin-users.yml` | Login + `/admin/users` + `/backup` |
| `admin-backup.yml` | `/backup`, `/backup/costs`, `/backup/compliance` |

E2E YAML is documentation for QA / MCP / Playwright — not executed by `validate-specs.mjs`.

## CI

Workflow: [`.github/workflows/testsprite.yml`](../.github/workflows/testsprite.yml)

| Job | When |
|-----|------|
| `validate-specs` | PR/push when `testsprite/**` or `swagger.json` changes |
| `live-api-smoke` | `workflow_dispatch` + `live_smoke=true` (optional secrets `TESTSPRITE_LOGIN` / `TESTSPRITE_PASSWORD`) |

## TestSprite MCP (Cursor)

There is **no** reliable public `testsprite-cli` for these YAML files. Use MCP (`@testsprite/testsprite-mcp`) with prompts in [`PROMPTS.md`](PROMPTS.md) for generative runs. Keep YAML as the **source of truth for what must stay true** in OpenAPI.

## Product truths for authors

- Production hosts: `pos` / `admin` / `api`.regkasse.at — not `{slug}` POS entry
- Cross-tenant → **404**
- Invalid password login → **401** (not 400)
- Dual offline systems — do not mix `offline_orders` and `offline_transactions`
- Working hours gate **public online orders only**

## Troubleshooting

```bash
curl http://localhost:5184/api/health
./scripts/reset-test-data.sh   # TestSprite DB seed (SQL), if used
```

If `validate-specs` fails after an API rename: update the YAML `endpoint:` lines or regenerate OpenAPI (`node scripts/generate-backend-openapi.mjs`).
