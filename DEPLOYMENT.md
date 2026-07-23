# Deployment guide

Production-oriented deployment notes for Regkasse (API, POS, Admin).  
Local setup stays in [`DEVELOPMENT.md`](DEVELOPMENT.md). Coupled FA+API releases: [`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md).

**Last updated:** 2026-07-21

**Production hosts (Single POS UI):**

| Surface | URL |
|---------|-----|
| POS | `https://pos.regkasse.at` |
| Admin (FA) | `https://admin.regkasse.at` |
| API | `https://api.regkasse.at` |
| Tenant sites | `/[slug]` (and optional verified custom domains) |

---

## Prerequisites

### DNS

| Host / pattern | Points to |
|----------------|-----------|
| `api.regkasse.at` | API load balancer / VM |
| `admin.regkasse.at` | Admin (FA) |
| `pos.regkasse.at` | POS web (if hosted) or marketing/redirect as designed |
| `*.regkasse.at` (optional) | Legacy / custom slug hosts, sites, or wildcard edge |

Reserved labels (never tenant slugs): `pos`, `api`, `admin`, `www`. Details: [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md), [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md).

### SSL / TLS

- Certificates covering `api`, `admin`, `pos` (and wildcard `*.regkasse.at` if used).
- Terminate TLS at the reverse proxy / load balancer; preserve `Host` for any Host-based routing.
- API container listens HTTP on **8080** internally (`ASPNETCORE_URLS`); put TLS in front.

### Environment / secrets readiness

- PostgreSQL production database (migrations applied).
- Redis (recommended for Production cache / CSRF session store patterns).
- Secrets injected via env / secret store — **never** bake JWT, DB passwords, or PEMs into images or git.
- Build-time vars for Admin (`NEXT_PUBLIC_*`) and POS (`EXPO_PUBLIC_*`) set **before** build.

### Coupled releases

When Admin consumes new API routes, deploy **backend first**, then FA, in the same window ([`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md)).

---

## Backend Deployment

### Publish (direct)

From repository root:

```bash
dotnet publish backend/KasseAPI_Final.csproj -c Release -o ./artifacts/api
```

Run (example):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://0.0.0.0:8080
export ConnectionStrings__DefaultConnection="Host=…;Database=…;Username=…;Password=…"
export JwtSettings__SecretKey="…"   # min 32 chars
dotnet ./artifacts/api/KasseAPI_Final.dll
```

Apply schema before traffic:

```bash
dotnet ef database update \
  --project backend/KasseAPI_Final.csproj \
  --startup-project backend/KasseAPI_Final.csproj
```

### Docker

Build context **must be the repo root** (references `tools/LicenseGenerator.Core`):

```bash
docker build -f backend/Dockerfile -t regkasse-api:latest .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="…" \
  -e JwtSettings__SecretKey="…" \
  regkasse-api:latest
```

- Image: `mcr.microsoft.com/dotnet/aspnet:10.0`, port **8080**
- Healthcheck: `GET /api/health/live`
- Full config: [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md), [`backend/README.md`](backend/README.md)

### Production checklist (API)

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Real TSE / FinanzOnline mode (not Fake/Simulation) per runbook
- [ ] `TwoFactorAuth__Enabled=true` for SuperAdmin
- [ ] `Security__Csrf__Enabled=true` (no Dev bypass)
- [ ] `Cors__AllowedOrigins` includes production FA/POS/Sites origins; custom site domains listed
- [ ] Backup paths / `Backup__ExecutionAdapterKind=PgDump` when using real backups
- [ ] License public PEM configured (`License` / OfflineVerification)

---

## Frontend (POS) Deployment

POS is an **Expo SDK 56** app. Production API base must be the shared API:

```text
EXPO_PUBLIC_API_BASE_URL=https://api.regkasse.at/api
```

Tenant comes from JWT after login — not per-tenant `{slug}` API hosts ([`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md)).

### Build

```bash
cd frontend
# Set EXPO_PUBLIC_* in env or EAS secrets BEFORE build
npm ci
npm run build          # expo export (web static) — see package.json "build"
```

Native / store binaries (preferred for registers):

```bash
cd frontend
npx eas build --platform android --profile production
# npx eas build --platform ios --profile production
```

Profile: [`frontend/eas.json`](frontend/eas.json) (`production` → Android APK, local credentials).

### Deploy channels

| Channel | Notes |
|---------|--------|
| **EAS / internal APK** | Sideload to devices; signing: [`docs/ANDROID_RELEASE_SIGNING.md`](docs/ANDROID_RELEASE_SIGNING.md) |
| **Google Play / App Store** | Configure store credentials outside this guide; do not commit keystores |
| **Web export** | Host `dist/` / Expo web output behind HTTPS on `pos.regkasse.at` if using web POS |

Install guide (de): [`docs/REGKASSE_APK_INSTALLATIONSANLEITUNG.md`](docs/REGKASSE_APK_INSTALLATIONSANLEITUNG.md).

---

## Frontend-Admin Deployment

`NEXT_PUBLIC_*` must be present at **`next build` time** — runtime-only env will not fix an already-built bundle ([`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md)).

### Build

```bash
cd frontend-admin
export NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at
export NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD
npm ci
npm run build
npm run start          # serves on :3000
```

### Vercel

- Config: [`frontend-admin/vercel.json`](frontend-admin/vercel.json) (`framework: nextjs`, `npm run build`).
- Set `NEXT_PUBLIC_API_BASE_URL` and `NEXT_PUBLIC_RKSV_ENVIRONMENT` in the Vercel project **Environment Variables** for Production (available to the build).
- Do **not** set `outputDirectory` to `.next`.

### Docker

```bash
cd frontend-admin
NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at \
NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD \
docker compose build
docker compose up -d
```

Or `docker build` with `--build-arg` for each `NEXT_PUBLIC_*` (see `frontend-admin/Dockerfile`). CI image: [`.github/workflows/frontend-admin-deploy.yml`](.github/workflows/frontend-admin-deploy.yml) (GHCR).

Nginx sample: `frontend-admin/nginx.conf` (`admin.regkasse.at` → `:3000`).

---

## Environment Variables

### Backend (required / critical)

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | e.g. `http://+:8080` (container) |
| `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `JwtSettings__SecretKey` | JWT signing (≥32 chars) |
| `JwtSettings__Issuer` / `JwtSettings__Audience` | Token validation |
| `Redis__ConnectionString` | Distributed cache (recommended) |
| `Redis__InstanceName` | Key prefix (e.g. `Regkasse_Prod`) |
| `Cors__AllowedOrigins` | Explicit origins beyond `*.regkasse.at` HTTPS |
| `TwoFactorAuth__Enabled` | `true` in Production |
| `Security__Csrf__Enabled` | `true` in Production |
| `License__OfflineVerificationPublicKeyPem` (or file paths) | Offline license verify |
| `Backup__*` | Staging/archive roots, `ExecutionAdapterKind`, `pg_dump` path when using real backups |
| Fiskaly / TSE secrets | Via secure config — see `CONFIGURATION.md` |
| FinanzOnline | Credentials typically DB/company settings; cutover tokens per runbook |

Full map: [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md).

### Frontend Admin (build-time)

| Variable | Purpose |
|----------|---------|
| `NEXT_PUBLIC_API_BASE_URL` | `https://api.regkasse.at` |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | `PROD` or `TEST` (label / FO mode UI) |
| `NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN` | Optional; default `regkasse.at` |
| `NEXT_PUBLIC_POS_APP_URL` | Optional POS deep link |
| `NEXT_PUBLIC_SENTRY_DSN` | Optional |

### Frontend POS (build-time)

| Variable | Purpose |
|----------|---------|
| `EXPO_PUBLIC_API_BASE_URL` | `https://api.regkasse.at/api` |
| `EXPO_PUBLIC_ADMIN_BASE_URL` | Optional license / FA links |
| `EXPO_PUBLIC_DEV_TENANT_ID` | **Dev only** — omit in production builds |

### Frontend Sites (if deployed)

| Variable | Purpose |
|----------|---------|
| `NEXT_PUBLIC_API_BASE_URL` | Shared API origin |

---

## Rollback

### Application binaries (API / FA / POS packages)

Ops scripts under [`scripts/`](scripts/README.md) (typical server layout `/var/www/regkasse`):

```bash
# Before deploy — archive current release (excludes secrets by design)
sudo ./scripts/prepare-rollback-backup.sh

# After a bad release — restore last (or named) stamp
sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
# or: sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh 20260719-120000
```

- Restores **backend**, **frontend-admin**, and **frontend** package trees from `backup/<stamp>`.
- Does **not** roll back PostgreSQL / EF migrations (schema rollback is a separate, high-risk procedure).
- Helper: `./scripts/document-rollback.sh`

### Container / platform rollbacks

| Platform | Action |
|----------|--------|
| Docker / GHCR | Redeploy previous image tag / digest |
| Vercel | Promote previous Production deployment in the Vercel dashboard |
| EAS / stores | Ship previous build; store review may apply |

### Data / fiscal

- **Do not** use API “restore” against production DB (validation-only / drills only). See [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](docs/BACKUP_AND_DISASTER_RECOVERY.md), [`docs/restore-boundary-notes.md`](docs/restore-boundary-notes.md).
- Database point-in-time recovery is an ops/DR procedure outside the app rollback scripts.

### Recommended order after rollback

1. Confirm API health (`/api/health/live`, `/api/health/ready`).
2. Confirm FA build matches API contract (or roll FA to matching tag).
3. Smoke login + one read-only admin page + POS health against `api.regkasse.at`.

---

## Related docs

| Doc | Topic |
|-----|--------|
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | Local setup |
| [`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md) | Coupled FA + API |
| [`frontend-admin/docs/CI_CD.md`](frontend-admin/docs/CI_CD.md) | FA CI/CD |
| [`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md) | `NEXT_PUBLIC_*` |
| [`docs/OFFLINE_PRODUCTION_DEPLOYMENT.md`](docs/OFFLINE_PRODUCTION_DEPLOYMENT.md) | Offline systems go-live |
| [`docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | FO production cutover |
| [`.github/workflows/README.md`](.github/workflows/README.md) | CI inventory |
