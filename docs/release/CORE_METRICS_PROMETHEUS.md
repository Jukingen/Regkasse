# Core Metrics (Prometheus / Grafana)

Temel fiscal ve replay metrikleri Prometheus formatında `/metrics` endpoint'inden sunulur. Prometheus ile scrape edilip Grafana'da dashboard olarak kullanılabilir.

## Endpoint

- **URL:** `GET /metrics`
- **Format:** Prometheus exposition (text)
- **Auth:** Yok (genelde sadece internal/monitoring ağından erişilir; gerekirse reverse proxy ile kısıtlanabilir)

## Metrikler

| Metrik adı | Tip | Açıklama |
|------------|-----|----------|
| `api_requests_total` | Counter | Toplam API istekleri. Label: `method`, `endpoint`, `status_code`. |
| `api_request_duration_ms` | Histogram | İstek süresi (ms). Label: `method`, `endpoint`. |
| `api_errors_total` | Counter | API hataları. Label: `method`, `endpoint`, `error_type` (status veya exception adı). |
| `api_active_requests` | Gauge | Anlık devam eden istek sayısı. |
| `db_query_duration_ms` | Histogram | DB sorgu süresi (ms). Label: `query_type` = `select` \| `insert` \| `update` \| `delete` \| `other`. |
| `db_queries_total` | Counter | Toplam DB sorguları. Label: `query_type`. |
| `db_connections_active` | Gauge | Açık (kiralanmış) DB bağlantı sayısı. |
| `tenants_active_total` | Gauge | Aktif tenant sayısı. |
| `revenue_total_eur` | Gauge | Toplam ciro (EUR; POS `PaymentDetails.TotalAmount` toplamı). |
| `orders_active_total` | Gauge | Aktif online siparişler (pending/accepted/preparing/ready). |
| `orders_created_total` | Counter | Oluşturulan online sipariş sayısı. |
| `users_registered_total` | Gauge | Kayıtlı kullanıcı sayısı. |
| `cache_hits_total` | Counter | Cache hit sayısı. |
| `cache_misses_total` | Counter | Cache miss sayısı. |
| `cache_size_bytes` | Gauge | Cache boyutu (byte); `ICacheMetricsService.RecordSize`. |
| `cache_hit_ratio` | Gauge | Hit oranı (hits / (hits + misses)); hit/miss sonrası güncellenir. |
| `replay_total` | Counter | Toplam offline replay denemesi (istek başına item sayısı). |
| `replay_failed_total` | Counter | Replay sonucu failed olan item sayısı. |
| `replay_duplicate_total` | Counter | Zaten synced (idempotent duplicate) olan item sayısı. |
| `advisory_lock_wait_seconds` | Histogram | Advisory lock alma süresi (saniye). Bucket'lar ~1ms–8s. |
| `payload_hash_mismatch_total` | Counter | Tespit edilip hizalanan payload_hash uyumsuzluk sayısı (replay lazy repair + maintenance repair). |
| `finanzonline_submit_total` | Counter | FinanzOnline gönderim denemesi sayısı. |
| `finanzonline_submit_failed_total` | Counter | FinanzOnline gönderim hata sayısı. Label: `failure_kind` = `transient` \| `permanent` \| `unknown`. |

## Prometheus scrape örneği

```yaml
scrape_configs:
  - job_name: 'kasse-api'
    static_configs:
      - targets: ['localhost:5183']
    metrics_path: /metrics
    scrape_interval: 15s
```

## Grafana

- **Data source:** Prometheus (scrape eden Prometheus sunucusu).
- **Örnek sorgular:**
  - Replay başarı oranı: `rate(replay_total[5m]) - rate(replay_failed_total[5m]) - rate(replay_duplicate_total[5m])` (yeni synced oranı için).
  - Replay failed oranı: `rate(replay_failed_total[5m]) / rate(replay_total[5m])`.
  - Lock bekleme: `histogram_quantile(0.95, rate(advisory_lock_wait_seconds_bucket[5m]))`.
  - FinanzOnline hata: `sum(rate(finanzonline_submit_failed_total[5m])) by (failure_kind)`.

## Değişen / ilgili dosyalar

- `backend/Services/Metrics/CacheMetricsService.cs` — `ICacheMetricsService` (`cache_hits_total`, `cache_misses_total`, `cache_size_bytes`, `cache_hit_ratio`).
- `backend/Services/Metrics/DbMetricsService.cs` — `IDbMetricsService` (`db_query_duration_ms`, `db_queries_total`, `db_connections_active`).
- `backend/Services/Metrics/BusinessMetricsService.cs` — `IBusinessMetricsService` (tenants/revenue/orders/users).
- `backend/Services/Hosted/BusinessMetricsRefreshHostedService.cs` — gauge refresh (5 dk).
- `backend/Middleware/MetricsMiddleware.cs` — `api_requests_*` / `api_errors_total` / `api_active_requests`.
- `backend/Data/DbQueryDurationInterceptor.cs` — EF komut → `IDbMetricsService.RecordQuery`.
- `backend/Data/DbConnectionMetricsInterceptor.cs` — bağlantı open/close → `TrackConnection`.
- `backend/Services/Cache/MemoryCacheService.cs` / `RedisCacheService.cs` — hit/miss → `ICacheMetricsService`.
- `backend/Services/Order/OnlineOrderIntakeService.cs` — `orders_created_total`.
- `backend/Services/CoreMetrics.cs` — `ICoreMetrics` + fiscal/replay Counter/Histogram tanımları.
- `backend/Services/OfflineTransactionService.cs` — replay_total, replay_failed_total, replay_duplicate_total, advisory_lock_wait_seconds, payload_hash_mismatch_total kaydı.
- `backend/Services/FinanzOnlineMetrics.cs` — FinanzOnline sayaçları Prometheus’a yönlendirme.
- `backend/Services/OfflinePayloadHashMaintenanceService.cs` — repair sırasında payload_hash_mismatch_total.
- `backend/ApplicationHost.cs` — metrics DI, interceptors, `MetricsMiddleware`, `app.MapMetrics()` ile `/metrics`.
