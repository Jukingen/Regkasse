# Frontend Admin — Performance Monitoring

**Owner:** FA maintainers  
**Scope:** `frontend-admin/` Core Web Vitals and page-load performance  
**Last updated:** 2026-07-21  
**Review cadence:** monthly (see §6)

This document describes how FA collects, visualizes, and alerts on performance metrics.

---

## 1. Architecture

```text
Browser (admin.regkasse.at)
  ├─ web-vitals (FCP, LCP, CLS, TTFB, INP)
  │    ├─→ Sentry Performance + warning events (primary)
  │    └─→ POST /api/monitoring/web-vitals (optional self-hosted beacon)
  └─ @vercel/speed-insights (when deployed on Vercel)
       └─→ Vercel Speed Insights dashboard
```

| Sink | When to use |
| ---- | ----------- |
| **Sentry Performance** | Default for all environments with `NEXT_PUBLIC_SENTRY_DSN` (Docker / nginx / Vercel) |
| **Vercel Speed Insights** | Extra dashboard when FA is hosted on Vercel |
| **Beacon → stdout JSON** | Self-hosted: scrape with Grafana Loki / Datadog Logs / CloudWatch |

Sentry `browserTracingIntegration` (see `src/instrumentation-client.ts`) already records navigation/fetch spans. The custom reporter adds explicit vital measurements and **degradation warnings** for alerting.

---

## 2. Metrics

| Metric | Meaning | Good budget | Alert when |
| ------ | ------- | ----------- | ---------- |
| **FCP** | First Contentful Paint | ≤ 1.8 s | > 1.8 s |
| **LCP** | Largest Contentful Paint | ≤ 2.5 s | **> 2.5 s** (primary SLO) |
| **CLS** | Cumulative Layout Shift | ≤ 0.1 | > 0.1 |
| **TTFB** | Time to First Byte | ≤ 800 ms | > 800 ms |
| **INP** | Interaction to Next Paint | ≤ 200 ms | > 200 ms |

### TTI (Time to Interactive)

**Not collected.** Google retired TTI from Core Web Vitals in favor of **INP**. FA treats INP as the interactivity signal. Do not reintroduce lab-only TTI polyfills unless product explicitly requires them.

Budgets live in code: [`src/lib/monitoring/webVitalsBudgets.ts`](../src/lib/monitoring/webVitalsBudgets.ts).

---

## 3. Code map

| File | Role |
| ---- | ---- |
| `src/components/monitoring/PerformanceMonitoring.tsx` | Root shell: Speed Insights + Web Vitals reporter |
| `src/components/monitoring/WebVitalsReporter.tsx` | `web-vitals` subscription |
| `src/lib/monitoring/reportWebVitalToSentry.ts` | Measurements + degradation `captureMessage` |
| `src/lib/monitoring/reportWebVitalBeacon.ts` | Optional same-origin beacon |
| `src/app/api/monitoring/web-vitals/route.ts` | Beacon sink (structured `console.info` JSON) |
| `src/app/layout.tsx` | Mounts `<PerformanceMonitoring />` |
| `next.config.mjs` | CSP `connect-src` includes `vitals.vercel-insights.com` |

---

## 4. Environment variables

All `NEXT_PUBLIC_*` values are **build-time** (same rule as RKSV / Sentry DSN).

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `NEXT_PUBLIC_SENTRY_DSN` | unset | Enables Sentry (errors + performance + vital alerts) |
| `NEXT_PUBLIC_SENTRY_ENVIRONMENT` | `NODE_ENV` | Sentry environment label |
| `NEXT_PUBLIC_SENTRY_RELEASE` | unset | Release for regressions by version |
| `NEXT_PUBLIC_SPEED_INSIGHTS` | on in `production` | Set `false` to disable Vercel Speed Insights |
| `NEXT_PUBLIC_WEB_VITALS_BEACON` | unset | Set `true` to POST vitals to `/api/monitoring/web-vitals` |

Local/dev: leave Sentry DSN unset; vitals still log via `technicalConsole.debug` in development.

---

## 5. Dashboards

### 5.1 Sentry (primary — all hosts)

1. Open the FA Sentry project → **Performance** / **Web Vitals**.
2. Filter by `environment:production` and release when set.
3. Useful views:
   - LCP / CLS / INP p75 over time
   - Slow transactions (App Router navigations)
   - Issues tagged `source:web-vitals` (budget breaches)

### 5.2 Vercel Speed Insights (Vercel only)

1. Deploy FA with Vercel (`vercel.json` present).
2. Project → **Speed Insights**.
3. Review real-user Core Web Vitals by route.

No extra Vercel project token is required for the default `@vercel/speed-insights` integration on Vercel-hosted apps.

### 5.3 Grafana / Datadog (optional self-hosted)

Enable the beacon at build time:

```env
NEXT_PUBLIC_WEB_VITALS_BEACON=true
```

Each vital becomes one stdout line:

```json
{"type":"web_vital","name":"LCP","value":3120,"rating":"poor","route":"/payments","degraded":true,"ts":"..."}
```

**Grafana Loki example (LogQL):**

```logql
{app="frontend-admin"} |= "web_vital" | json | name="LCP" | value > 2500
```

Panel ideas:

- p75 `value` by `name` and `route` (unwrap JSON)
- count of `degraded=true` per hour
- top routes by poor LCP

**Datadog Logs:** facet on `@type:web_vital`, `@name`, `@route`, `@degraded`; create a timeseries of `@value` for `name:LCP`.

Wire container/process logs from the FA Node process (Docker `npm start` or nginx→Node) into your log stack — no separate metrics agent required for the MVP.

---

## 6. Alerts

### 6.1 Sentry alert (recommended)

Create an **Issue Alert** (or Metric Alert if on a plan that supports it):

| Field | Value |
| ----- | ----- |
| Condition | Event message contains `Web vital degraded: LCP` **or** tag `vital` equals `LCP` |
| Filter | `environment:production`, tag `source:web-vitals` |
| Action | Slack / email / PagerDuty |
| Threshold | e.g. **≥ 10 events in 1 hour** (tune to traffic) |

Also add sibling alerts for:

- `Web vital degraded: CLS`
- `Web vital degraded: INP`
- `Web vital degraded: TTFB` (often origin/CDN — escalate to infra)

Code already emits these warnings when values exceed budgets (`reportWebVitalToSentry.ts`).

### 6.2 Grafana / Datadog

Alert when Loki/Datadog query shows LCP `degraded=true` rate above baseline (e.g. >5% of LCP samples in 15m).

### 6.3 Vercel

Use Vercel Speed Insights notifications if available on the team plan; otherwise rely on Sentry.

---

## 7. Monthly performance review

**Cadence:** first Monday of each month (or sprint nearest to month start).  
**Attendees:** FA maintainer + optional ops.  
**Duration:** 30 minutes.

### Agenda checklist

1. **Trend:** Sentry (or Vercel) p75 LCP / INP / CLS vs last month.
2. **Breaches:** Top routes by `Web vital degraded:*` count.
3. **Regressions:** Correlate with releases (`NEXT_PUBLIC_SENTRY_RELEASE`).
4. **Tech debt:** Pull open performance items from [`TECHNICAL_DEBT.md`](../TECHNICAL_DEBT.md) (e.g. FA-TD-002/003/004).
5. **Actions:** File follow-ups (lazy load, virtual tables, image optimization, API TTFB).
6. **Update** the “Last reviewed” line below.

| Month | Reviewed by | LCP p75 | Notes / actions |
| ----- | ----------- | ------- | --------------- |
| 2026-07 | — | — | Process established; first review pending |
| 2026-08 | | | |
| 2026-09 | | | |

Add calendar reminder: **“FA Core Web Vitals review”** monthly.

---

## 8. Local verification

```bash
cd frontend-admin
npm run test -- src/lib/monitoring/__tests__/webVitalsBudgets.test.ts
npm run test -- src/__tests__/nextConfig.security.test.ts
```

Manual (dev):

1. `npm run dev` — open DevTools console; `[web-vitals]` debug lines appear on load/interaction.
2. Production-like: build with Sentry DSN + open Performance in Sentry after browsing key routes (`/`, `/payments`, `/admin/tenants`).

---

## 9. Privacy

- Routes are **pathname only** (query/hash stripped).
- No user ids, tokens, or form fields in vital payloads.
- Sentry `sendDefaultPii: false` remains in force.
