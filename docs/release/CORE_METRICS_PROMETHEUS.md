# Core Metrics (Prometheus / Grafana)

Temel fiscal ve replay metrikleri Prometheus formatında `/metrics` endpoint'inden sunulur. Prometheus ile scrape edilip Grafana'da dashboard olarak kullanılabilir.

## Endpoint

- **URL:** `GET /metrics`
- **Format:** Prometheus exposition (text)
- **Auth:** Yok (genelde sadece internal/monitoring ağından erişilir; gerekirse reverse proxy ile kısıtlanabilir)

## Metrikler

| Metrik adı | Tip | Açıklama |
|------------|-----|----------|
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

- `backend/Services/CoreMetrics.cs` — `ICoreMetrics` + Prometheus Counter/Histogram tanımları.
- `backend/Services/OfflineTransactionService.cs` — replay_total, replay_failed_total, replay_duplicate_total, advisory_lock_wait_seconds, payload_hash_mismatch_total kaydı.
- `backend/Services/FinanzOnlineMetrics.cs` — FinanzOnline sayaçları Prometheus’a yönlendirme.
- `backend/Services/OfflinePayloadHashMaintenanceService.cs` — repair sırasında payload_hash_mismatch_total.
- `backend/Program.cs` — `ICoreMetrics` kaydı, `app.MapMetrics()` ile `/metrics`.
