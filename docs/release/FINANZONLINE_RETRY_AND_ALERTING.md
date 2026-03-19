# FinanzOnline automatic retry and alerting

## Değişen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Options/FinanzOnlineRetryJobOptions.cs` | Config: Interval, MaxRetryCount, BaseDelaySeconds, BackoffCapSeconds, BatchSize, AlertFailedThreshold, RegisterRepeatedFailureThreshold, Enabled. Namespace: `KasseAPI_Final.Configuration`. |
| `backend/Services/FinanzOnlineMetrics.cs` | `IFinanzOnlineMetrics` (IncrementSubmitTotal, IncrementSubmitFailed(kind), GetSnapshot), `IFinanzOnlineAlertSink`, `NoOpFinanzOnlineAlertSink`. |
| `backend/Services/FinanzOnlineRetryHostedService.cs` | Background job: Pending retry with exponential backoff, max retry, mark max-retries-exceeded as Failed, emit alerts. |
| `backend/Services/PaymentService.cs` | Optional `IFinanzOnlineMetrics`; IncrementSubmitTotal/IncrementSubmitFailed on create and retry paths. |
| `backend/Controllers/FinanzOnlineReconciliationController.cs` | GET `metrics` endpoint; ctor `IFinanzOnlineMetrics`. |
| `backend/Program.cs` | Register FinanzOnlineRetryJobOptions, IFinanzOnlineMetrics, IFinanzOnlineAlertSink, FinanzOnlineRetryHostedService. |
| `backend/appsettings.json` | `FinanzOnlineRetryJob` section. |
| `docs/release/FINANZONLINE_RECONCILIATION.md` | Sections 5, 8, 9, 10, 11, 12 updated. |

---

## Retry strategy

- **Ne retry edilir:** Sadece `FinanzOnlineStatus == "Pending"` ve `FinanzOnlineRetryCount < MaxRetryCount` (varsayılan 5) ve backoff süresi geçmiş kayıtlar.
- **Backoff:** `BaseDelaySeconds * 2^RetryCount` saniye, en fazla `BackoffCapSeconds` (örn. 3600). İlk denemeden hemen sonra ikinci deneme en erken BaseDelaySeconds saniye sonra.
- **Sonuç:** Success → `Submitted`. Transient fail → `Pending` (retry count artar). Permanent/Unknown fail → `Failed`. Retry sayısı MaxRetryCount’a ulaştıktan sonra bir kez daha fail olursa status `Failed` yapılır, hata metnine "(Max retries exceeded)." eklenir.
- **Duplicate submit:** Job sadece Pending seçer. `RetryFinanzOnlineSubmitAsync` zaten Submitted ise external API çağrılmaz; duplicate submit riski değişmedi.

---

## Ops nasıl izler

1. **Metrikler:** `GET /api/admin/finanzonline-reconciliation/metrics` (FinanzOnlineView) → `submitTotal`, `submitFailedTotal`, `submitFailedTransient`, `submitFailedPermanent`, `submitFailedUnknown`. Uygulama restart’ta sıfırlanır.
2. **Liste:** `GET /api/admin/finanzonline-reconciliation?status=Pending,Failed` ile bekleyen ve başarısız kayıtlar.
3. **Log alert:** "FinanzOnlineAlert" içeren log satırları: Failed sayısı eşiği aşıldığında veya aynı kasa için tekrarlayan hata. Bu mesajlara log tabanlı alert kuralı bağlanabilir.
4. **Alert sink (isteğe bağlı):** `IFinanzOnlineAlertSink` implementasyonu (webhook, queue vb.) kaydedilirse `OnFailedCountThresholdExceeded` ve `OnRegisterRepeatedFailure` çağrılır. Varsayılan: `NoOpFinanzOnlineAlertSink`.
5. **Job kapatma:** `FinanzOnlineRetryJob:Enabled: false` ile otomatik retry kapatılır; sadece manuel retry kullanılır.
