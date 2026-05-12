# Regkasse Admin Panel

Next.js Admin Panel for Regkasse POS System.
Built with Ant Design, TanStack Query, and Orval.

## Prerequisites

- Node.js 18+
- Backend API running (KasseAPI)

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```

2. Environment Setup:
   Copy `.env.example` to **`.env.local` in the `frontend-admin/` directory** (same folder as `package.json` and `next.config.mjs`). This monorepo has **no root-level Next.js app** — a `.env.local` at the repository root is **not** read when you run `npm run dev` here; each app (`frontend-admin`, `frontend`) uses its own package root.
   ```env
   NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
   NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
   ```
   (Backend API varsayılan port: 5184; `backend/appsettings.Development.json` içindeki `Urls` ile uyumlu olmalı)

### Next.js: `NEXT_PUBLIC_*` variables (build-time, not runtime-only)

**Diagnosis (confirmed):** Names starting with `NEXT_PUBLIC_` are **compiled into the client JavaScript** when you run `next dev` or `next build`. They are **not** read fresh from the server environment on every request like a secret `DATABASE_URL`.

| Broken flow | Why the UI stays `UNCONFIGURED` |
|-------------|--------------------------------|
| Image built **without** `NEXT_PUBLIC_RKSV_ENVIRONMENT`, then variable set only on `docker run` / Compose `environment:` / K8s pod env | The browser bundle was already built with `undefined` / empty. `next start` does not re-bundle. |
| CI builds artifact without the var, ops adds it only in production host env | Same: client chunk never saw the value at compile time. |

**Correct flow:** Provide `NEXT_PUBLIC_RKSV_ENVIRONMENT` (and other `NEXT_PUBLIC_*`) **before** `next build` (or before the dev compiler picks up `.env.local`). For Docker: use `ARG`/`ENV` **before** `RUN npm run build`, or Compose `build.args`, or set env on the CI step that runs `npm run build`.

**This repo:** There is **no** `Dockerfile` or `docker-compose` for `frontend-admin` in-tree. **CI:** `.github/workflows/api-client-alignment.yml` runs `npm run build` with `NEXT_PUBLIC_RKSV_ENVIRONMENT` and `NEXT_PUBLIC_API_BASE_URL` set on that step (`.env.local` is gitignored). Full deployment notes and Docker examples: [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md).

### RKSV: `NEXT_PUBLIC_RKSV_ENVIRONMENT`

- **Allowed values:** `TEST`, `PROD` (any casing). **`NEXT_PUBLIC_*` variables must be present when `next dev` / `next build` runs** — not only at `next start` or container runtime. Restart dev or rebuild after changing them.
- **Why:** The RKSV hub (`/rksv`) shows an explicit FinanzOnline environment badge so operators don’t mix up test vs live FinanzOnline context (Austria Registrierkasse / RKSV).
- **If not set (local dev):** `next dev` still starts; `/rksv` shows **UNCONFIGURED** with fix hints and a one-time English `console.warn`.
- **Invalid value (e.g. `STAGING`):** UI shows **INVALID** with a sanitised display of what was read; `npm run build` also fails (`next.config.mjs`).

Code: `src/shared/config/rksvEnvironment.ts`.

3. Generate API Client:
   If Backend Swagger changes, ensure `orval.config.ts` points to `../backend/swagger.json` and run:
   ```bash
   npm run generate:api
   ```

## Scripts

- `npm run dev`: Start development server (localhost:3000)
- `npm run dev:clean`: Deletes `.next` then starts dev — use if `/rksv` stays **UNCONFIGURED** after fixing `.env.local` (stale inlined `NEXT_PUBLIC_*`).
- `npm run build`: Build for production (**requires** `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` or `PROD`; see above)
- `npm start`: Start production server
- `npm run lint`: Run ESLint
- `npm run generate:api`: Generate TypeScript client from Swagger

## Project Structure

- `src/app`: Next.js App Router pages
- `src/api/generated`: Orval generated hooks and models
- `src/features`: Feature-specific components
- `src/lib`: Shared providers (AntD, QueryClient)
- `src/theme`: Ant Design theme configuration

## Architecture

- **Auth**: Uses `/api/Auth/login` (Cookie-based or Token-based).
- **Data Fetching**: TanStack Query via Orval generated hooks.
- **UI**: Ant Design v5 with CSS-in-JS registry for SSR.
- **i18n**: Custom `I18nProvider` + JSON catalogs; runtime namespace’ler ve dosya adı eşlemesi için `src/i18n/README.md` kaynak kabul edilir.
