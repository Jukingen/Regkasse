# Monitoring guide — Regkasse

Production observability: health probes, metrics, logs, dashboards, and how they fit together.

**Last updated:** 2026-07-29

| Related | Link |
|---------|------|
| Alerting | [`ALERTING.md`](ALERTING.md) |
| Metrics catalog | [`METRICS.md`](METRICS.md) |
| Stack files | [`../monitoring/README.md`](../monitoring/README.md) |
| FA package | [`../frontend-admin/docs/MONITORING.md`](../frontend-admin/docs/MONITORING.md) |
| Prometheus release notes | [`release/CORE_METRICS_PROMETHEUS.md`](release/CORE_METRICS_PROMETHEUS.md) |
| Docker prod | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| In-app dashboard | FA `/admin/monitoring` (`system.critical`) |

---

## Architecture

```text
  Uptime / blackbox ──► /api/health/live|ready · FA /health · POS /healthz · Sites /health
  Prometheus ─────────► scrape API /metrics (+ node/cadvisor optional)
  Promtail ───────────► Docker json-file logs ──► Loki (14d)
  Grafana ────────────► Prometheus + Loki dashboards
  Alertmanager ───────► Slack / on-call (see ALERTING.md)
  Sentry (FA) ────────► browser errors / Web Vitals / API client latency
  Activity feed ──────► FA bell / email / webhooks (business events)
```

Do **not** invent a second metrics path — extend `Monitoring:*` + Prometheus exposition and FA Sentry beacons.

---

## Health checks

| Surface | Endpoint | Purpose |
|---------|----------|---------|
| API liveness | `GET /api/health/live` · `GET /health/live` | Process up (no DB) |
| API readiness | `GET /api/health/ready` · `GET /health/ready` | DB + TSE production lock + FON simulation gate |
| API deps | `GET /api/health` | DB + cached TSE/NTP snapshot |
| Migrations | `GET /api/health/migrations` | EF applied vs pending |
| TSE / FON | `GET /health/tse/mode` · `/health/finanzonline` | Fiscal posture |
| Admin | `GET /health` · `GET /api/monitoring/health` | FA uptime / detail |
| POS web | `GET /healthz` | nginx static export |
| Sites | `GET /health` | Next.js storefront |

**Docker HEALTHCHECK** uses these paths (see Dockerfiles + `docker-compose.prod.yml`).

Smoke: [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md).

---

## Logging

| Layer | Format | Levels | Sink |
|-------|--------|--------|------|
| API (Production) | ASP.NET Core **JSON console** (`Logging:Console:FormatterName=json`) | Information+ for app; Warning for framework; JWT success is Debug | Container stdout → Promtail → Loki |
| API (Development) | **Readable** console (`FormatterName=readable`) | Same levels; scopes include Tenant/User | Local console |
| FA | **pino** JSON on Route Handlers | `LOG_LEVEL` | stdout / beacons |
| Compose | `json-file` rotation (`LOG_MAX_SIZE` / `LOG_MAX_FILES`) | — | Host + Promtail |

Request enrichment: after auth, `RequestLoggingScopeMiddleware` adds unmasked `Tenant` / `User` / `Role` / `CorrelationId` scopes. Failures from `MetricsMiddleware` include method, path+query, user, and tenant. Slow requests (≥ `Monitoring:SlowRequestThresholdMs`) log Warning.

**Retention**

| Store | Default |
|-------|---------|
| Loki | 14 days (`retention_period: 336h`) |
| Prometheus TSDB | 15 days (`PROMETHEUS_RETENTION`) |
| Docker json-file | size-based (`10m` × 5 files typical) |
| Audit / fiscal DB | legal retention (7y) — **not** replaced by Loki |

Never log passwords, voucher codes, raw PEMs, or unredacted card data ([`CONFIGURATION.md`](../backend/CONFIGURATION.md) § Logging).

---

## Metrics & performance

- API: `GET /metrics` (Prometheus text) when `Monitoring:Enabled` + `Monitoring:Prometheus:Enabled`
- Middleware: request count, errors, duration, active requests
- Fiscal: FinanzOnline, offline replay, TSE fleet gauges (`TseMetricsService`)
- Business gauges: tenants, revenue, orders, users (refreshed periodically)
- FA: Sentry + `/admin/monitoring` client rolling window

Full catalog: [`METRICS.md`](METRICS.md).

Resource usage (CPU/memory/disk): scrape **cAdvisor** / **node_exporter** on the host if needed (not bundled); Grafana panels can be added later.

---

## Dashboards

| Dashboard | Where |
|-----------|--------|
| **Regkasse API / Fiscal** | Grafana auto-provisioned (`monitoring/grafana/dashboards/regkasse-api.json`) |
| **Frontend Admin** | Same Grafana folder (`regkasse-fa.json`) + Loki queries |
| **In-app** | `https://admin.regkasse.at/admin/monitoring` |

---

## Quick start (host)

```bash
# 1) App
docker compose -f docker-compose.prod.yml --env-file .env.production --profile admin up -d

# 2) Observability
docker compose -f monitoring/docker-compose.monitoring.yml up -d

# 3) Probes
curl -fsS http://127.0.0.1:5184/api/health/live
curl -fsS http://127.0.0.1:5184/metrics | head
curl -fsS http://127.0.0.1:3000/health
open http://127.0.0.1:3002   # Grafana
```

Set `GRAFANA_ADMIN_PASSWORD` in the environment before first Grafana start.

---

## Security notes

- Bind Prometheus/Grafana/Loki/Alertmanager to **127.0.0.1** (Compose defaults); put SSO/VPN in front if exposed.
- `/metrics` is anonymous — restrict via reverse proxy / private network in real prod.
- Alertmanager starts with a **null** receiver until you install a secret-backed config from `alertmanager.yml.example`.

---

## Checklist

- [ ] Health probes green for API + UIs you run  
- [ ] `/metrics` scrapes in Prometheus targets  
- [ ] Grafana dashboards show data  
- [ ] Loki shows container logs  
- [ ] Alerts routed (see [`ALERTING.md`](ALERTING.md))  
- [ ] FA Sentry DSN set for Production builds  
- [ ] On-call knows fiscal alerts (`RegkasseTseFleetUnhealthy`, `RegkasseFinanzOnlineFailures`)  
