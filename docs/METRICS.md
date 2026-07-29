# Metrics reference — Regkasse

Prometheus metrics exposed by the API (`GET /metrics`) and related gauges.

**Last updated:** 2026-07-29

| Related | Link |
|---------|------|
| Monitoring | [`MONITORING.md`](MONITORING.md) |
| Detailed release notes | [`release/CORE_METRICS_PROMETHEUS.md`](release/CORE_METRICS_PROMETHEUS.md) |
| Config | `Monitoring` section in `appsettings*.json` / env |
| Code | `MetricsMiddleware`, `CoreMetrics`, `*MetricsService`, `TseMetricsService` |

---

## Endpoint

| Item | Value |
|------|--------|
| Path | `/metrics` (override: `Monitoring:MetricsEndpoint`) |
| Format | Prometheus text exposition |
| Auth | Anonymous — **restrict** at reverse proxy / private network |
| Enable | `Monitoring:Enabled=true` and `Monitoring:Prometheus:Enabled=true` |

Suggested scrape interval: 15s (`Monitoring:Prometheus:ScrapeIntervalSeconds`).

---

## HTTP / API

| Metric | Type | Labels | Meaning |
|--------|------|--------|---------|
| `api_requests_total` | Counter | `method`, `endpoint`, `status_code` | Request count |
| `api_request_duration_ms` | Histogram | `method`, `endpoint` | Latency (ms) |
| `api_errors_total` | Counter | `method`, `endpoint`, `error_type` | Errors |
| `api_active_requests` | Gauge | — | In-flight requests |

**SLO helpers**

```promql
sum(rate(api_errors_total[5m])) / clamp_min(sum(rate(api_requests_total[5m])), 0.001)
histogram_quantile(0.95, sum(rate(api_request_duration_ms_bucket[5m])) by (le))
```

---

## Database & cache

| Metric | Type | Labels | Meaning |
|--------|------|--------|---------|
| `db_query_duration_ms` | Histogram | `query_type` | EF command duration |
| `db_queries_total` | Counter | `query_type` | Query count |
| `db_connections_active` | Gauge | — | Open connections |
| `cache_hits_total` / `cache_misses_total` | Counter | — | Cache |
| `cache_size_bytes` | Gauge | — | Approx size |
| `cache_hit_ratio` | Gauge | — | hits/(hits+misses) |

---

## Business

| Metric | Type | Meaning |
|--------|------|---------|
| `tenants_active_total` | Gauge | Active tenants |
| `revenue_total_eur` | Gauge | Sum of POS totals (indicative) |
| `orders_active_total` | Gauge | Active online orders |
| `orders_created_total` | Counter | Online orders created |
| `users_registered_total` | Gauge | Registered users |

Refreshed by `BusinessMetricsRefreshHostedService` (≈5 minutes).

---

## Fiscal / offline / FON

| Metric | Type | Meaning |
|--------|------|---------|
| `finanzonline_submit_total` | Counter | FON attempts |
| `finanzonline_submit_failed_total` | Counter | FON failures (`failure_kind`) |
| `replay_total` / `replay_failed_total` / `replay_duplicate_total` | Counter | Offline replay |
| `advisory_lock_wait_seconds` | Histogram | Lock wait |
| `payload_hash_mismatch_total` | Counter | Hash repair / mismatch |
| `tse_devices_total` | Gauge | Devices by health status |
| `tse_devices_by_provider` | Gauge | By provider |
| `tse_device_health_score` | Gauge | Per device 0–100 |
| `tse_average_health_score` | Gauge | Fleet average |
| `tse_failover_active` | Gauge | Active failovers |
| `tse_primary_devices` / `tse_backup_devices` | Gauge | Role counts |
| `tse_fleet_status` | Gauge | Fleet health signal |
| `tse_health_check_staleness_seconds` | Gauge | Staleness |

Receipt / signature volume is primarily reflected via payment/replay and TSE device health — treat TSE + FON metrics as the fiscal pulse, not a second ledger.

---

## Backup / restore / ops

Backup and restore orchestrators expose Prometheus counters for gate outcomes and run totals (`PrometheusBackupOrchestratorMetrics`, restore/DR equivalents). Use FA backup dashboards for operator UX; scrape `/metrics` for SRE graphs.

---

## FA client metrics (not Prometheus)

| Signal | Sink |
|--------|------|
| Axios latency / error rate | Sentry + optional `/api/monitoring/metrics` beacon |
| Web Vitals | Sentry + beacon |
| Rolling in-tab summary | `/admin/monitoring` |

Thresholds: `frontend-admin/src/lib/monitoring/thresholds.ts` (error rate 5%, latency 1000ms).

---

## Blackbox / uptime

Prometheus job `blackbox-http` exports `probe_success`, `probe_duration_seconds`, etc. for configured health URLs (see `monitoring/prometheus/prometheus.yml`).

---

## Adding a metric

1. Prefer an existing `*MetricsService` / `ICoreMetrics` method.  
2. Use stable **snake_case** names and low-cardinality labels (no raw tenant UUIDs / URLs with IDs).  
3. Document here + alert rule if page-worthy.  
4. Never put PII or fiscal secrets in label values.
