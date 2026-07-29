# Monitoring stack — Regkasse

Optional Prometheus / Grafana / Loki / Alertmanager / blackbox for production-oriented hosts.

| Doc | Topic |
|-----|--------|
| [`docs/MONITORING.md`](../docs/MONITORING.md) | Ops guide |
| [`docs/ALERTING.md`](../docs/ALERTING.md) | Alerts + Slack/Pager |
| [`docs/METRICS.md`](../docs/METRICS.md) | Metric catalog |
| FA in-app | `/admin/monitoring` + [`frontend-admin/docs/MONITORING.md`](../frontend-admin/docs/MONITORING.md) |

## Quick start

```bash
# App stack first (API metrics on :5184, optional UIs)
docker compose -f docker-compose.prod.yml --env-file .env.production up -d

# Observability
docker compose -f monitoring/docker-compose.monitoring.yml up -d
```

Open Grafana: `http://127.0.0.1:3002` (default user `admin` / set `GRAFANA_ADMIN_PASSWORD`).

Enable Slack routing: copy `alertmanager/alertmanager.yml.example` → `alertmanager.yml` and substitute webhook URLs (do not commit secrets).

## Layout

```text
monitoring/
  docker-compose.monitoring.yml
  prometheus/     scrape + alert rules
  alertmanager/   routing (null receiver by default)
  grafana/        provisioning + dashboards
  loki/           log store (14d retention)
  promtail/       Docker log shipper
  blackbox/       HTTP probes
```
