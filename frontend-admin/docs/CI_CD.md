# Frontend Admin CI/CD

Canonical pipeline docs for `frontend-admin` (Next.js 16 admin). GitLab CI is **not** used in this repo; all automation is **GitHub Actions**.

## Pipeline overview

```text
PR / push (frontend-admin/**)
        │
        ▼
┌───────────────────────────┐
│  Frontend Admin CI        │  .github/workflows/frontend-admin-ci.yml
│  1. npm run lint          │
│  2. npm run typecheck     │
│  3. npm run test          │
│  4. npm run build         │
│  5. npm run test:e2e      │  (after quality job)
│  + Slack on failure       │  (optional secret)
└───────────────────────────┘

push main CI green (`workflow_run`)  OR  workflow_dispatch
        │
        ▼
┌───────────────────────────┐
│  Frontend Admin Deploy    │  .github/workflows/frontend-admin-deploy.yml
│  Build & push GHCR image  │
│  → staging environment    │  after CI success on main / dispatch staging
│  → production environment │  dispatch only + manual approval
└───────────────────────────┘
```

Related (still run separately when their paths match):

| Workflow | Role |
| -------- | ---- |
| `api-client-alignment.yml` | Orval / OpenAPI drift + admin build smoke |
| `localization-validation.yml` | i18n strict gates |
| `api-contract-tests.yml` | Contract tests that install FA deps |
| `frontend-admin-e2e.yml` | Manual / reusable Playwright only (`workflow_dispatch` / `workflow_call`) |

## Quality gate commands (local parity)

From `frontend-admin/`:

```bash
npm ci
npm run lint
npm run typecheck
npm run test
npm run build          # needs NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST|PROD
npm run test:e2e       # after build; Playwright Chromium installed
```

CI sets:

- `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`
- `NEXT_PUBLIC_API_BASE_URL=http://127.0.0.1:5184`

See [DEPLOYMENT_BUILD_TIME_ENV.md](./DEPLOYMENT_BUILD_TIME_ENV.md).

## Caching

| Cache | Purpose |
| ----- | ------- |
| `actions/setup-node` `cache: npm` | npm download cache (`~/.npm`) |
| `actions/cache` → `frontend-admin/node_modules` | Skip `npm ci` on lockfile hit |
| `actions/cache` → `frontend-admin/.next/cache` | Faster `next build` |
| Docker Buildx `type=gha` | Faster image builds in Deploy |

Cache key includes Node **22** and `package-lock.json` hash. After lockfile changes, the first run repopulates caches.

## Deploy: staging vs production

| Target | Trigger | GitHub Environment | Image tags (GHCR) |
| ------ | ------- | ------------------ | ----------------- |
| **Staging** | After **Frontend Admin CI** succeeds on `main`/`master` (`workflow_run`), or Actions → Deploy → `staging` | `frontend-admin-staging` | `:staging`, `:staging-<sha7>`, `:sha-<full>` |
| **Production** | Actions → **Frontend Admin Deploy** → `production` only | `frontend-admin-production` (**require reviewers**) | `:production`, `:production-<sha7>`, `:sha-<full>` |

Image name: `ghcr.io/<owner-lowercase>/regkasse-frontend-admin`.

### One-time GitHub setup

1. **Environments** (Settings → Environments):
   - Create `frontend-admin-staging` (optional protection).
   - Create `frontend-admin-production` → **Required reviewers** (manual approval gate).
2. **Variables** (repo or environment):

   | Variable | Example |
   | -------- | ------- |
   | `FA_STAGING_API_BASE_URL` | Staging API origin |
   | `FA_STAGING_RKSV_ENVIRONMENT` | `TEST` |
   | `FA_PRODUCTION_API_BASE_URL` | `https://api.regkasse.at` |
   | `FA_PRODUCTION_RKSV_ENVIRONMENT` | `PROD` |
   | `FA_STAGING_URL` / `FA_PRODUCTION_URL` | Environment URL badges |
   | `FA_SENTRY_DSN` | Optional public DSN baked at build |

3. **Secrets**:

   | Secret | Purpose |
   | ------ | ------- |
   | `FA_STAGING_DEPLOY_WEBHOOK_URL` | Optional POST after staging image push |
   | `FA_PRODUCTION_DEPLOY_WEBHOOK_URL` | Optional POST after production approval |
   | `SLACK_WEBHOOK_URL` | Optional Slack Incoming Webhook on CI/Deploy failure |

Without deploy webhooks, the workflow still **pushes the image**; ops pull on the host:

```bash
docker pull ghcr.io/<owner>/regkasse-frontend-admin:staging
# or production tag after approval
```

Compose locally: see root `frontend-admin/docker-compose.yml` and [README Docker](../README.md#docker).

### Production approval flow

1. Merge to `main` → **Frontend Admin CI** green → Deploy builds **staging** image (RKSV usually `TEST`).
2. Smoke staging (`/login`, RKSV badge, one Mandanten path).
3. Actions → **Frontend Admin Deploy** → Run workflow → target **production**.
4. GitHub pauses on Environment **frontend-admin-production** until a required reviewer approves.
5. Image push (with `PROD` build-args) + optional production webhook.

**Coupled releases:** when FA needs new API routes, ship backend first (or same window). See [`docs/ADMIN_FA_DEPLOY.md`](../../docs/ADMIN_FA_DEPLOY.md).

## Failure notifications

| Channel | Behavior |
| ------- | -------- |
| **GitHub** | Watchers / org notification settings receive Actions failure emails by default |
| **Slack** | If `SLACK_WEBHOOK_URL` is set, CI and Deploy post a short message with run link; if unset, the notify job skips cleanly |

No in-repo email SMTP is configured (avoid secrets in workflows). Prefer Slack + GitHub notifications.

## Vercel (optional alternate)

`vercel.json` supports framework deploy. Prefer one primary path (Docker/GHCR **or** Vercel) to avoid drift. Vercel Project → Root Directory = `frontend-admin`; do **not** set Output Directory to `.next`.

## Troubleshooting

| Symptom | Check |
| ------- | ----- |
| Build fails `RKSV_ENVIRONMENT` | `NEXT_PUBLIC_RKSV_ENVIRONMENT` must be `TEST` or `PROD` at build time |
| E2E Chromium missing | CI uses `npx playwright install --with-deps chromium` |
| Cache miss every run | Lockfile changed; expected once |
| Deploy skipped production on push | By design — production is dispatch + approval only |
| Package push 403 | Workflow needs `packages: write`; package visibility under Packages |

## Related

- [DEPLOYMENT_BUILD_TIME_ENV.md](./DEPLOYMENT_BUILD_TIME_ENV.md)
- [README.md](../README.md) — CI/CD section
- [`docs/ADMIN_FA_DEPLOY.md`](../../docs/ADMIN_FA_DEPLOY.md)
- [ONBOARDING.md](../ONBOARDING.md) § Deploy
