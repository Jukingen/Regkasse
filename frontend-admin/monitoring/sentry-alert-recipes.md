# Sentry alert recipes — Frontend Admin

Configure these in the FA Sentry project (Issues → Alerts or Metrics → Alerts).
Thresholds match `src/lib/monitoring/thresholds.ts`.

## 1. API error rate > 5%

Code emits (rate-limited) when the **browser rolling window** exceeds 5%:

- Message: `API error rate exceeded 5%`
- Tags: `source:api-metrics`, `alert:error_rate`

**Sentry Issue Alert**

| Field | Value |
| ----- | ----- |
| When | An event is captured |
| If | Message contains `API error rate exceeded` AND tag `source` equals `api-metrics` |
| Then | Notify Slack / email |
| Filter | `environment:production` |
| Action interval | ≥ 5 minutes |

Prefer a **Metric Alert** on `fa.api.errors` / `fa.api.requests` when your Sentry plan exposes custom metrics (SDK already increments when Metrics API is available).

## 2. API response time > 1s

Code emits when a single call exceeds 1000 ms:

- Message: `API response time exceeded 1000ms`
- Tags: `source:api-metrics`, `alert:response_time`

| Field | Value |
| ----- | ----- |
| When | An event is captured |
| If | Message contains `API response time exceeded` |
| Then | Notify Slack / email |
| Filter | `environment:production` |
| Frequency | e.g. ≥ 20 events / 1 hour (tune to traffic) |

Also review **Performance** → transactions / axios spans with duration > 1s (`browserTracingIntegration`).

## 3. Client / unhandled errors

| Field | Value |
| ----- | ----- |
| When | Number of events in an issue is more than `N` (e.g. 10) |
| Filter | `environment:production`, level `error`/`fatal`, exclude tag `source:web-vitals` |
| Then | Pager / Slack |

Axios 5xx already reported via `reportAxiosErrorToSentry` (`source:axios`).

## 4. Uptime (external)

Sentry does not replace uptime probes. Point UptimeRobot / Pingdom / k8s at:

- `GET https://admin.regkasse.at/health` → expect HTTP 200 + `"status":"ok"`
- Optional deep: `GET https://admin.regkasse.at/api/monitoring/health`

Alert when consecutive failures ≥ 2 in 2 minutes.

## 5. Web Vitals (existing)

See `docs/PERFORMANCE_MONITORING.md` — message `Web vital degraded: LCP`, tag `source:web-vitals`.
