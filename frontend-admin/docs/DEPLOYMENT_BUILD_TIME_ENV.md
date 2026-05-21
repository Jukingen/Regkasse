# Admin panel: `NEXT_PUBLIC_*` at build time (Regkasse repo facts)

This document is grounded in **what exists in this repository** (as of the last update). It explains why `NEXT_PUBLIC_RKSV_ENVIRONMENT` must be present when `next build` (or `next dev` compilation) runs, not only at container or process **runtime**.

## Deployment Requirements (multi-tenant)

### DNS Configuration

- Wildcard **A** record: `*.regkasse.at` → server IP (tenant subdomains + `admin.regkasse.at`).
- SSL certificate must support the wildcard domain (`*.regkasse.at`).
- Load balancer / reverse proxy must preserve the `Host` header for API tenant resolution.

### Environment Variables (backend)

| Variable | Effect on tenant resolution |
|----------|----------------------------|
| `ASPNETCORE_ENVIRONMENT=Development` | API accepts `X-Tenant-Id` and `?tenant=` (slug) on loopback/dev |
| `ASPNETCORE_ENVIRONMENT=Production` | API uses **subdomain / Host only** |

Full ops notes: `REGKASSE_AI_ONBOARDING.md` §16 Deployment Requirements.

## 1. What this repo contains

| Area | In this repo? |
|------|----------------|
| `Dockerfile` for `frontend-admin` | **No** — no `Dockerfile` or `docker-compose` files were found under the repository root. |
| GitHub Actions running `frontend-admin` production build | **Yes** — `.github/workflows/api-client-alignment.yml` runs `npm run build` in `frontend-admin/`. |
| Committed `.env.local` | **No** — `.gitignore` excludes `.env.local`; CI must not rely on a checked-in file for secrets or public build vars. |

## 2. Build-time vs runtime-only

- **Build-time (correct for `NEXT_PUBLIC_*`):** The variable is set in the environment **before** `next build` or before the dev server compiles modules that read `process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT`. Next.js inlines these values into the **client JavaScript bundle**.
- **Runtime-only (wrong for fixing an already-built bundle):** Setting `NEXT_PUBLIC_RKSV_ENVIRONMENT` only when starting the container (`docker run -e`, Compose `environment:` on the running service, K8s pod env **after** image build) does **not** change strings already compiled into static chunks. `next start` serves those chunks as built.

**Why the `/rksv` badge stays `UNCONFIGURED`:** The browser loads JS that was emitted with `undefined` or empty for that variable. Runtime env on the Node server does not rewrite those client bundles.

## 3. CI in this repo (verified)

Workflow: **`.github/workflows/api-client-alignment.yml`**

The step **“Admin build (TypeScript + Next smoke)”** sets at least:

- `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`
- `NEXT_PUBLIC_API_BASE_URL=…`

on the **same step** as `npm run build`, so the value is available at **build time** (required by `next.config.mjs` for production builds).

Other workflows (`localization-validation.yml`, `api-contract-tests.yml`) install `frontend-admin` dependencies but **do not** run `next build` for the admin app.

## 4. If you add Docker later (recommended pattern)

Place `ARG` / `ENV` **before** `RUN npm run build`. Pass values with `docker build --build-arg` or Compose `build.args`.

### Example `Dockerfile` fragment (not in repo — copy when you add a real file)

```dockerfile
# Build stage
FROM node:20-alpine AS builder
WORKDIR /app
COPY frontend-admin/package.json frontend-admin/package-lock.json ./
RUN npm ci
COPY frontend-admin/ ./
# Must be set before next build (not only at runtime)
ARG NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
ARG NEXT_PUBLIC_API_BASE_URL=http://127.0.0.1:5183
ENV NEXT_PUBLIC_RKSV_ENVIRONMENT=$NEXT_PUBLIC_RKSV_ENVIRONMENT
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
RUN npm run build

# Runtime stage — do NOT expect new NEXT_PUBLIC_* here to change the client bundle
FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=builder /app/.next/standalone ./
# ... plus public/static as per Next standalone docs
CMD ["node", "server.js"]
```

### Example `docker-compose.yml` fragment (build args)

```yaml
services:
  admin:
    build:
      context: .
      dockerfile: path/to/Dockerfile
      args:
        NEXT_PUBLIC_RKSV_ENVIRONMENT: TEST
        NEXT_PUBLIC_API_BASE_URL: https://api.example.com
```

## 5. Local dev, CI, production — correct flow (summary)

| Context | What to do |
|---------|------------|
| **Local** | Put `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` (or `PROD`) in `frontend-admin/.env.local`. Run `cd frontend-admin && npm run dev`. Restart dev after changing any `NEXT_PUBLIC_*`. |
| **CI** | Set `NEXT_PUBLIC_*` on the job step that runs `npm run build` (see `api-client-alignment.yml`). Do not assume `.env.local` exists on the runner. |
| **Production / container** | Set `NEXT_PUBLIC_*` during **image build** (`ARG`/`ENV` + `RUN npm run build`) or equivalent CI build step. Setting them only at `docker run` is too late for the client bundle. |

## 6. Related code

- Env read / RKSV badge: `src/shared/config/rksvEnvironment.ts`
- Build guard: `next.config.mjs`
- Setup template: `.env.example`

## 7. Coupled backend + frontend releases

When shipping admin dashboard RKSV overview endpoints (`/api/rksv/monatsbeleg/status-overview`, `/api/rksv/reminder/status-overview`), deploy **API and `frontend-admin` together**. See **`docs/ADMIN_FA_DEPLOY.md`** for route matrix and smoke checks.
