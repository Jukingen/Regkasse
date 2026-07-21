# Frontend Admin — structured logging

Canonical logging for `frontend-admin` (browser + Next.js Route Handlers).

## Goals

| Requirement | Implementation |
| ----------- | -------------- |
| Timestamp | ISO-8601 `time` on every record |
| Level | `debug` \| `info` \| `warn` \| `error` (+ pino `level` label on server) |
| Message | `msg` (English technical only — never i18n UI copy) |
| Context | `component`, `userId`, `sessionId`, `tenantId`, `route` when bound |
| No secrets | Redaction of password/token/JWT/authorization keys before emit |
| Aggregation | Sentry (errors) + stdout JSON (pino / beacon) for Datadog, Loki, ELK, CloudWatch |

## App API (client + shared)

Prefer:

```ts
import { logger } from '@/lib/logger';

logger.info('Loaded products', { component: 'ProductsPage' });
logger.child({ component: 'LoginForm' }).warn('CSRF bootstrap delayed');
logger.error(err, { code: 'EXPORT_FAILED' });
```

Ambient context (user / session) is bound by `LogContextBinder` inside `AppProviders`:

- `sessionId` — ephemeral `sessionStorage` id (not an auth token)
- `userId` / `tenantId` — from `/me` when authenticated

Do **not** use raw `console.*` in feature code (`eslint` `no-console`).

## Record shape (browser console)

```json
{
  "time": "2026-07-21T12:00:00.000Z",
  "level": "error",
  "levelNum": 50,
  "msg": "Export failed",
  "service": "frontend-admin",
  "env": "production",
  "component": "InvoiceList",
  "userId": "…",
  "sessionId": "…",
  "code": "EXPORT_FAILED"
}
```

### Environment gating

| Level | Development | Production (browser) |
| ----- | ----------- | -------------------- |
| debug / info / warn | Console | Silent |
| error | Console | Console + Sentry (if DSN) + optional log beacon |

## Server (pino)

Route Handlers use:

```ts
import { serverLogger } from '@/lib/logging/serverLogger';

serverLogger.info({ route: '/api/monitoring/logs' }, 'client_log');
```

- Package: **`pino`**
- Redact paths configured in `serverLogger` (password, tokens, auth headers, …)
- Level via `LOG_LEVEL` (default `info` in production, `debug` otherwise)
- JSON lines on **stdout** — scrape with your agent

## Aggregation options

### 1. Sentry (default for errors)

- Wired via `registerSentryErrorReporter()` after `Sentry.init`
- `logger.error` → `captureException` / `captureMessage` with redacted `extra`
- Set `NEXT_PUBLIC_SENTRY_DSN` at **build** time

### 2. Self-hosted stdout → Datadog / Grafana Loki / ELK / CloudWatch

1. Run FA in Docker / Node so container logs are collected.
2. Optionally enable browser beacons (build-time):

```env
NEXT_PUBLIC_LOG_BEACON=true
NEXT_PUBLIC_WEB_VITALS_BEACON=true
```

3. Endpoints:

| Endpoint | Purpose |
| -------- | ------- |
| `POST /api/monitoring/logs` | Structured client warn/error → pino stdout |
| `POST /api/monitoring/web-vitals` | Core Web Vitals → pino stdout |

4. Point the agent at container stdout. Example **Datadog** (Docker label / agent):

```yaml
# Illustrative — use your org’s Datadog/Fluent Bit/Filebeat config
labels:
  com.datadoghq.ad.logs: '[{"source":"nodejs","service":"frontend-admin"}]'
```

Example **Elastic Filebeat** input: container log driver or file prospector on the JSON lines (`service: frontend-admin`).

There is **no** mandatory hosted ELK/Datadog account in-repo — aggregation is ops wiring on top of structured stdout + optional Sentry.

## Redaction

Implemented in `src/lib/logging/redact.ts` (isomorphic) and pino `redact` on the server.

Blocked key examples: `password`, `token`, `accessToken`, `refreshToken`, `authorization`, `cookie`, `apiKey`, `licenseKey`, …

JWT-shaped strings → `[REDACTED_JWT]`.

## Key files

| Path | Role |
| ---- | ---- |
| `src/lib/logger.ts` | Public `logger` + `setLogContext` + `child()` |
| `src/shared/dev/technicalConsole.ts` | Emit + error reporter hook |
| `src/lib/logging/redact.ts` | Secret redaction |
| `src/lib/logging/logContext.ts` | Ambient context + session id |
| `src/lib/logging/serverLogger.ts` | pino (server-only) |
| `src/components/logging/LogContextBinder.tsx` | Bind user/session into context |
| `src/app/api/monitoring/logs/route.ts` | Log beacon ingest |

## Related

- [PERFORMANCE_MONITORING.md](./PERFORMANCE_MONITORING.md) — Web Vitals
- [README.md](../README.md) — Diagnostics / logging summary
- Sentry: `NEXT_PUBLIC_SENTRY_DSN`, `src/lib/monitoring/reportToSentry.ts`
