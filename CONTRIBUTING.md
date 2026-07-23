# Contributing to Regkasse

Guidelines for working in this monorepo. Prefer small, reversible changes over broad rewrites. Executable truth lives in code, package configs, and CI — not only in docs.

## Prerequisites

| Tool | Notes |
|------|-------|
| Node.js | **20+** LTS |
| npm | **Workspaces** at repo root (see `package.json` → `workspaces`) |
| .NET SDK | **10.x** |
| PostgreSQL | **16+** (local or Docker) |
| Optional | Redis, Docker Desktop, Android Studio / Xcode / Expo Go |

## Clone and install

```bash
git clone <repo-url> Regkasse
cd Regkasse

# Preferred: install all JS workspaces from root (see .npmrc legacy-peer-deps)
npm install

# Or install a single workspace if you only need one app:
# npm install -w registrierkasse-admin

# Backend secrets (first time)
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
cd ..
```

Optional root helpers:

```bash
npm install                 # workspaces + prepare (Husky hooks)
npm run install:git-hooks   # re-install hooks if needed
npm run precommit           # same as the git pre-commit hook
npm run verify:api-client   # Orval / OpenAPI drift check
```

**Husky pre-commit** (`.husky/pre-commit` → `scripts/git-hooks/pre-commit.mjs`):

1. API client verify (`--openapi-only`, or full if `swagger.json` / generated client staged)
2. `lint` / `typecheck` only for **packages with staged files** (keeps commits fast)
3. Tests **off** by default — set `HUSKY_RUN_TESTS=1` for fast contract tests

Skip: `SKIP_PRECOMMIT=1`, `SKIP_API_CLIENT_VERIFY=1`, `SKIP_PRECOMMIT_LINT=1`, `SKIP_PRECOMMIT_TYPECHECK=1`.

## Daily development

From the **repository root**:

| Command | What it runs |
|---------|----------------|
| `npm run dev` | **All** app servers in parallel (backend, POS, admin, sites) |
| `npm run dev:workspaces` | Native `npm run dev --workspaces` (sequential; blocks on first server) |
| `npm run build` / `test` / `lint` | All workspaces via `npm run … --workspaces --if-present` |
| `make <target>` / `just <recipe>` | Same orchestration — [`Makefile`](Makefile) / [`Justfile`](Justfile) (`dev`, `build`, `test`, `lint`, `clean`, `docker-up`, …) |
| `npm run dev:backend` | API on http://localhost:5184 |
| `npm run dev:admin` | FA on http://localhost:3000 |
| `npm run dev:pos` | Expo Metro (typically :8081) |
| `npm run dev:sites` | Tenant sites on http://localhost:3001 |

Or work inside a package (`cd frontend-admin && npm run dev`). Standard scripts per workspace: **`dev`**, **`build`**, **`test`**, **`lint`**, **`typecheck`** (where applicable).

Backend is included as workspace `@regkasse/backend` (npm scripts wrap `dotnet`).

### Environment

- **Admin:** `frontend-admin/.env.local` from `.env.example` — `NEXT_PUBLIC_API_BASE_URL`, `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`
- **POS:** `frontend/.env` — `EXPO_PUBLIC_API_BASE_URL` (include `/api`), `EXPO_PUBLIC_DEV_TENANT_ID`
- **Sites:** `frontend-sites/.env.local` — `NEXT_PUBLIC_API_BASE_URL`
- **Dev tenant:** `X-Tenant-Id: dev` or `?tenant=dev` (Development only)

## Monorepo map

See the [root README](README.md#repository-layout). Package READMEs:

- [backend/README.md](backend/README.md)
- [frontend/README.md](frontend/README.md)
- [frontend-admin/README.md](frontend-admin/README.md)
- [frontend-sites/README.md](frontend-sites/README.md)
- [localization/README.md](localization/README.md)
- [scripts/README.md](scripts/README.md)
- [tools/README.md](tools/README.md)
- [testsprite/README.md](testsprite/README.md)
- [docs/README.md](docs/README.md)
- [ai/README.md](ai/README.md)

## Language and UI rules

| Context | Language |
|---------|----------|
| Code identifiers / API errors / backend logs | English |
| POS UI strings | German (`de-DE`) — do not translate to EN/TR |
| Admin UI strings | i18n under `frontend-admin/src/i18n/` (de/en/tr) |
| Git commits | English |

## API boundaries (do not cross)

| Client | Allowed | Forbidden |
|--------|---------|-----------|
| POS (`frontend/`) | `/api/pos/*`, `/api/Auth/*`, `/api/Receipts/*` | `/api/admin/*` |
| Admin (`frontend-admin/`) | `/api/admin/*`, `/api/Auth/*` | `/api/pos/*` |

Cross-tenant access must return **HTTP 404** (not 403). Do not use `IgnoreQueryFilters()` except Super Admin ops.

## High-risk areas

Treat with extra care (tests + review): payments, TSE/RKSV signature chain, voucher ledger, FinanzOnline outbox, tenant isolation, backup/restore, auth/RBAC. See [`ai/07_DO_NOT_TOUCH.md`](ai/07_DO_NOT_TOUCH.md) and [`AGENTS.md`](AGENTS.md).

## Making changes

1. Read nearby code and the package README before editing.
2. Prefer minimal diffs; do not mix unrelated refactors.
3. Keep schema changes additive; never edit committed EF migrations.
4. After OpenAPI changes: regenerate FA client (`npm run generate:api` in `frontend-admin`, then `npm run verify:api-client` from root).
5. For medium/large work: short plan, affected files, risks, compatibility, test strategy.

## Validation before PR

From repo root when relevant:

```bash
npm run test:backend
npm run test:admin
npm run test:pos
npm run verify:api-client
npm run i18n:ci
```

CI maps (see [`.github/workflows/README.md`](.github/workflows/README.md)): backend unit + PostgreSQL integration, Frontend Admin (lint/typecheck/test/build/E2E), POS, Sites, OpenAPI alignment, localization. Optional Slack: secret `SLACK_WEBHOOK_URL`.

Also useful:

```bash
node scripts/validate-critical-openapi-paths.mjs
cd frontend-admin && npm run lint && npm run typecheck
cd frontend && npm run lint && npm run typecheck
```

## Pull requests

- Focused PRs; clear description of **why**.
- Unit/integration tests where behavior changes.
- No secrets in commits (`.env`, PEMs, production keys).
- No `console.log` in production FE/FA code; avoid TypeScript `any` (prefer `unknown`).
- Sensitive ops need audit events where the domain already requires them.

## Why npm workspaces (with care)?

Root `package.json` declares workspaces: `backend`, `frontend`, `frontend-admin`, `frontend-sites`, `localization`.

- **`build` / `test` / `lint`:** `npm run <script> --workspaces --if-present`
- **`dev`:** parallel runner (`scripts/dev-workspaces.mjs`) — npm’s native `--workspaces` is **sequential** and would block on the first long-running server; use `npm run dev:workspaces` if you want that behavior
- **`.npmrc`:** `legacy-peer-deps=true` for Expo / TypeScript peer ranges
- Apps may still keep local `node_modules`; prefer `npm install` from the repo root after clone

Do not add `pnpm-workspace.yaml` unless the team migrates package managers explicitly.

## Further reading

- [`AGENTS.md`](AGENTS.md) — always-applied development rules
- [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md) — product/architecture brief
- [`docs/`](docs/README.md) — human docs
- [`ai/`](ai/README.md) — contracts for agents and complex changes
