# Admin panel (FA) + API — coupled releases

Some admin features call **new backend routes** that older API builds do not expose. Deploy **backend and `frontend-admin` in the same release window** (same maintenance window, ideally same pipeline run) when the table below applies.

## RKSV dashboard batch endpoints (Phase 4–5)

| Frontend consumer | New API route | If API is old |
|-------------------|---------------|---------------|
| `useAdminMonatsbelegOverview` | `GET /api/rksv/monatsbeleg/status-overview` | 404 → Monatsbeleg card empty / error |
| `useRksvReminderOverview` | `GET /api/rksv/reminder/status-overview` | 404 → RKSV reminder card empty / error |

Per-register routes remain available for POS/tools; the admin dashboard no longer depends on N parallel `…/status/{cashRegisterId}` calls for these two cards.

## Recommended deploy order

1. **Backend** — publish API with both overview routes (no breaking change to existing per-register endpoints).
2. **Frontend-admin** — build with `NEXT_PUBLIC_*` set at **build time** (see `frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`).
3. Smoke on a tenant with ≥1 cash register: open `/dashboard`, Network tab → one `monatsbeleg/status-overview` and one `reminder/status-overview` (not N× per register).

Rolling **frontend before backend** briefly shows dashboard RKSV errors until the API is updated; rolling **backend only** is safe for older FA bundles (they keep using per-register calls until you ship the new admin build).

## OpenAPI / Orval (overview endpoints)

After backend RKSV overview routes change:

```bash
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/validate-critical-openapi-paths.mjs
node scripts/verify-api-client.mjs
```

Dashboard hooks use generated `getApiRksvMonatsbelegStatusOverview` / `getApiRksvReminderStatusOverview` from `src/api/generated/rksv/rksv.ts`.

## Bundle analysis (local)

```bash
cd frontend-admin
npm install
npm run analyze
```

Opens webpack bundle reports in the browser (`ANALYZE=true` + `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`).

## CI reference

- **Quality gate:** `.github/workflows/frontend-admin-ci.yml` — `lint`, `typecheck`, `test`, `build`, `test:e2e` (with `node_modules` / Next cache).
- **Deploy:** `.github/workflows/frontend-admin-deploy.yml` — GHCR image; staging on `main`; production via `workflow_dispatch` + GitHub Environment approval.
- **OpenAPI smoke:** `.github/workflows/api-client-alignment.yml` (`NEXT_PUBLIC_RKSV_ENVIRONMENT`, `npm run build`).
- Full FA pipeline docs: `frontend-admin/docs/CI_CD.md`.
- Commit updated `backend/swagger.json` whenever API routes/DTOs change (regenerate; do not hand-edit).

## Related docs

- `frontend-admin/docs/CI_CD.md` — CI/CD process (caching, staging/prod, Slack)
- `frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md` — `NEXT_PUBLIC_*` at image/CI/Vercel build time
- `frontend-admin/README.md` — Docker, Vercel (`vercel.json`), Nginx (`nginx.conf`), CI/CD summary
- `docs/MULTI_TENANT.md` — tenant host / `X-Tenant-Id` (Development only)
