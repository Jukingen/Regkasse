# RKSV Kasa İşlemleri — Operasyonel El Kitabı

> **Yasal uyarı:** Bu belge yalnızca kod tabanındaki uygulamayı özetler; hukuki danışmanlık veya RKSV/ABG uyumluluk garantisi değildir. Avusturya mevzuatı ve resmi yorumlar için uzman veya yetkili mercilere başvurun.

> **Kapsam:** Açıklamalar **Türkçe**; arayüzde Almanca kullanılan menü/sayfa adları **Almanca** bırakılmıştır.

---

## 1. Tagesbericht (formal günlük rapor)

### Ne
Viyana takvim günü ve kasa kapsamında özetlenmiş, `SnapshotJson` + hash ile dondurulabilen **resmî Tagesbericht** kaydı. FinanzOnline tarafına özet gönderimi için outbox mesajı üretilebilir (kodda “Non-DEP summary” olarak not düşülür).

### Neden (işletme / RKSV bağlamı)
Operasyonel gün sonu ile karışmaması için ayrı bir **formal rapor** katmanı; denetim ve muhasebe ile hizalanmış özet + gönderim durumu izlenebilir.

### Menü / sayfa (`frontend-admin`)
- **Tagesbericht (formal)** — `frontend-admin/src/app/(protected)/reporting/tagesbericht/page.tsx` (liste), `.../tagesbericht/[id]/page.tsx` (detay).
- Yan bağlantılar: **Report Center** (`reporting/report-center/page.tsx`), **RKSV** altındaki FinanzOnline ekranları (aşağıda).

### POS / mobil (`frontend`)
- Formal Tagesbericht API’sine bağlı **gerçek** bir POS ekranı **bulunamadı**. `frontend/app/(screens)/reports.tsx` dosyasında örnek veri ve `TODO: API` notları vardır; üretimde formal Tagesbericht **yalnızca admin** tarafında işlenir.

### Backend
- **Controller:** `backend/Controllers/TagesberichtReportsController.cs` — `GET/POST` altında `api/reports/tagesbericht` (liste, `generate`, `finalize`, `correction`, `{id}/submit-finanzonline`).
- **Servis:** `ITagesberichtService` / `TagesberichtService.cs`.
- **Model:** `backend/Models/TagesberichtReport.cs` (tablo `tagesbericht_reports`).
- **DTO:** `backend/Models/Reports/TagesberichtDtos.cs`.

### İzinler
- **Görüntüleme:** `report.view` (`AppPermissions.ReportView`).
- **Üret / finalize / düzeltme:** `report.export` (`AppPermissions.ReportExport`).
- **FinanzOnline gönder:** `finanzonline.submit` (`AppPermissions.FinanzOnlineSubmit`).

### Adım adım (özet)
1. Admin’de **Tagesbericht (formal)** listesine girin; tarih / kasa filtrelerini kullanın.
2. Gerekirse **generate** ile geçici (`Provisional`) özet oluşturun veya yenileyin.
3. İçerik doğrulandıktan sonra **finalize** ile kalıcı hale getirin (kodda `Finalized` / düzeltme zinciri).
4. Gerekirse **FinanzOnline** gönderimini tetikleyin (`POST .../submit-finanzonline`); outbox ve satır üzerindeki gönderim alanları güncellenir.

### Beklenen çıktı
- Liste/detay DTO’larında özet tutarlar, vergi / ödeme yöntemi dağılımı, mutabakat bayrakları, gönderim durumu (`TagesberichtSubmissionStateDto` vb.).

### Sık hatalar / engeller
- Yetkisiz kullanıcı: `403` (ilgili `HasPermission`).
- Gönderim hataları: rapor satırında `LastSubmissionError` / outbox terminal durumları (ayrıntı için FinanzOnline outbox ekranı).

### Eksik / kısmi
- POS’tan formal Tagesbericht **yok** (`reports.tsx` placeholder).
- FinanzOnline tarafı kod yorumlarında **DEP satırı değil**, bilgilendirici özet hattı olarak tanımlanır (`FinanzOnlineOutbox.cs` notları).

---

## 2. Monatsbericht (formal aylık rapor)

### Ne
Ay bazlı formal rapor; Tagesbericht ile aynı yaşam döngüsü desenine yakın (geçici → finalize → düzeltme → FinanzOnline).

### Neden
Aylık resmî özet ve gönderim izi; üst raporlar (ör. Jahresbericht) ile birleştirilebilir.

### Menü / sayfa (`frontend-admin`)
- **Monatsbericht (formal)** — `reporting/monatsbericht/page.tsx`, `reporting/monatsbericht/[id]/page.tsx`.

### POS
**Not found in current implementation** (doğrudan tetikleyici).

### Backend
- `backend/Controllers/MonatsberichtReportsController.cs` — `api/reports/monatsbericht` (+ `generate`, `finalize`, `correction`, `{id}/submit-finanzonline`).
- `IMonatsberichtService` / `MonatsberichtService.cs`, model `MonatsberichtReport`.

### İzinler
Tagesbericht ile aynı: `ReportView`, `ReportExport`, `FinanzOnlineSubmit`.

### Adım adım
Tagesbericht ile paralel: ay seçimi → generate → finalize → isteğe bağlı FinanzOnline submit.

### Beklenen çıktü
`MonatsberichtDto` / liste öğeleri; bağlı günler ve gönderim özeti.

### Eksik / kısmi
- FinanzOnline notu: **Non-DEP monthly summary** (`FinanzOnlineOutbox.cs`).

---

## 3. Jahresbericht (formal yıllık rapor)

### Ne
Yıl bazlı formal rapor; aynı controller/servis kalıbı.

### Menü / sayfa (`frontend-admin`)
- **Jahresbericht (formal)** — `reporting/jahresbericht/page.tsx`, `reporting/jahresbericht/[id]/page.tsx`.

### POS
**Not found in current implementation.**

### Backend
- `backend/Controllers/JahresberichtReportsController.cs` — `api/reports/jahresbericht`.

### İzinler
`ReportView`, `ReportExport`, `FinanzOnlineSubmit`.

### Eksik / kısmi
- FinanzOnline notu: **Non-DEP annual summary** (`FinanzOnlineOutbox.cs`).

---

## 4. RKSV Sonderbelege — Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg

RKSV özel fişleri **POS ödeme rotalarından ayrıdır**; `PaymentService` üzerinden oluşturulmaz. Üretim `RksvSpecialReceiptsController` + `RksvSpecialReceiptService` ile yapılır; sonuç kayıtları normal `PaymentDetails` / `Invoice` / `Receipt` tablolarına yazılır ve fiş DTO’sunda `RksvSpecialReceiptKind` ile işaretlenir.

### Ortak API (`backend`)

| İşlem | HTTP | İzin (`AppPermissions`) |
|--------|------|-------------------------|
| Monats-Nullbeleg | `POST api/rksv/special-receipts/nullbeleg` | `RksvNullbelegCreate` |
| Startbeleg | `POST api/rksv/special-receipts/startbeleg` | `RksvStartbelegCreate` |
| Monatsbeleg | `POST api/rksv/special-receipts/monatsbeleg` | `RksvMonatsbelegCreate` |
| Jahresbeleg | `POST api/rksv/special-receipts/jahresbeleg` | `RksvJahresbelegCreate` |
| Schlussbeleg (Endbeleg) | `POST api/rksv/special-receipts/schlussbeleg` | `RksvSchlussbelegCreate` |

İstek/yanıt gövdeleri: `backend/DTOs/RksvSpecialReceiptDtos.cs`. Controller: `backend/Controllers/RksvSpecialReceiptsController.cs`. İş kuralları ve TSE sıfır tutarlı imza: `backend/Services/RksvSpecialReceiptService.cs`.

### Admin arayüzü (`frontend-admin`)

- **Sayfa:** `frontend-admin/src/app/(protected)/rksv/sonderbelege/page.tsx` → `RksvSonderbelegePage` (`frontend-admin/src/features/rksv-operations/components/RksvSonderbelegePage.tsx`).
- **Menü:** RKSV grubu altında **Sonderbelege** (`nav.rksvLeafSonderbelege`); rota `/rksv/sonderbelege` (`frontend-admin/src/features/rksv/rksvAdminMenuModel.ts`).
- **Akış (özet):** Kasa seçimi → ilgili izin varsa oluşturma kartları (Nullbeleg / Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg) → `POST /api/rksv/special-receipts/...` → son fiş listesi `getApiReceiptsList` ile son 300 kayıt içinden `rksvSpecialReceiptKind` dolu olanlarla süzülür. İsteğe bağlı **Beleg erneut drucken** (`ReceiptReprintWizard`, `RECEIPT_REPRINT`). Schlussbeleg için ek onay metni modalı vardır.
- **Fiş detayı:** `receipts/[receiptId]/page.tsx` içinde Startbeleg/Jahresbeleg için `RksvSpecialReceiptFinanzOnlineSubmissionCard` (`ReceiptDTO.rksvFinanzOnlineSubmission`).
- **FinanzOnline durumu (yalnızca izlenen türler):** Arayüzde Startbeleg ve Jahresbeleg için gönderim rozeti/metni `isRksvFinanzOnlineTrackedSpecialReceiptKind` (`frontend-admin/src/features/receipts/utils/rksvFinanzOnlineSubmissionUi.ts`) ile sınırlanır; backend ile uyumludur.

### FinanzOnline outbox izi (kod davranışı)

- **Outbox kuyruğuna eklenen Sonderbeleg türleri:** yalnızca **Startbeleg** ve **Jahresbeleg** (`RksvSpecialReceiptService` içinde `EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync` çağrıları; mesaj türleri `FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission` / `RksvJahresbelegSubmission`).
- **Nullbeleg, Monatsbeleg, Schlussbeleg:** `RksvSpecialReceiptService` bu türler için `EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync` çağırmaz; `RksvSpecialReceiptFinanzOnlineSubmissions` satırı da bu oluşturma yollarında eklenmez.
- **Durum satırı:** `RksvSpecialReceiptFinanzOnlineSubmission` entity + `ReceiptDTO.RksvFinanzOnlineSubmission` (`RksvFinanzOnlineSubmissionStatusDto`). İşleyici: `RksvSpecialReceiptFinanzOnlineOutboxHandler`.

### 4.1 Nullbeleg

Viyana takvimi `year`/`month` için kasa başına tek kayıt; `ActsAsJahresbeleg` isteğe bağlı (gövdede `null` ise Aralık için `true` varsayılanı serviste uygulanır). Misafir müşteri + sıfır tutarlı ödeme/fatura/fiş üretimi `RksvSpecialReceiptService.CreateNullbelegAsync`.

### 4.2 Startbeleg

Kasa başına tek Startbeleg; kasa kalıcı devre dışı değilse. Oluşturma sonrası FO submission satırı ve outbox mesajı eklenir (`CreateStartbelegAsync`).

### 4.3 Monatsbeleg

Yalnızca **mevcut** Viyana takvim ayı için; Aralık ayında istek **Jahresbeleg** yoluna yönlendirilir (ayrı `Monatsbeleg` satırı üretilmez — servis içi dallanma). FO özel iz kaydı yok.

### 4.4 Jahresbeleg

Viyana takvim yılı **şu anki yıl veya bir önceki yıl** ile sınırlı; erken düzenleme notu `EarlyReason` ile taşınabilir. Oluşturma sonrası FO submission + outbox (`CreateJahresbelegAsync`).

### 4.5 Schlussbeleg (Endbeleg)

Açık vardiya yokken ve kasa durumu kapalı uygunluğunda; sonrasında kasa **kalıcı olarak devre dışı** bırakılır. FO özel iz kaydı yok.

---

## 9. Belege / fişler (Receipts)

### Ne
Ödeme ile atomik oluşturulan **kalıcı** fiş (`receipts` tablosu + kalemler + vergi satırları). “Ödeme sonrası tembel üretim” yok; `GetReceiptByPaymentId` açıkça kalıcı fiş bekler.

### Neden
RKSV için imza, zincir, QR yükü ve satır düzeyi kanıt.

### Menü / sayfa (`frontend-admin`)
- **Belege** — `frontend-admin/src/app/(protected)/receipts/page.tsx`, detay `receipts/[receiptId]/page.tsx` (nav etiketi: `nav.receipts` / varsayılan Almanca **Belege**).

### POS
- Ödeme tamamlama: `frontend/components/PaymentModal.tsx` → `frontend/services/api/paymentService.ts` (`api/pos/payment`).
- Fiş verisi: `PaymentService.GetReceiptDataAsync` → `ReceiptsController` / `ReceiptService`.

### Backend
- **Liste / okuma:** `backend/Controllers/ReceiptsController.cs` — `api/Receipts/list` (`SaleView`), `api/Receipts/by-payment/{paymentId}`, `api/Receipts/{receiptId}`.
- **Oluşturma:** Ödeme sırasında `PaymentService` içinde `_receiptService.AddReceiptFromPaymentToContextAsync` (`PaymentService.cs`).
- **Servis:** `ReceiptService.cs` (QR yükü `_R1-AT1_...` biçimi, önceki imza, sertifika seri no).

### İzinler
- Admin liste/detay: `sale.view` (`ReceiptsController` üzerinde `HasPermission(AppPermissions.SaleView)`).
- `create-from-payment`: `sale.create`.
- POS ödeme: `payment.take` (`PaymentController`).

### Adım adım
1. POS’ta sepeti ödeyin → sunucu ödeme + fatura + fişi tek transaction’da yazar.
2. Admin’de **Belege** üzerinden arama / detay veya ödeme kaydından ilişkili fişe geçin.

### Beklenen çıktü
`ReceiptDTO`: `ReceiptNumber`, `Date`, `KassenID`, şirket, satırlar, `TaxRates`, `Payments`, `Signature` (JWS, önceki imza, QR metni).

### Sık hatalar
- Ödeme var fiş yok: `GetReceiptDataAsync` log’unda “receipt must be created at payment time” uyarısı ile uyumlu `404`.

### Eksik / kısmi
- (Sonderbelege için bkz. **bölüm 4**; normal satış fişi yolu değişmedi.)

---

## 10. Zahlungen / ödemeler (Payments)

### Ne
`payment_details` tabanlı satış ödemesi; POS faturası (`invoices` + `SourcePaymentId`) ile eşlenir.

### Neden
Gün sonu ve formal raporların **fatura eşlemesi** kontrolleri (ör. `GetPaymentsWithoutInvoiceCountAsync`) ödeme–fatura tutarlılığına dayanır.

### Menü / sayfa (`frontend-admin`)
- **Zahlungen** — `frontend-admin/src/app/(protected)/payments/page.tsx`.

### POS
- `PaymentModal` + `paymentService` → `POST api/pos/payment` (legacy: `api/Payment` aynı controller).

### Backend
- `backend/Controllers/PaymentController.cs` — sınıf düzeyi `payment.take`; yöntemler `methods`, `POST` create vb.

### İzinler
- Ana POS ödemesi: `payment.take`.
- Admin ödeme listesi için ilgili sayfadaki guard’lar (repo: `routePermissions` / özellikler; detay için `frontend-admin` route izin eşlemesine bakın).

### Adım adım
POS’ta ödeme alın; admin’de **Zahlungen** ile doğrulama, FinanzOnline kuyruğu veya mutabakat için çapraz kontrol.

### Eksik / kısmi
- `paymentService` içinde günlük rapor için kullanılmaması gerektiğine dair yorum vardır (backend’de ayrı “daily-report” route yok).

---

## 11. Tagesabschluss / operativer Tagesabschluss (gün sonu ve dönem kapanışları)

### Ne
- **Günlük:** `DailyClosing` (`ClosingType = Daily`), TSE imzası, Viyana takvim günü için `Invoice` (Paid) toplamları; FinanzOnline açıksa `SubmitDailyClosingAsync`.
- **Aylık / yıllık:** Benzer şekilde `Monthly` / `Yearly` kapanış kaydı ve TSE imzası; **kod incelemesinde** aylık/yıllık yol için `FinanzOnlineService.SubmitMonthlyClosingAsync` / `SubmitYearlyClosingAsync` çağrısı **günlük** akıştaki gibi görünmedi (yalnızca günlük kapanışta `SubmitDailyClosingAsync` çağrısı vardı).

### Neden
Kasa dönemini TSE ile mühürlemek ve (etkinse) FinanzOnline’a iletmek.

### Menü / sayfa (`frontend-admin`)
- **Operativer Tagesabschluss** — `frontend-admin/src/app/(protected)/tagesabschluss/page.tsx` (nav: `nav.tagesabschluss`).

### POS
- `frontend/components/TagesabschlussModal.tsx` → `frontend/services/api/tagesabschlussService.ts` → `POST /tagesabschluss/daily|monthly|yearly`, `GET .../can-close/{id}`, `history`, `statistics` (istemci tabanı `/api` önekli olabilir; sunucu sınıfı `api/Tagesabschluss`).

### Backend
- `backend/Controllers/TagesabschlussController.cs` — **tüm** aksiyonlar sınıf düzeyinde `HasPermission(AppPermissions.TseSign)`.
- `backend/Services/TagesabschlussService.cs` — `PerformDailyClosingAsync`, `PerformMonthlyClosingAsync`, `PerformYearlyClosingAsync`.

### İzinler
- **`tse.sign`** (controller sınıfı).

### Adım adım
1. `can-close` ile engel kontrolü (TSE bağlı mı, gün içi faturasız ödeme var mı, bugün zaten kapanmış mı).
2. Günlük kapanışı çalıştırın; sonuç DTO’sunda `TseSignature`, isteğe bağlı `FinanzOnlineStatus` alanları.

### Sık hatalar
- TSE bağlı değil: `InvalidOperationException` (“TSE device is not connected…”).
- Faturasız ödeme: `Closing blocked: N payment(s) without a matching invoice`.
- İşlem yok: “No transactions found for today…”.

### Nachträglicher (rückdatierter) Tagesabschluss
- Geçmiş Vienna iş günü için kapanış mümkün; `CreatedAt` / TSE imza zamanı **geriye alınmaz**.
- `is_backdated`, `late_creation_reason` (zorunlu) ve audit `TagesabschlussBackdatedCreated`.
- Ayrıntı: [`docs/BACKDATED_TAGESABSCHLUSS.md`](BACKDATED_TAGESABSCHLUSS.md).

### Tagesabschluss sonrası (doğrulanmış RKSV davranış)

| Kural | Uygulama |
|-------|----------|
| Kapalı kasada yeni ödeme yok | `RegisterStatus.Closed` → `ValidatePaymentRegister*` → `CASH_REGISTER_CLOSED` → HTTP 400 |
| Yeni satış için kasa/schicht açılmalı | `TryOpenCashRegisterAsync` → `Open` |
| Aynı Viyana gününde ikinci Daily closing yok | `CanPerformClosingAsync` + unique index `(CashRegisterId, ClosingDate, ClosingType)` |

**Not:** `IsClosed` alanı yok; durum `CashRegister.Status` enum’udur. Aynı takvim gününde Z-Bericht sonrası yeniden açıp satış almak kodda engellenmez; ikinci kapanış engellenir — operasyonel risk ayrıntısı: [`docs/RKSV_AFTER_TAGESABSCHLUSS.md`](RKSV_AFTER_TAGESABSCHLUSS.md).

### Eksik / kısmi
- Aylık/yıllık kapanışta FinanzOnline gönderiminin günlük ile **paralel olmadığı** kod okumasıyla ortaya çıkar; üretim kararı için ek doğrulama önerilir.
- Aynı Viyana gününde Tagesabschluss sonrası reopen + ödeme için sert engel **yok** (yukarıdaki doğrulama dokümanı).

---

## 12. DEP / dışa aktarma / denetim izi

### Ne (uygulamada)
1. **FinanzOnline RKDB `belegpruefung`:** DEP desenine uyan `beleg` metni için yapısal doğrulama (`FinanzOnlineRkdbBelegpruefungValidator`). Fiş QR metni çoğu zaman bu desenle **aynı değildir** (`FinanzOnlineService.TryResolveRkdbBelegpruefungAsync` yorumu).
2. **Fiscal export (DEP-benzeri paket):** `GET api/admin/fiscal-export` — `IFiscalExportService` “DEP-like fiscal export package” üretir; profiller `operational_preview`, `accounting_report`, `legal_compliance_export`, `diagnostic_package` (`FiscalExportController`, `FiscalExportProfileRules`).
3. **Bütünlük raporu:** `GET api/admin/integrity` — `IntegrityController`, `AuditView`.
4. **Hukuki export öncesi kapı:** `GET api/reports/legal-export-completeness/...` — `LegalExportCompletenessController`, `ReportView`.

### Menü / sayfa (`frontend-admin`)
- **Fiscal export (tanı)** — `/rksv/fiscal-export-diagnostics` (`fiscal-export-diagnostics/page.tsx`); izin eşlemesi `routePermissions.ts` içinde `REPORT_EXPORT`.
- **RKSV · Integrity** — `/rksv/integrity` (`integrity/page.tsx`).
- Formal raporlarla ilişkili metin çözümleyiciler: `formalReportContentResolver.ts` (export profili satırı).

### POS
Doğrudan DEP dışa aktarma **yok**; veri sunucu/admin API’lerinden alınır.

### İzinler (özet)
- Fiscal export: en az `report.export`; daha sıkı profiller `audit.view` ve `fiscal.export.compliance` (`FiscalExportProfileRules`).
- Integrity: `audit.view`.

### Eksik / kısmi
- Klasik “tek düz DEP dosyası üret ve BMF’ye yükle” akışı bu belgede **ayrı bir ürün özelliği olarak doğrulanmadı**; mevcut olan fiscal export JSON paketi ve RKDB `belegpruefung` doğrulama katmanlarıdır.

---

## 13. FinanzOnline / RKSV doğrulama ve operasyon ekranları

### Ne
- **Yapılandırma ve tanı:** `FinanzOnlineController` — `api/FinanzOnline/config`, `status`, test connection, hata geçmişi vb. (`SettingsView` / `FinanzOnlineView` / `FinanzOnlineManage` kombinasyonları endpoint bazında dosyada ayrılmıştır).
- **Outbox (birincil yaşam döngüsü):** `FinanzOnlineOutboxAdminController` — `GET api/admin/finanzonline-outbox` (`FinanzOnlineView`).
- **Eski ödeme satırı mutabakatı:** `FinanzOnlineReconciliationController` — `GET api/admin/finanzonline-reconciliation` (`FinanzOnlineView`), `POST .../retry/{paymentId}` (`FinanzOnlineSubmit`); controller açıklamasında **legacy** ve outbox’a tercih edilmesi notu vardır.
- **Hazırlık özeti:** `FinanzOnlineReadinessController` — `GET api/admin/finanzonline-readiness` (`FinanzOnlineView`).

### Menü / sayfa (`frontend-admin`, Almanca nav örnekleri)
- **FinanzOnline · Outbox** — `/rksv/finanz-online-outbox`
- **FinanzOnline-Abgleich** (kuyruk) — `/rksv/finanz-online-queue`
- **FinanzOnline-Abgleich (Legacy)** — aynı kuyruk sayfasında legacy rozet
- **RKSV Übersicht / Status** — `/rksv/status`
- **FinanzOnline (diagnostic)** — `/rksv/finanz-online-operations`
- **Verifications** — `/rksv/verifications` (dosya mevcut)
- **Report Center** — FinanzOnline outbox’a bağlantılar içerir

### POS
FinanzOnline yönetim ekranları **admin**dedir; POS ödeme sonrası gönderim `PaymentService` / `FinanzOnlineService` ile arka planda yürür (ayrıntı için ilgili servis ve `DispatchPostCommitComplianceAsync`).

### İzinler (özet)
- `finanzonline.view`, `finanzonline.manage`, `finanzonline.submit` (`AppPermissions`).

### Eksik / kısmi
- Formal rapor FinanzOnline mesajları **DEP özet hattı değildir** (outbox notları).

---

## Ek: Operative Berichte (Tagesbericht değil)

**Operative Berichte** / **Report Center** / **Personal / Kassenleistung** — `OperationalReportsController.cs` (`api/Reports/operational/...`). Bunlar `payment_details` tabanlı operasyonel özetlerdir; formal **Tagesbericht (formal)** ile karıştırılmamalıdır (controller XML yorumunda X/Z açıklaması vardır).

---

## Hızlı referans — API yolları (kanıt özeti)

| Alan | Temel route |
|------|----------------|
| Formal Tagesbericht | `api/reports/tagesbericht` |
| Formal Monatsbericht | `api/reports/monatsbericht` |
| Formal Jahresbericht | `api/reports/jahresbericht` |
| Operasyonel raporlar | `api/Reports/operational/...` |
| Legal export completeness | `api/reports/legal-export-completeness/...` |
| POS ödeme | `api/pos/payment` (+ legacy `api/Payment`) |
| Tagesabschluss | `api/Tagesabschluss/...` |
| RKSV Sonderbelege | `api/rksv/special-receipts/nullbeleg`, `.../startbeleg`, `.../monatsbeleg`, `.../jahresbeleg`, `.../schlussbeleg` |
| Fişler | `api/Receipts/...` |
| Fiscal export | `api/admin/fiscal-export` |
| Integrity | `api/admin/integrity` |
| FO outbox | `api/admin/finanzonline-outbox` |
| FO reconciliation (legacy) | `api/admin/finanzonline-reconciliation` |
| FO readiness | `api/admin/finanzonline-readiness` |
| FinanzOnline config/status | `api/FinanzOnline/...` |

---

## Son kontrol listesi (operasyon)

| Konu | Durum |
|------|--------|
| POS’tan formal Tagesbericht | Eksik (placeholder ekran) |
| RKSV Sonderbelege (Nullbeleg … Schlussbeleg) | Bölüm 4 + `api/rksv/special-receipts/*`; FO özel iz: Startbeleg + Jahresbeleg |
| Aylık/yıllık Tagesabschluss → FinanzOnline | Günlük ile aynı otomasyon kodda görülmedi |
| Formal rapor FO gönderimi | Bilgilendirici / Non-DEP özet (outbox notu) |
