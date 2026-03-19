# FinanzOnline submit — reconciliation & alerting

## Amaç

Ödeme/fatura/fiş DB transaction commit olduktan sonra yapılan FinanzOnline submit’te, DB ile dış raporlama arasında geçici uyuşmazlık oluştuğunda bunun sessiz kalmaması: state, alert/event ve operasyonel görünürlük.

**Önemli:** DB truth geri alınmaz; sadece reconciliation state ve retry/alerting eklenir.

---

## 1. Commit sonrası akış ve hata davranışı

- **Yer:** `PaymentService.CreatePaymentAsync` — transaction commit (payment + invoice + receipt + stock) sonrası, audit log’lardan hemen sonra.
- **Koşul:** `effectiveTseRequired == true` (TSE imzalı ödeme).
- **Yapılan:** `IFinanzOnlineService.SubmitInvoiceAsync(createdInvoice)` çağrılır; sonuç **best-effort** olarak `PaymentDetails` üzerinde reconciliation alanlarına yazılır. Submit veya state güncellemesi hata verirse ödeme işlemi başarılı sayılmaya devam eder (DB rollback yok).

---

## 2. Failure sınıflandırması

| Tür | Açıklama | Örnek | Retry / Alert |
|-----|----------|--------|----------------|
| **Transient** | Geçici ağ/sunucu hatası | `HttpRequestException`, `TaskCanceledException`, timeout, 5xx | Retry uygun; status **Pending**. |
| **Permanent** | Kalıcı/validasyon/duplicate | Mesajda "duplicate", "already submitted", "validation", "forbidden" | Otomatik retry yapılmaz; status **Failed**. |
| **Unknown** | Diğer | Beklenmeyen exception | Retry denenebilir; status **Failed**. |

Sınıflandırma: `FinanzOnlineService.ClassifyFailure` ve payment catch path’te `PaymentService.ClassifyFinanzOnlineFailure` (aynı mantık).

---

## 3. Yeni state modeli (PaymentDetails)

| Alan | Açıklama |
|------|----------|
| `finanz_online_status` | **NotSent** (kullanılmıyor), **Pending**, **Submitted**, **Failed**, **NeedsReconciliation** |
| `finanz_online_error` | Son hata mesajı (truncate 500); Submitted ise null. |
| `finanz_online_reference_id` | Dış sistem referans ID (Submitted ise dolu). |
| `finanz_online_last_attempt_at_utc` | Son deneme zamanı (UTC). |
| `finanz_online_retry_count` | Manuel/otomatik retry sayacı. |

**Kurallar:**

- İlk submit sonrası: **Submitted** (başarı) veya **Pending** (Transient) / **Failed** (Permanent/Unknown).
- Retry başarılı olursa **Submitted**; retry da başarısızsa yine **Pending** veya **Failed**.
- Zaten **Submitted** iken retry çağrılırsa external submit **tekrar çağrılmaz** (duplicate submit riski yönetilir).

---

## 4. Retry-safe reconciliation & event/log

- **FinanzOnlineSubmission:** Her submit denemesi (başarılı/başarısız) için bir kayıt; `InvoiceId`, `Success`, `ErrorMessage`, `ResponseStatusCode`, `ResponseBodyJson`, `SubmittedAt`.
- **FinanzOnlineError:** Her **başarısız** submit’te bir kayıt; `ErrorType = "Submission"`, `InvoiceNumber`, `CashRegisterId`, `ReferenceId`, `Status = "Active"`.
- **Log:** Başarı: `Invoice sent to FinanzOnline: {InvoiceId}, ReferenceId=…`. Başarısız: `FinanzOnline submit failed for Invoice {InvoiceId}: {Error}, FailureKind=…`. State güncellemesi hata verirse: `Failed to update PaymentDetails FinanzOnline state for PaymentId=…; reconciliation view may be stale.`

Alerting: Otomatik retry job çalıştıktan sonra Failed sayısı veya aynı register’da tekrarlayan hata eşiği aşılırsa log (FinanzOnlineAlert) + isteğe bağlı `IFinanzOnlineAlertSink` ile event. Metrikler: `GET /api/admin/finanzonline-reconciliation/metrics` (finanzonline_submit_total, finanzonline_submit_failed_total by FailureKind).

---

## 5. Ops dashboard / sorgulanabilir status

- **GET /api/admin/finanzonline-reconciliation**  
  Sorgulanabilir liste: `status` (Pending, Failed, NeedsReconciliation), `cashRegisterId`, `fromUtc`, `toUtc`, `limit`.  
  Cevap: `total`, `items[]` (PaymentId, ReceiptNumber, CreatedAt, TotalAmount, CashRegisterId, FinanzOnlineStatus, FinanzOnlineError, FinanzOnlineReferenceId, FinanzOnlineLastAttemptAtUtc, FinanzOnlineRetryCount).

- **POST /api/admin/finanzonline-reconciliation/retry/{paymentId}**  
  Manuel retry. Zaten Submitted ise 200 + “Submitted”, external submit tekrar çağrılmaz.

- **GET /api/admin/finanzonline-reconciliation/metrics**  
  Sayaçlar: submitTotal, submitFailedTotal, submitFailedTransient/Permanent/Unknown (uygulama yeniden başlayınca sıfırlanır).

- **SQL (trend / dashboard):**  
  `SELECT finanz_online_status, COUNT(*) FROM payment_details WHERE created_at >= @from AND created_at <= @to GROUP BY finanz_online_status`

---

## 6. Support / admin görünürlüğü

- Reconciliation listesi: **Pending / Failed / NeedsReconciliation** durumları filtrelenebilir.
- Retry: Tekil ödeme için “Retry submit” ile yeniden deneme.
- Fiscal export: Şu an sadece **DailyClosing** için FinanzOnlineStatus/Error/ReferenceId var; payment bazlı FO durumu bu endpoint ve `payment_details` kolonları üzerinden izlenir.

---

## 7. Duplicate external submit riski

- Aynı payment için retry, **Submitted** ise `SubmitInvoiceAsync` **çağrılmaz**; mevcut `FinanzOnlineReferenceId` ile 200 dönülür.
- Gerçek FinanzOnline API’de “zaten gönderilmiş” cevabı gelirse, ileride **Permanent** (duplicate) olarak sınıflandırılıp status **Submitted** yapılabilir ve mevcut referenceId saklanabilir (şu an simülasyon).

---

## 8. Otomatik retry job (background) ve metrikler

- **HostedService:** `FinanzOnlineRetryHostedService` — periyodik (varsayılan 2 dk) Pending kayıtları seçer; exponential backoff (BaseDelaySeconds × 2^RetryCount, cap BackoffCapSeconds) ve max retry (varsayılan 5) uygular. Başarı → Submitted; Transient fail → Pending; Permanent/Unknown fail → Failed. Max retry aşıldıktan sonra status Failed yapılır, hata metnine "(Max retries exceeded)." eklenir.
- **Duplicate submit:** Job sadece status = Pending olanları seçer; `RetryFinanzOnlineSubmitAsync` zaten Submitted ise external submit çağırmaz.
- **Alert:** Her döngü sonunda: (1) Failed sayısı > AlertFailedThreshold ise log "FinanzOnlineAlert: Failed count … exceeds threshold …" + `IFinanzOnlineAlertSink.OnFailedCountThresholdExceeded`. (2) Aynı register’da Failed veya max-retry Pending sayısı ≥ RegisterRepeatedFailureThreshold ise log + `OnRegisterRepeatedFailure`.
- **Metrikler:** `IFinanzOnlineMetrics` — her submit denemesi için `IncrementSubmitTotal`; başarısızda `IncrementSubmitFailed(FailureKind)`. GET `/api/admin/finanzonline-reconciliation/metrics` ile okunur (submitTotal, submitFailedTransient/Permanent/Unknown).
- **Config:** `appsettings` → `FinanzOnlineRetryJob`: Enabled, Interval, MaxRetryCount, BaseDelaySeconds, BackoffCapSeconds, BatchSize, AlertFailedThreshold, RegisterRepeatedFailureThreshold.

---

## 9. Değişen / eklenen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Services/IFinanzOnlineService.cs` | `FinanzOnlineSubmitResponse.FailureKind`, `FinanzOnlineFailureKind` enum. |
| `backend/Models/PaymentDetails.cs` | FinanzOnline reconciliation kolonları. |
| `backend/Data/AppDbContext.cs` | PaymentDetails FO kolon config + index. |
| `backend/Migrations/*_AddPaymentDetailsFinanzOnlineReconciliation.cs` | Tablo migration. |
| `backend/Services/FinanzOnlineService.cs` | SubmitInvoiceAsync: submission/error kaydı, failure sınıflandırması. |
| `backend/Services/PaymentService.cs` | Commit sonrası FO sonucuna göre state güncelleme; `RetryFinanzOnlineSubmitAsync`; `UpdatePaymentFinanzOnlineStateAsync`, `ClassifyFinanzOnlineFailure`; isteğe bağlı `IFinanzOnlineMetrics`. |
| `backend/Controllers/FinanzOnlineReconciliationController.cs` | GET list, POST retry, GET metrics. |
| `backend/Options/FinanzOnlineRetryJobOptions.cs` (namespace Configuration) | Retry job config. |
| `backend/Services/FinanzOnlineMetrics.cs` | IFinanzOnlineMetrics, IFinanzOnlineAlertSink, NoOpFinanzOnlineAlertSink. |
| `backend/Services/FinanzOnlineRetryHostedService.cs` | Background job: Pending retry + backoff + max retry + alert. |
| `backend/Program.cs` | FinanzOnlineRetryJob options, IFinanzOnlineMetrics, IFinanzOnlineAlertSink, FinanzOnlineRetryHostedService. |
| `backend/KasseAPI_Final.Tests/FinanzOnlineReconciliationTests.cs` | Retry başarı, already-submitted idempotency, payment not found. |
| `docs/release/FINANZONLINE_RECONCILIATION.md` | Bu doküman. |

---

## 10. Mevcut risk (önceki durum)

- Commit başarılı, FinanzOnline submit başarısız olsa bile **hiçbir yerde** kalıcı state yoktu; sadece log.
- Hangi ödemelerin dışarıya gönderilmediği **sorgulanamıyordu**.
- Retry veya manuel inceleme için **işaret yoktu**.
- Duplicate submit riski **sadece** “retry yapılmazsa” ile sınırlıydı; artık Submitted ise retry tekrar göndermiyor.

---

## 11. Eksik kalan alanlar

- **DailyClosing:** TagesabschlussService’te FO submit sonrası sadece try/catch ile status yazılıyor; response’taki `Success`/`FailureKind` ile state iyileştirmesi yapılmadı (kapsam dışı).
- **Fiscal export:** Payment bazlı FO status özeti (sayı/yüzde) export JSON’a eklenmedi; istenirse `integrity` veya ayrı bir alanla eklenebilir.
- **Prometheus:** Metrikler şu an in-memory + GET metrics endpoint; Prometheus scrape eklenirse aynı sayaçlar expose edilebilir.

---

## 12. Operasyonda takip yöntemi

1. **Metrikler:** GET `/api/admin/finanzonline-reconciliation/metrics` → `submitTotal`, `submitFailedTotal`, `submitFailedTransient/Permanent/Unknown` (uygulama yeniden başlayınca sıfırlanır).
2. **Liste:** GET `/api/admin/finanzonline-reconciliation?status=Pending,Failed` ile Pending/Failed kayıtları.
3. **Alert (log):** "FinanzOnlineAlert" mesajlarına alert kuralı (Failed count threshold veya register repeated failure). İsteğe bağlı: `IFinanzOnlineAlertSink` implementasyonu (webhook, queue) kaydedilerek event’leri dış sisteme gönderin.
4. **Retry stratejisi:** Otomatik job Pending’leri exponential backoff ile en fazla MaxRetryCount (varsayılan 5) kez dener; sonrasında status Failed yapılır. Manuel retry her zaman mümkün (POST retry/{paymentId}).
5. **Duplicate submit:** Job ve manuel retry, Submitted kayıtları tekrar göndermez; sadece Pending/Failed seçilir / hedeflenir.
