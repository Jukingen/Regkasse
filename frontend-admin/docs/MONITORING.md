# Frontend Admin — Monitoring

**Owner:** FA maintainers  
**Scope:** Uptime, error rates, API latency, process health, alerts, dashboards  
**Last updated:** 2026-07-21  

Related: [PERFORMANCE_MONITORING.md](./PERFORMANCE_MONITORING.md) (Core Web Vitals), [LOGGING.md](./LOGGING.md) (structured logs).

---

## 1. Architecture

```text
Browser (admin.regkasse.at)
  ├─ Sentry browser SDK (errors + tracing + optional Metrics)
  ├─ axios interceptors → reportApiMetric (latency / error rate)
  ├─ web-vitals → Sentry + optional beacon
  └─ optional beacons → /api/monitoring/{metrics,logs,web-vitals}

Next.js FA process
  ├─ GET /health                         (uptime probe)
  ├─ GET /api/monitoring/health          (detailed health JSON)
  └─ pino stdout                         (Datadog / Loki / ELK)

External
  ├─ UptimeRobot / Pingdom / k8s         → /health
  ├─ Sentry dashboards + alerts
  └─ Grafana (import monitoring/grafana-fa-dashboard.json)
```

**Datadog APM agent is not embedded in the FA bundle.** Use Sentry as the default SaaS sink; scrape stdout JSON for Datadog Logs / Grafana when self-hosting.

---

## 2. What we monitor

| Area | How | Threshold / note |
| ---- | --- | ---------------- |
| **Uptime** | `GET /health`, `GET /api/monitoring/health` | HTTP 200 + `status: ok` |
| **Client errors** | Sentry (`logger.error`, unhandled, axios 5xx) | Issue volume alerts |
| **API error rate** | Rolling 5‑min window in-tab + Sentry message | **> 5%** (min 20 samples) |
| **API response time** | axios duration → Sentry + dashboard | **> 1000 ms** per call |
| **Server health** | FA Node health JSON (uptime, memory, Sentry configured) | Probe from k8s / ops |
| **Web Vitals** | See PERFORMANCE_MONITORING.md | LCP > 2.5 s primary |

Thresholds live in code: [`src/lib/monitoring/thresholds.ts`](../src/lib/monitoring/thresholds.ts).

---

## 3. Code map

| Path | Role |
| ---- | ---- |
| `src/lib/axios.ts` | Marks request start; records metrics on response/error |
| `src/lib/monitoring/reportApiMetric.ts` | Store + Sentry + optional metrics beacon |
| `src/lib/monitoring/apiMetricsStore.ts` | In-tab rolling samples for `/admin/monitoring` |
| `src/app/health/route.ts` | Public uptime probe |
| `src/app/api/monitoring/health/route.ts` | Detailed health |
| `src/app/api/monitoring/metrics/route.ts` | API metric beacon → pino |
| `src/app/(protected)/admin/monitoring/page.tsx` | Super Admin dashboard |
| `src/proxy.ts` | Allows unauthenticated `/api/monitoring/{health,web-vitals,logs,metrics}` |
| `monitoring/grafana-fa-dashboard.json` | Grafana import |
| `monitoring/sentry-alert-recipes.md` | Alert setup checklist |

---

## 4. Environment variables

| Variable | Purpose |
| -------- | ------- |
| `NEXT_PUBLIC_SENTRY_DSN` | Enables Sentry errors + performance (build-time) |
| `NEXT_PUBLIC_SENTRY_ENVIRONMENT` | Sentry environment label |
| `NEXT_PUBLIC_METRICS_BEACON=true` | POST API metrics to `/api/monitoring/metrics` |
| `NEXT_PUBLIC_WEB_VITALS_BEACON=true` | Web Vitals stdout beacon |
| `NEXT_PUBLIC_LOG_BEACON=true` | Structured log beacon |
| `LOG_LEVEL` | pino level on Route Handlers |

---

## 5. Dashboards

### 5.1 In-app (Super Admin)

Open **`/admin/monitoring`** (`system.critical`):

- Live API error rate vs 5% threshold
- p50 / p95 / p99 latency vs 1s threshold
- FA health probe result
- Recent sanitized API calls (this browser tab)

### 5.2 Sentry (primary SaaS)

1. **Issues** — filter `source:api-metrics`, `source:axios`, `source:logger`
2. **Performance** — navigations + fetch/XHR spans (`browserTracingIntegration`)
3. **Web Vitals** — see PERFORMANCE_MONITORING.md

### 5.3 Grafana (self-hosted)

1. Ship FA container stdout to Loki (or Datadog Logs → Grafana).
2. Import [`monitoring/grafana-fa-dashboard.json`](../monitoring/grafana-fa-dashboard.json).
3. Adjust LogQL `service` label to match your scrape config.

### 5.4 Vercel Speed Insights

Optional when hosted on Vercel (page performance only).

---

## 6. Alerts

| Alert | Condition | Where to configure |
| ----- | --------- | ------------------ |
| API error rate | > **5%** in rolling window | Sentry Issue Alert — see `monitoring/sentry-alert-recipes.md` |
| API latency | call **> 1s** | Sentry Issue Alert + Performance |
| Uptime | `/health` failing | UptimeRobot / Pingdom / k8s Probe |
| LCP degraded | > 2.5 s | Existing web-vitals alerts |

Code already emits rate-limited Sentry messages for error-rate and slow-call breaches when DSN is active.

---

## 7. Uptime checklist

```bash
curl -sS https://admin.regkasse.at/health
curl -sS https://admin.regkasse.at/api/monitoring/health
```

Expect JSON with `"status":"ok"`. Wire the first URL into your uptime provider (1‑minute interval recommended).

---

## 8. Privacy

- Metric paths strip query strings and UUIDs (`sanitizeApiPath`).
- No passwords, tokens, or request bodies in metrics.
- Sentry `sendDefaultPii: false`.

---

## 9. Local verification

```bash
cd frontend-admin
npm run test -- src/lib/monitoring/__tests__/
curl -sS http://localhost:3000/health
curl -sS http://localhost:3000/api/monitoring/health
```

Open `/admin/monitoring` as Super Admin after navigating a few API-backed pages.
