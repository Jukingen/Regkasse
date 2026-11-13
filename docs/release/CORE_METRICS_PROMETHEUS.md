# Core Metrics (Prometheus / Grafana)

Core fiscal and replay metrics are served in Prometheus format from the `/metrics` endpoint. Prometheus can scrape them and Grafana can display them as a dashboard.

**Canonical catalog (English):** [`../METRICS.md`](../METRICS.md) · **Ops stack:** [`../MONITORING.md`](../MONITORING.md) · Compose: [`../../monitoring/`](../../monitoring/).

## Endpoint

- **URL:** `GET /metrics`
- **Format:** Prometheus exposition (text)
- **Auth:** None (typically reachable only from the internal/monitoring network; restrict with a reverse proxy if needed)

## Metrics

| Metric name | Type | Description |
|-------------|------|-------------|
| `api_requests_total` | Counter | Total API requests. Label: `method`, `endpoint`, `status_code`. |
| `api_request_duration_ms` | Histogram | Request duration (ms). Label: `method`, `endpoint`. |
| `api_errors_total` | Counter | API errors. Label: `method`, `endpoint`, `error_type` (status or exception name). |
| `api_active_requests` | Gauge | In-flight request count. |
| `db_query_duration_ms` | Histogram | DB query duration (ms). Label: `query_type` = `select` \| `insert` \| `update` \| `delete` \| `other`. |
| `db_queries_total` | Counter | Total DB queries. Label: `query_type`. |
| `db_connections_active` | Gauge | Open (leased) DB connection count. |
| `tenants_active_total` | Gauge | Active tenant count. |
| `revenue_total_eur` | Gauge | Total revenue (EUR; sum of POS `PaymentDetails.TotalAmount`). |
| `orders_active_total` | Gauge | Active online orders (pending/accepted/preparing/ready). |
| `orders_created_total` | Counter | Created online-order count. |
| `users_registered_total` | Gauge | Registered user count. |
| `cache_hits_total` | Counter | Cache hit count. |
| `cache_misses_total` | Counter | Cache miss count. |
| `cache_size_bytes` | Gauge | Cache size (bytes); `ICacheMetricsService.RecordSize`. |
| `cache_hit_ratio` | Gauge | Hit ratio (hits / (hits + misses)); updated after hit/miss. |
| `replay_total` | Counter | Total offline replay attempts (item count per request). |
| `replay_failed_total` | Counter | Replay items whose result is failed. |
| `replay_duplicate_total` | Counter | Items already synced (idempotent duplicate). |
| `advisory_lock_wait_seconds` | Histogram | Advisory lock acquire duration (seconds). Buckets ~1ms–8s. |
| `payload_hash_mismatch_total` | Counter | Detected and aligned payload_hash mismatch count (replay lazy repair + maintenance repair). |
| `finanzonline_submit_total` | Counter | FinanzOnline submit attempt count. |
| `finanzonline_submit_failed_total` | Counter | FinanzOnline submit error count. Label: `failure_kind` = `transient` \| `permanent` \| `unknown`. |

## Prometheus scrape example

```yaml
scrape_configs:
  - job_name: 'kasse-api'
    static_configs:
      - targets: ['localhost:5183']
    metrics_path: /metrics
    scrape_interval: 15s
```

## Grafana

- **Data source:** Prometheus (the Prometheus server that scrapes).
- **Example queries:**
  - Replay success rate: `rate(replay_total[5m]) - rate(replay_failed_total[5m]) - rate(replay_duplicate_total[5m])` (for newly synced rate).
  - Replay failed rate: `rate(replay_failed_total[5m]) / rate(replay_total[5m])`.
  - Lock wait: `histogram_quantile(0.95, rate(advisory_lock_wait_seconds_bucket[5m]))`.
  - FinanzOnline errors: `sum(rate(finanzonline_submit_failed_total[5m])) by (failure_kind)`.

## Changed / related files

- `backend/Services/Metrics/CacheMetricsService.cs` — `ICacheMetricsService` (`cache_hits_total`, `cache_misses_total`, `cache_size_bytes`, `cache_hit_ratio`).
- `backend/Services/Metrics/DbMetricsService.cs` — `IDbMetricsService` (`db_query_duration_ms`, `db_queries_total`, `db_connections_active`).
- `backend/Services/Metrics/BusinessMetricsService.cs` — `IBusinessMetricsService` (tenants/revenue/orders/users).
- `backend/Services/Hosted/BusinessMetricsRefreshHostedService.cs` — gauge refresh (5 min).
- `backend/Middleware/MetricsMiddleware.cs` — `api_requests_*` / `api_errors_total` / `api_active_requests`.
- `backend/Data/DbQueryDurationInterceptor.cs` — EF command → `IDbMetricsService.RecordQuery`.
- `backend/Data/DbConnectionMetricsInterceptor.cs` — connection open/close → `TrackConnection`.
- `backend/Services/Cache/MemoryCacheService.cs` / `RedisCacheService.cs` — hit/miss → `ICacheMetricsService`.
- `backend/Services/Order/OnlineOrderIntakeService.cs` — `orders_created_total`.
- `backend/Services/CoreMetrics.cs` — `ICoreMetrics` + fiscal/replay Counter/Histogram definitions.
- `backend/Services/OfflineTransactionService.cs` — records `replay_total`, `replay_failed_total`, `replay_duplicate_total`, `advisory_lock_wait_seconds`, `payload_hash_mismatch_total`.
- `backend/Services/FinanzOnlineMetrics.cs` — FinanzOnline counters forwarded to Prometheus.
- `backend/Services/OfflinePayloadHashMaintenanceService.cs` — `payload_hash_mismatch_total` during repair.
- `backend/ApplicationHost.cs` — metrics DI, interceptors, `MetricsMiddleware`, `app.MapMetrics()` for `/metrics`.
