# Regkasse

RKSV-compliant multi-tenant POS platform for Austrian cash registers (Registrierkassen).

| Surface | Local | Production |
|---------|-------|------------|
| POS | http://localhost:8081 | https://pos.regkasse.at |
| Admin (FA) | http://localhost:3000 | https://admin.regkasse.at |
| Tenant sites | http://localhost:3001 | `/[slug]` storefronts (+ optional custom domains) |
| API | http://localhost:5184 | https://api.regkasse.at |

---

## Project Overview

Regkasse is an npm-workspace monorepo for:

- **POS** — cashier operations (cart, payment, receipts, offline queues); UI copy in **German (de-DE)**
- **Admin (FA)** — Mandanten-Admin and Super Admin back office (users, RKSV, backup, billing, digital services); **i18n de/en/tr**
- **Tenant websites** — shared Next.js storefronts and online-order intake (`frontend-sites`; not fiscal POS)
- **API** — ASP.NET Core multi-tenant backend with RKSV/TSE, FinanzOnline outbox, backup/DR, and licensing

**Single POS UI:** production POS is one shared host (`pos.regkasse.at`); tenant comes from JWT `tenant_id` after login — not `{slug}.regkasse.at` as the POS entry point. See [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md).

**Boundaries:** POS → `/api/pos/*`; Admin → `/api/admin/*`; Sites → `/api/public/*` + `/api/sites/*`. Cross-tenant access returns **HTTP 404**.

---

## Quick Start

### Prerequisites

| Tool | Notes |
|------|-------|
| Node.js | **20+** LTS |
| npm | Workspaces enabled at repo root |
| .NET SDK | **10.x** |
| PostgreSQL | **16+** (local or Docker) |
| Optional | Redis (`scripts/start-redis-dev.ps1`), Expo Go / Android Studio |

### Install

```bash
git clone <repo-url> Regkasse
cd Regkasse
npm install   # JS workspaces + Husky prepare

# Backend config (first time) — see backend/README.md
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
cd ..
```

### Run everything

```bash
npm run dev                 # parallel: API + POS + Admin + Sites
```

**Docker (optional):** copy `.env.example` → `.env`, then `docker compose up --build` — see [`DEVELOPMENT.md`](DEVELOPMENT.md#docker-compose-full-stack). Or `just docker-up` / `make docker-up`.

### Run each project

| Project | Command | URL |
|---------|---------|-----|
| API | `npm run dev:backend` | http://localhost:5184 |
| Admin | `npm run dev:admin` | http://localhost:3000 |
| POS | `npm run dev:pos` | http://localhost:8081 (Expo) |
| Sites | `npm run dev:sites` | http://localhost:3001 |

Or from a package directory (`cd frontend-admin && npm run dev`). Workspace scripts: `dev`, `build`, `test`, `lint`, `typecheck` (where present).

### Dev tenant

In **Development** only: `X-Tenant-Id: dev` or `?tenant=dev`. Production authenticated traffic uses JWT `tenant_id`.

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

Full setup: [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

## Tech Stack

| Area | Technology |
|------|------------|
| Backend | ASP.NET Core **10** (`net10.0`), EF Core **10.0.10**, PostgreSQL, JWT / Identity |
| Admin (FA) | Next.js **16.2.x**, React **19.2.x**, Ant Design **6**, TanStack Query, Orval |
| POS | Expo SDK **56**, React Native **0.85.x**, TypeScript |
| Sites | Next.js **16**, React **19** |
| i18n | Shared [`localization/`](localization/) tooling; FA locales de/en/tr; POS de-DE |
| Tooling | npm workspaces, Husky pre-commit, GitHub Actions, TestSprite YAML + Node runners |
| Infra helpers | Redis (optional), license issuer under [`tools/`](tools/) |

Stack pins are also summarized in [`AGENTS.md`](AGENTS.md) § Updated Stack Versions.

---

## Repository layout

| Folder | Purpose |
|--------|---------|
| [`backend/`](backend/) | ASP.NET Core API — auth, payments, RKSV/TSE, FinanzOnline, backup, billing, OpenAPI |
| [`frontend/`](frontend/) | Mobile POS (Expo) — cashier UI |
| [`frontend-admin/`](frontend-admin/) | Admin panel (Next.js) |
| [`frontend-sites/`](frontend-sites/) | Shared tenant websites / online-order UI |
| [`localization/`](localization/) | i18n import/export/validation and CI budgets |
| [`scripts/`](scripts/) | OpenAPI verify, seeds, git hooks, SQL helpers — [`scripts/README.md`](scripts/README.md) |
| [`tools/`](tools/) | License generator + wrappers — [`tools/README.md`](tools/README.md) |
| [`testsprite/`](testsprite/) | API/E2E specs + CI validate/smoke — [`testsprite/README.md`](testsprite/README.md) |
| [`docs/`](docs/) | Operator/developer documentation |
| [`ai/`](ai/) | AI/agent contracts and guardrails |
| [`shared/`](shared/) | Small shared constants for tooling |
| [`.github/workflows/`](.github/workflows/) | CI inventory — [`.github/workflows/README.md`](.github/workflows/README.md) |

---

## Documentation

| Doc | Audience |
|-----|----------|
| [`docs/README.md`](docs/README.md) | **Docs index** (start here for human guides) |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Setup, conventions, PRs, Husky |
| [`API_CONTRACT.md`](API_CONTRACT.md) | **HTTP API index** (Auth, Users, Digital, Billing) ↔ swagger |
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | **Local setup** — prerequisites, run, test, troubleshooting |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | **Production deploy** — DNS/SSL, API/POS/FA, env vars, rollback |
| [`AGENTS.md`](AGENTS.md) | Always-applied agent / engineering rules |
| [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md) | Product / fiscal onboarding brief |
| [`ai/README.md`](ai/README.md) | AI contract index |
| [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md) | Tenancy & isolation |
| [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md) | Single POS UI hosts |
| [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](docs/BACKUP_AND_DISASTER_RECOVERY.md) | Backup / DR hub |
| [`docs/BILLING_TENANT_LICENSE.md`](docs/BILLING_TENANT_LICENSE.md) | Mandant license sales |
| [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md) | SuperAdmin 2FA |
| [`docs/WORKING_HOURS.md`](docs/WORKING_HOURS.md) | Website hours (never gates POS/FA) |
| [`SECURITY.md`](SECURITY.md) | Vulnerability reporting & developer security practices |

Package READMEs: [`backend/README.md`](backend/README.md), [`frontend/README.md`](frontend/README.md), [`frontend-admin/README.md`](frontend-admin/README.md), [`frontend-sites/README.md`](frontend-sites/README.md).

### API client (OpenAPI → Orval)

Admin consumes `backend/swagger.json` via Orval:

```bash
node scripts/generate-backend-openapi.mjs   # refresh swagger
npm run generate:api                        # Orval → frontend-admin/src/api/generated
npm run verify:api-client                   # fail on drift
```

Husky pre-commit and CI (`api-client-alignment.yml`, optional auto-generate) keep the client aligned. See [`.github/workflows/README.md`](.github/workflows/README.md).

### Roles (short)

| UI (de) | Backend | Scope |
|---------|---------|-------|
| Super-Administrator | `SuperAdmin` | Platform |
| Mandanten-Admin | `Manager` | Own tenant |
| Kassierer | `Cashier` | POS |

---

## Contributing

1. Read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`AGENTS.md`](AGENTS.md) for the area you touch.
2. Prefer small, reversible PRs; do not extend legacy `/api/Payment`, `/api/Cart`, `/api/Product`.
3. After OpenAPI changes: regenerate Orval client and run `npm run verify:api-client`.
4. Pre-commit (Husky): API verify + staged-package lint/typecheck. Tests are opt-in (`HUSKY_RUN_TESTS=1`).
5. Keep POS UI strings German; Admin UI via i18n files under `frontend-admin/src/i18n/`.

```bash
npm run install:git-hooks   # ensure .husky/pre-commit
npm run precommit           # same checks as the hook
npm run lint && npm run typecheck
```

CI inventory: [`.github/workflows/README.md`](.github/workflows/README.md).

---

## Security

To report a vulnerability, see [`SECURITY.md`](SECURITY.md) (responsible disclosure to **security@regkasse.at**). Do not file public issues for security bugs.

---

## License

**Proprietary** — All rights reserved. See [`LICENSE`](LICENSE).

Unauthorized copying, distribution, or use of this software, via any medium, is strictly prohibited without prior written permission from the copyright holder.
