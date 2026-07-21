# Admin panel: `NEXT_PUBLIC_*` at build time (Regkasse repo facts)

This document is grounded in **what exists in this repository** (as of the last update). It explains why `NEXT_PUBLIC_RKSV_ENVIRONMENT` must be present when `next build` (or `next dev` compilation) runs, not only at container or process **runtime**.

## Deployment Requirements (multi-tenant)

### DNS Configuration

- Wildcard **A** record: `*.regkasse.at` → server IP (tenant subdomains + `admin.regkasse.at`).
- SSL certificate must support the wildcard domain (`*.regkasse.at`).
- Load balancer / reverse proxy must preserve the `Host` header for API tenant resolution.

### Environment Variables (backend)

| Variable                             | Effect on tenant resolution                                     |
| ------------------------------------ | --------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT=Development` | API accepts `X-Tenant-Id` and `?tenant=` (slug) on loopback/dev |
| `ASPNETCORE_ENVIRONMENT=Production`  | API uses **subdomain / Host only**                              |

Full ops notes: `REGKASSE_AI_ONBOARDING.md` §16 Deployment Requirements.

## 1. What this repo contains

| Area                                                     | In this repo?                                                                                                        |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `Dockerfile` for `frontend-admin`                        | **Yes** — `frontend-admin/Dockerfile` (multi-stage: `deps` → `builder` → non-root `runner`, `npm start`)             |
| `docker-compose.yml` for `frontend-admin`                | **Yes** — `frontend-admin/docker-compose.yml` (port 3000, build-args for `NEXT_PUBLIC_*`)                            |
| `vercel.json` for `frontend-admin`                       | **Yes** — Next.js framework + `npm run build`; **do not** set `outputDirectory` to `.next`                          |
| `nginx.conf` for `frontend-admin`                        | **Yes** — reverse proxy sample for `admin.regkasse.at` → `:3000`                                                    |
| GitHub Actions running `frontend-admin` production build | **Yes** — `frontend-admin-ci.yml`, `frontend-admin-deploy.yml` (GHCR), `api-client-alignment.yml`. See `docs/CI_CD.md`. |
| Committed `.env.local`                                   | **No** — `.gitignore` excludes `.env.local`; CI must not rely on a checked-in file for secrets or public build vars. |

## 2. Build-time vs runtime-only

- **Build-time (correct for `NEXT_PUBLIC_*`):** The variable is set in the environment **before** `next build` or before the dev server compiles modules that read `process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT`. Next.js inlines these values into the **client JavaScript bundle**.
- **Runtime-only (wrong for fixing an already-built bundle):** Setting `NEXT_PUBLIC_RKSV_ENVIRONMENT` only when starting the container (`docker run -e`, Compose `environment:` on the running service, K8s pod env **after** image build) does **not** change strings already compiled into static chunks. `next start` serves those chunks as built.

**Why the `/rksv` badge stays `UNCONFIGURED`:** The browser loads JS that was emitted with `undefined` or empty for that variable. Runtime env on the Node server does not rewrite those client bundles.

## 3. CI in this repo (verified)

Primary quality gate: **`.github/workflows/frontend-admin-ci.yml`** — sets `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` and `NEXT_PUBLIC_API_BASE_URL` for `npm run build` (and E2E).

Also: **`.github/workflows/api-client-alignment.yml`** (“Admin build” step) and **`.github/workflows/frontend-admin-deploy.yml`** (Docker build-args for staging/prod).

Those values must be on the **same step / image build** as `next build`, so they are available at **build time** (required by `next.config.mjs` for production builds).

Pipeline detail: [`CI_CD.md`](./CI_CD.md). Other workflows (`localization-validation.yml`, `api-contract-tests.yml`) install FA deps but are not the full quality gate.

## 4. Docker / Vercel / Nginx (in-repo)

### Docker

Canonical files:

- `frontend-admin/Dockerfile`
- `frontend-admin/docker-compose.yml`
- `frontend-admin/.dockerignore`

`ARG` / `ENV` for `NEXT_PUBLIC_*` are set **before** `RUN npm run build`. Pass values with `docker build --build-arg` or Compose `build.args`.

```bash
cd frontend-admin
NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at \
NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD \
docker compose build
docker compose up -d
```

The runner stage uses a non-root user (`nextjs`, uid 1001) and `CMD ["npm", "start"]` on port **3000**.

### Vercel

Canonical file: `frontend-admin/vercel.json`

| Field | Value |
| ----- | ----- |
| `framework` | `nextjs` |
| `installCommand` | `npm ci` |
| `buildCommand` | `npm run build` |
| `outputDirectory` | **omit** — Next.js owns `.next`; overriding Project Settings to `.next` breaks SSR |

Set `NEXT_PUBLIC_API_BASE_URL` and `NEXT_PUBLIC_RKSV_ENVIRONMENT` as **Build** environment variables in the Vercel project (Root Directory = `frontend-admin`).

### Nginx (self-hosted)

- `frontend-admin/nginx.conf` — TLS vhost proxying to `127.0.0.1:3000`
- `frontend-admin/deploy/nginx-map-connection-upgrade.conf` — WebSocket `map` for `http {}`

Also documented in `frontend-admin/README.md` → **Docker** / **Vercel** / **Nginx**.

## 5. Local dev, CI, production — correct flow (summary)

| Context                    | What to do                                                                                                                                                                       |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Local**                  | Put `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` (or `PROD`) in `frontend-admin/.env.local`. Run `cd frontend-admin && npm run dev`. Restart dev after changing any `NEXT_PUBLIC_*`.      |
| **CI**                     | Set `NEXT_PUBLIC_*` on the job step that runs `npm run build` (see `api-client-alignment.yml`). Do not assume `.env.local` exists on the runner.                                 |
| **Production / container** | Set `NEXT_PUBLIC_*` during **image build** (`frontend-admin/Dockerfile` build-args / Compose `build.args`, or CI build step). Setting them only at `docker run` is too late for the client bundle. |

## 6. Related code

- Env read / RKSV badge: `src/shared/config/rksvEnvironment.ts`
- Build guard: `next.config.mjs`
- Setup template: `.env.example`

## 7. Coupled backend + frontend releases

When shipping admin dashboard RKSV overview endpoints (`/api/rksv/monatsbeleg/status-overview`, `/api/rksv/reminder/status-overview`), deploy **API and `frontend-admin` together**. See **`docs/ADMIN_FA_DEPLOY.md`** for route matrix and smoke checks.

## 8. Bundle analysis (optional, local)

From `frontend-admin/`:

```bash
npm run analyze
```

Uses `@next/bundle-analyzer` (`ANALYZE=true`, `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`). Not part of CI; for investigating client chunk size after dependency or route changes.
