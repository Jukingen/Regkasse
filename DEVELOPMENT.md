# Local development guide

Single entry point for setting up and running Regkasse on a developer machine.  
For conventions and PRs see [`CONTRIBUTING.md`](CONTRIBUTING.md). For product/agent rules see [`AGENTS.md`](AGENTS.md).

**Last updated:** 2026-07-21

---

## Prerequisites

| Tool | Version / notes |
|------|-----------------|
| **Node.js** | **20+** LTS (npm workspaces at repo root) |
| **.NET SDK** | **10.x** (`net10.0`) |
| **PostgreSQL** | **16+** — local install or Docker |
| **Docker** | Optional but recommended — full stack via root [`docker-compose.yml`](docker-compose.yml), or PostgreSQL-only / Testcontainers |
| **Redis** | Optional for host `dotnet run`; **required** inside Compose (backend uses StackExchange.Redis) — Windows host: `.\scripts\start-redis-dev.ps1` |
| **Git** | Required |
| **Expo Go / Android Studio / Xcode** | Optional — POS device testing |

Check:

```bash
node -v
npm -v
dotnet --version
psql --version   # or: docker ps
```

---

## Setup

Clone and install JS workspaces from the **repository root** (preferred):

```bash
git clone <repo-url> Regkasse
cd Regkasse
npm install
```

`.npmrc` sets `legacy-peer-deps=true` for Expo/Next coexistence.

### Backend (database, migrations, appsettings)

```bash
cd backend

# Templates are tracked; real appsettings*.json are gitignored
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json
# macOS/Linux: cp appsettings.example.json appsettings.json && cp appsettings.Development.example.json appsettings.Development.json

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS_LONG"

dotnet restore KasseAPI_Final.csproj
dotnet ef database update --project KasseAPI_Final.csproj --startup-project KasseAPI_Final.csproj
```

| Concern | Guidance |
|---------|----------|
| Connection string | User secrets or env `ConnectionStrings__DefaultConnection` — never commit passwords |
| JWT secret | Min **32** characters |
| Config map | [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) |
| Docker Postgres example | `docker run --name regkasse-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=kasse_db -p 5432:5432 -d postgres:16` |
| Full Docker stack | See [Docker Compose (full stack)](#docker-compose-full-stack) below |


Optional Redis: see [`tools/redis/README.md`](tools/redis/README.md) and `backend/CONFIGURATION.md`.

### Frontend (POS)

```bash
# From repo root (already done via npm install), or:
npm install -w cash-register

cd frontend
copy .env.example .env
# macOS/Linux: cp .env.example .env
```

Minimum `.env` values:

```env
EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api
EXPO_PUBLIC_DEV_TENANT_ID=dev
```

- Include **`/api`** in the POS base URL.
- On a physical device, use your LAN IP (e.g. `http://192.168.1.10:5184/api`), not `localhost`.
- Restart Metro after changing `.env`.

### Frontend-Admin (FA)

```bash
npm install -w registrierkasse-admin

cd frontend-admin
copy .env.example .env.local
# macOS/Linux: cp .env.example .env.local
```

Minimum `.env.local` values:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
```

- Put the file in **`frontend-admin/`** (not the repo root) — Next.js only loads env next to the app.
- `NEXT_PUBLIC_*` are **build-time**; change them before `next dev` / `next build`.
- Optional: `NEXT_PUBLIC_DEV_TENANT_ID=dev` for default Dev tenant.

### Frontend-Sites (optional)

```bash
cd frontend-sites
copy .env.example .env.local
# NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
npm run dev    # http://localhost:3001
```

---

## Running the Application

Default local ports: API **5184**, Admin **3000**, POS **8081**, Sites **3001**.

### All surfaces (from repo root)

```bash
npm run dev
```

### Backend

From repo root (correct project path):

```bash
dotnet run --project backend/KasseAPI_Final.csproj
# or: npm run dev:backend
```

- API: http://localhost:5184  
- Health: http://localhost:5184/api/health  
- Swagger (Development): http://localhost:5184/swagger  

> Note: `dotnet run --project backend` alone is not enough — point at `KasseAPI_Final.csproj`.

### Frontend (POS)

```bash
cd frontend
npm start
# or from root: npm run dev:pos
```

Expo Metro typically serves http://localhost:8081.

### Frontend-Admin

```bash
cd frontend-admin
npm run dev
# or from root: npm run dev:admin
```

Admin UI: http://localhost:3000  
Optional hosts: `http://admin.regkasse.local:3000` (see FA `.env.example`).

### Dev tenant smoke

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

`X-Tenant-Id` / `?tenant=` work only when `ASPNETCORE_ENVIRONMENT=Development`.

---

## Make / Just

Root [`Makefile`](Makefile) and [`Justfile`](Justfile) wrap the same npm / Docker Compose commands.

| Target / recipe | Action |
|-----------------|--------|
| `dev` | `npm run dev` (all surfaces) |
| `build` | `npm run build` |
| `test` | `npm run test` |
| `lint` | `npm run lint` |
| `typecheck` | `npm run typecheck` |
| `clean` | `node scripts/clean-artifacts.mjs` (bin/obj/.next/dist/.expo) |
| `docker-up` | `docker compose up --build -d` |
| `docker-down` | `docker compose down` |
| `docker-up-pos` | Compose with `--profile pos` |
| `verify-api` / `i18n` | API client verify / localization CI |

```bash
# Prefer Just on Windows (native recipes)
winget install Casey.Just
just --list
just clean
just docker-up

# GNU Make (Linux/macOS; Windows: choco/winget GnuWin32.Make — add bin to PATH)
make help
make clean
```

`dev`, `build`, `test`, and `lint` are long-running or heavy; use them when you intend to run the full monorepo suite.

---

## Docker Compose (full stack)

One-command local environment from the **repository root**: PostgreSQL, Redis, ASP.NET API, Frontend-Admin (Next.js), and optionally POS static web (Expo export → nginx).

| Service | Container port | Host default | Notes |
|---------|----------------|--------------|--------|
| `postgres` | 5432 | **5432** | `postgres:16-alpine`, volume `regkasse_pgdata` |
| `redis` | 6379 | **6379** | Required by API `ICacheService` |
| `backend` | 8080 | **5184** | Waits for Postgres + Redis healthy; migrates/seeds on startup |
| `frontend-admin` | 3000 | **3000** | Waits for API healthy; `NEXT_PUBLIC_*` baked at **build** |
| `frontend` (profile `pos`) | 80 | **8081** | Optional Expo **web** static export — not Metro/native |

### Start

```bash
# From repository root
cp .env.example .env
# Windows: copy .env.example .env

# Edit .env — set JWT_SECRET_KEY to ≥32 random characters

docker compose up --build
# Optional POS web:
docker compose --profile pos up --build
```

Detach: `docker compose up --build -d`  
Stop: `docker compose down` (add `-v` to wipe Postgres/Redis volumes).

### Smoke checks

```bash
curl -fsS http://localhost:5184/api/health/live
curl -fsS -H "X-Tenant-Id: dev" http://localhost:5184/api/health
curl -fsSI http://localhost:3000/login
# With profile pos:
curl -fsSI http://localhost:8081/
```

Open Admin: http://localhost:3000 — API Swagger (Development): http://localhost:5184/swagger

### Environment variables

Root [`.env.example`](.env.example) documents Compose vars. Compose loads `.env` automatically (gitignored).

| Variable | Purpose |
|----------|---------|
| `POSTGRES_*` | DB user/password/name/host port |
| `JWT_SECRET_KEY` | Backend `JwtSettings__SecretKey` (min 32 chars) |
| `ASPNETCORE_ENVIRONMENT` | Default `Development` (CORS, Dev tenant header, CSRF/2FA bypass) |
| `NEXT_PUBLIC_API_BASE_URL` | **Build-arg** for Admin — use `http://localhost:5184` (browser on host) |
| `EXPO_PUBLIC_API_BASE_URL` | **Build-arg** for POS web — include `/api` |

Important:

- Clients in the browser call **`localhost`**, not the Docker DNS name `backend`.
- Changing `NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` requires **`docker compose build --no-cache`** for that service (values are inlined at image build).
- Backend Dockerfile build context is the **repo root** (`-f backend/Dockerfile`). Admin context is `frontend-admin/`. POS Dockerfile is `frontend/Dockerfile` with root context.
- FA-only compose remains at [`frontend-admin/docker-compose.yml`](frontend-admin/docker-compose.yml) if you only need the Admin image.

### Troubleshooting (Compose)

| Symptom | Check |
|---------|--------|
| `backend` unhealthy / exit | `docker compose logs backend` — Postgres ready? JWT ≥32 chars? Redis up? |
| Admin API calls fail (CORS / network) | Rebuild Admin with `NEXT_PUBLIC_API_BASE_URL=http://localhost:5184`; confirm API port published |
| Port already in use | Change `API_PORT` / `ADMIN_PORT` / `POSTGRES_PORT` in `.env` |
| Schema / login issues | API runs `Database.Migrate()` on startup; wipe volumes with `docker compose down -v` only if you accept data loss |
| Docker CLI missing | Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and ensure `docker compose version` works in your shell |

---

## Testing

### Backend

```bash
cd backend
dotnet test
# or: npm run test:backend
```

PostgreSQL-tagged integration tests may need Docker (see CI / `backend/README.md`). Filter unit-only:

```bash
dotnet test --filter "Category!=PostgreSql"
```

### Frontend (POS)

```bash
cd frontend
npm run test
# or: npm run test:pos
```

### Frontend-Admin

```bash
cd frontend-admin
npm run test
# or: npm run test:admin
```

Contract / truth suites: `npm run test:contract` inside `frontend-admin`.

### Other useful checks

```bash
npm run typecheck
npm run verify:api-client
npm run testsprite:validate
```

---

## Troubleshooting

### API will not start / health fails

- Confirm PostgreSQL is running and the connection string matches (`dotnet user-secrets list`).
- Apply migrations: `dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`.
- Port **5184** in use → stop the other process or change the launch profile.
- Readiness unhealthy while live is OK until DB is reachable (`/api/health/live` vs `/api/health/ready`).

### CORS / Admin or POS cannot call API

- Use the documented Dev origins; backend CORS allows local/LAN and `*.regkasse.local` in Development.
- POS: `EXPO_PUBLIC_API_BASE_URL` must include `/api` and be reachable from the device.
- Admin: `NEXT_PUBLIC_API_BASE_URL` should be `http://localhost:5184` (**without** trailing `/api` if paths already include it).

### Wrong or empty tenant data

- Development: set `X-Tenant-Id: dev` or FA/POS Dev tenant switcher; slug must exist in `tenants`.
- Production model: tenant from JWT — do not expect `{slug}.regkasse.at` as POS entry ([`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md)).

### Admin RKSV page shows UNCONFIGURED

- Ensure `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` (or `PROD`) is in **`frontend-admin/.env.local`**, then restart `next dev`.
- Do not put that env only at the monorepo root.

### Expo / Metro issues

- Restart after `.env` changes; try `npx expo start --clear`.
- Physical device: LAN IP in `EXPO_PUBLIC_API_BASE_URL`; firewall must allow the API port.

### Orval / API client drift

```bash
node scripts/generate-backend-openapi.mjs
npm run generate:api
npm run verify:api-client
```

Pre-commit may block on drift — see [`CONTRIBUTING.md`](CONTRIBUTING.md) (Husky skip vars).

### Redis / cache errors

- Redis is optional for many Dev paths; start with `.\scripts\start-redis-dev.ps1` or Docker if CONFIGURATION requires it.
- Binaries under `tools/redis/` are downloaded on demand (gitignored).

### `npm install` peer dependency conflicts

- Root `.npmrc` uses `legacy-peer-deps=true`. Prefer **root** `npm install` over nested installs when possible.

### Husky hooks not running

```bash
npm run install:git-hooks
git config core.hooksPath   # expect .husky/_ (Husky 9)
```

---

## Related docs

| Doc | Topic |
|-----|--------|
| [`README.md`](README.md) | Project overview |
| [`Makefile`](Makefile) / [`Justfile`](Justfile) | Common `dev` / `build` / `test` / `lint` / `clean` / `docker-*` |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Conventions, workspaces, PRs |
| [`backend/README.md`](backend/README.md) | API deep dive |
| [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) | Config keys |
| [`docs/README.md`](docs/README.md) | Documentation index |
| [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md) | Tenancy |
