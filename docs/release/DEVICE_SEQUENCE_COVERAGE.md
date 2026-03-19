# DeviceId / ClientSequence coverage (offline replay observability)

## Amaç

Eski mobil build'lerin hâlâ `DeviceId` veya `ClientSequenceNumber` göndermediği durumları ölçmek ve rollout riskini görünür kılmak. Domain davranışı değiştirilmez; sadece observability eklenir.

---

## Coverage nasıl hesaplanıyor

- **Kaynak:** Her offline replay isteğinde, geçerli olan (boş olmayan `OfflineTransactionId` ve `CashRegisterId`) her item için bir **sample** yazılır: `offline_intent_coverage_samples` tablosuna.
- **Alanlar (sample başına):** `created_at_utc`, `cash_register_id`, `has_device_id` (gönderildiyse true), `has_client_sequence` (gönderildiyse true), `replay_batch_correlation_id`.
- **deviceId eksik oranı** = `has_device_id = false` olan sample sayısı / toplam sample sayısı (belirli zaman aralığı ve isteğe bağlı register filtresiyle).
- **sequence eksik oranı** = `has_client_sequence = false` olan sample sayısı / toplam sample sayısı.
- **Register bazında coverage:** Aynı tabloda `cash_register_id` ile gruplanarak her kasa için total / withDeviceId / withSequence sayıları üretilir.
- **Zaman bazlı trend:** `created_at_utc` ile tarih/saat dilimine göre gruplama yapılarak günlük/saatlik coverage oranları çıkarılabilir (SQL veya admin endpoint ile).

Sample yazma, replay akışında **best-effort**’tur: kayıt atarken hata olursa sadece uyarı log’lanır, replay işlemi başarısız sayılmaz.

---

## Fraud-resistance path’inin degrade olması

Offline replay’da sıra tabanlı dolandırıcılık koruması şu blokta çalışır:

- **Koşul:** `!string.IsNullOrWhiteSpace(offline.DeviceId) && offline.ClientSequenceNumber.HasValue`
- **Yapılan:** Aynı `(CashRegisterId, DeviceId)` için önceki maksimum `ClientSequenceNumber` ile karşılaştırma; gap veya duplicate tespit edilirse ilgili audit’ler ve status güncellemesi (Failed / gap flag) uygulanır.
- **Unique index:** `(CashRegisterId, DeviceId, ClientSequenceNumber)` — Postgres’te **null** değerler unique sayılmadığı için, DeviceId veya ClientSequenceNumber **eksik** olan satırlar bu index ile çoğaltma engeli **göremez**.

**Sonuç:** DeviceId veya ClientSequenceNumber gönderilmeyen (eski mobil build) intents için:

- Sıra tabanlı gap/duplicate kontrolü **hiç çalışmaz**.
- Aynı cihazdan gelen birden fazla “boş sıra”lı intent, unique index ile engellenmez (null’lar tekrarlanabilir).
- Fraud-resistance path’i **o intent için** devre dışı kalır; rollout’ta eski client’ların oranı yüksekse risk artar.

Bu davranış `OfflineTransactionService` içinde değiştirilmedi; sadece hangi oranda intent’in bu korumadan yararlanmadığı artık ölçülebilir.

---

## Değişen / eklenen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Models/OfflineIntentCoverageSample.cs` | Yeni entity (observability sample). |
| `backend/Data/AppDbContext.cs` | `OfflineIntentCoverageSamples` DbSet + tablo konfigürasyonu. |
| `backend/Migrations/20260319003746_AddOfflineIntentCoverageSamples.cs` | `offline_intent_coverage_samples` tablosu. |
| `backend/Services/OfflineTransactionService.cs` | Replay loop’ta geçerli item sonrası `RecordOfflineIntentCoverageAsync` çağrısı; private method ile sample insert (try/catch). |
| `backend/Models/Export/FiscalExportDtos.cs` | `FiscalExportIntegrityDto`: `OfflineIntentCoverageTotal`, `OfflineIntentCoverageWithDeviceId`, `OfflineIntentCoverageWithSequence`. |
| `backend/Services/FiscalExportService.cs` | Export penceresinde coverage sorgusu; Integrity’ye sayılar + diagnostic not. |
| `backend/Controllers/OfflineIntentCoverageController.cs` | `GET /api/admin/offline-intent-coverage` (fromUtc, toUtc, cashRegisterId opsiyonel). |
| `docs/release/DEVICE_SEQUENCE_COVERAGE.md` | Bu doküman. |

---

## Operasyonda nasıl izlenecek

1. **Admin endpoint:** `GET /api/admin/offline-intent-coverage?fromUtc=&toUtc=&cashRegisterId=`  
   - Dönüş: `total`, `withDeviceId`, `withSequence`, `deviceIdMissingRate`, `sequenceMissingRate`, `byRegister` (kasa bazlı).  
   - Varsayılan: son 24 saat; `cashRegisterId` verilmezse tüm kasalar.

2. **Fiscal export:** `GET /api/admin/fiscal-export?...` → `integrity.offlineIntentCoverageTotal`, `offlineIntentCoverageWithDeviceId`, `offlineIntentCoverageWithSequence` ve `integrityDiagnosticNotes` içinde özet cümle (DeviceId/Sequence coverage % ve “Low coverage increases replay/fraud-resistance risk” uyarısı).

3. **SQL (trend):**  
   - Günlük: `created_at_utc::date` ile grupla, total / with_device_id / with_client_sequence say.  
   - Saatlik: `date_trunc('hour', created_at_utc)` ile aynı metrikler.

4. **Log:** Sample yazma hatası olursa `OfflineTransactionService` uyarı log’u: “Offline intent coverage sample insert failed for CashRegisterId=…; replay continues.”

---

## Rollout kararları için kullanım

- **deviceIdMissingRate** veya **sequenceMissingRate** yüksekse: hâlâ çok sayıda eski client replay yapıyor demektir; sıra tabanlı fraud-resistance bu intents için devre dışı.
- **byRegister** ile belirli kasa/lokasyonlarda düşük coverage tespit edilebilir; hedef build güncellemesi veya eğitim planlanabilir.
- Zaman bazlı trend ile yeni build rollout’tan sonra oranların düştüğü doğrulanabilir; tam tersi kalıyorsa eski client kullanımı sürüyor demektir.
- Fiscal export alanları, denetim/destek paketlerinde “bu dönemde offline intent’lerin X%’inde DeviceId/Sequence vardı” bilgisini raporlamak için kullanılabilir.

---

## İlgili referanslar

- Offline replay akışı: `backend/Services/OfflineTransactionService.cs` (ReplayOfflineTransactionsAsync, CreateOfflineTransactionRowAsync).
- Sıra kontrolü: aynı dosyada “Step 2: client sequence tracking” bloğu ve unique index yorumu.
- Fiscal export semantics: `docs/release/FISCAL_EXPORT_DIAGNOSTICS.md`.
