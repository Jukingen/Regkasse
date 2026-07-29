# FinanzOnline Sonderbeleg SOAP — Uygulama Planı (P0-1)

**Tarih:** 2026-07-29  
**Hedef:** `RksvFinanzOnlineSubmissionClient` iskeletini gerçek BMF `rkdb` / `belegpruefung` gönderimine bağlamak  
**Kaynak aksiyon:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-1**  
**Değerlendirme:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> Bu doküman teknik uygulama planıdır. Tek başına yasal/BMF uyumluluk kanıtı değildir. Resmi kaynaklar çelişirse BMF dokümanları geçerlidir (`docs/RKSV_OFFICIAL_SOURCES.md`).

---

## 1. Mevcut SOAP iskeletinin yapısı

### 1.1 Dosya ve roller

| Tip | Sınıf | Rol |
|-----|--------|-----|
| Arayüz | `IRksvFinanzOnlineSubmissionClient` | Start/Jahres submit sözleşmesi |
| Fake | `FakeRksvFinanzOnlineSubmissionClient` | Ağ yok; sahte `Verified` / yapılandırılabilir fail |
| Legacy | `NotImplementedRksvFinanzOnlineSubmissionClient` | `NotImplementedException` |
| “Real” iskelet | `RksvFinanzOnlineSubmissionClient` | Config guard + **ağ yok** → `RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED` |

Kaynak: `backend/Services/FinanzOnlineIntegration/RksvFinanzOnlineSubmissionClient.cs`.

### 1.2 Arayüzde tanımlı metodlar

```csharp
Task<RksvFinanzOnlineSubmissionResult> SubmitStartbelegAsync(...);
Task<RksvFinanzOnlineSubmissionResult> SubmitJahresbelegAsync(...);
```

Her ikisi de aynı `SubmitCoreAsync(receiptKind, …)` yoluna düşer.

### 1.3 “Real” istemcinin bugünkü akışı

```text
Enabled == false?
  → RKS_SUBMISSION_DISABLED (+ ManualVerificationRequired)
ValidateEnabledOptions (EndpointUrl HTTPS, Timeout, credential/cert secret *referansları*)?
  → RKS_SUBMISSION_CONFIG_INCOMPLETE
aksi halde
  → log (Production uyarısı)
  → RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED   ← ağ çağrısı YOK
```

`AllowOutboundNetworkCalls` okunur ama **hiçbir HTTP/SOAP yapılmaz** (yorum satırında açıkça belirtilmiş).

### 1.4 Eksik parçalar (iskelette olmayanlar)

| Eksik | Açıklama |
|-------|----------|
| Session edinme | FON Session-Webservice `login` → session `id` |
| Credential resolve | `ParticipantCredentialsConfigurationKey` / `IFinanzOnlineCredentialProvider` ile gerçek tid/benid/şifre okuma |
| mTLS / client cert | `ClientCertificateSecretName` yalnızca doğrulanıyor; HttpClient’a bağlanmıyor |
| Payload mapping | `QrPayload` → BMF `belegpruefung.beleg` (DEP/machine-code biçimi) |
| RKDB XML üretimi | `FinanzOnlineRkdbBelegpruefungXmlBuilder` bu istemciye bağlı değil |
| SOAP gönderimi | `rkdb` envelope POST |
| Yanıt parse | return codes / `verificationResultList` → `RksvFinanzOnlineSubmissionResult` |
| Hata sınıflandırma | Transient vs permanent (outbox handler’ın kullandığı kodlarla hizalı) |
| `SubmitMonatsbelegAsync` | Arayüzde yok (P1-1 kapsamında) |
| Mode hizası | Outbox enqueue hâlâ `Mode=TEST` sabit (P1-2) |

Outbox tarafı (`RksvSpecialReceiptFinanzOnlineOutboxHandler`) bu istemciyi zaten çağırır; eksik olan **transport gerçekliği**.

---

## 2. BMF resmi WSDL / dokümantasyon

Repoda WSDL dosyası gömülü değildir; BMF birincil kaynakları:

| Kaynak | URL / konum |
|--------|-------------|
| **WSDL — Registrierkassen-Webservice** | `https://finanzonline.bmf.gv.at/fonws/ws/regKasseService.wsdl` |
| **Endpoint (rkdb)** | tipik: `https://finanzonline.bmf.gv.at/fonws/ws/rkdb` (WSDL/PDF ile doğrula) |
| **Namespace** | tipik: `https://finanzonline.bmf.gv.at/rkdb` (config: `FinanzOnline:Registrierkassen:SoapNamespace`) |
| **BMF Registrierkassen-Webservice PDF** | [BMF_Registrierkassen_Webservice.pdf](https://www.bmf.gv.at/dam/jcr:19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf) |
| **Doküman sürüm notları** | [BMF_Registrierkassen_Webservice_Dokumentenversion.pdf](https://www.bmf.gv.at/dam/jcr:075f0d4d-7df0-4661-9997-57a6e3b147ff/BMF_Registrierkassen_Webservice_Dokumentenversion.pdf) |
| **Handbuch Registrierkassen** | [BMF_Handbuch_Registrierkassen.pdf](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf) |
| **Session WSDL** | `https://finanzonline.bmf.gv.at/fonws/ws/session` (prod örnek: `FinanzOnline:Session:BaseUrl`) |
| **Hub** | [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md), BMF Registrierkassen start page |

**Operasyon (Sonderbeleg doğrulama için birincil aday):** `rkdb` içinde **`belegpruefung`** — tek beleg, **senkron** yanıt (BMF PDF: Belegprüfung istisnası).  
Paket içeriği XSD (`regKasse.xsd`) ile uyumlu olmalıdır; repoda builder: `FinanzOnlineRkdbBelegpruefungXmlBuilder`.

**Not:** Startbeleg/Jahresbeleg “ayrı SOAP metodları” değildir; aynı `rkdb` + `belegpruefung` ile maschinenlesbarer Code gönderilir. Kasa/SCU **kayıt** (`registrierung_kasse` / `registrierung_se`) ayrı bir işlem türüdür ve zaten `SoapFinanzOnlineRegistrierkassenTransport` hattında ele alınır.

---

## 3. Tamamlama adımları (önerilen sıra)

### Adım 0 — Sözleşme netliği (Compliance + Backend, ~1 İG)

1. Onayla: Startbeleg/Jahresbeleg FON’a **`belegpruefung`** ile mi gidecek? (BMF Webservice modeli buna işaret eder.)  
2. `beleg` alanı: Anlage Z12 maschinenlesbarer Code — **QR wire** (`{machineCode}_{compactJws}`) değil; çoğu zaman **yalnızca machine code** veya BMF’nin istediği tam biçim.  
3. Return code → `Verified` / `Failed` / `ManualVerificationRequired` eşlemesini Compliance ile sabitle.

### Adım 1 — Beleg metni üretimi (Backend, ~2–3 İG)

1. `RksvFinanzOnlineSubmissionPayload.QrPayload` genelde `Receipt.QrCodePayload` = `{machineCode}_{jws}` (`RksvReceiptQrPayloadBuilder`).  
2. `FinanzOnlineRkdbBelegpruefungValidator.IsValidDepCandidate` deseni: `^(_[^_]+){13}$` — **tam QR çoğu zaman FAIL**.  
3. Çözüm: compact JWS’den `SignaturePipeline.TryGetMachineCodeFromCompactJws` **veya** QR’dan machine-code segmentini ayır; validator’dan geçir; gerekirse sertifika serisi / AES anahtarının FON’da kayıtlı olduğunu Ops ile doğrula (`benutzerschluessel` kasa kaydında).

### Adım 2 — Session (güvenlik modeli) (Backend + Ops, ~2 İG)

FON rkdb yolu klasik **WS-Security UsernameToken** değil; mevcut mimari:

```text
Session SOAP login (tid / benid / pin)
  → session id
rkdbRequest: tid, benid, id (=session), art_uebermittlung (T|P), <rkdb>…
HTTPS POST + SOAPAction "rkdb"
```

Mevcut: `SoapFinanzOnlineSessionTransport`, session cache / `GetValidSessionAsync` (submission pipeline).  
**Yapılacak:** RKSV istemcisinin aynı session altyapısını kullanması (yeni WS-Security katmanı yazma).

İsteğe bağlı: client certificate (`ClientCertificateSecretName`) — Ops, FON katılımcı gereksinimine göre HttpClient handler’a bağlar.

### Adım 3 — SOAP isteği oluşturma (Backend, ~2 İG)

1. `FinanzOnlineRkdbBelegpruefungCommand` doldur (`PaketNr`, `SatzNr`, `Beleg`, `TsErstellungUtc`, opsiyonel `Kundeninfo`).  
2. `FinanzOnlineRkdbBelegpruefungXmlBuilder.Build(ns, cmd)` → `<rkdb>…<belegpruefung>…`.  
3. Mapper: mevcut `DefaultFinanzOnlineCommandMapper` + `RkdbBelegpruefung` yolu **yeniden kullanılabilir**.

### Adım 4 — Gönderme (Backend, ~1–2 İG)

`IFinanzOnlineRegistrierkassenClient.SubmitAsync` → (TEST gate) → `SoapFinanzOnlineRegistrierkassenTransport.SubmitAsync`:

- Envelope: `soapenv:Envelope` / `rkdbRequest`  
- Header: `SOAPAction: "rkdb"`  
- `art_uebermittlung`: TEST=`T`, PROD=`P`

### Adım 5 — Yanıt işleme (Backend, ~2–3 İG)

1. `ParseRkdbResponse` çıktısını `RksvFinanzOnlineSubmissionResult`’a map et.  
2. Başarı: `Success=true`, `ExternalReference` (paket/satır/ref), `VerificationStatus=Verified` veya BMF’nin döndürdüğü durum.  
3. Soft-fail / return code ≠ 0: `Failed` veya `ManualVerificationRequired`.  
4. Ağ hataları: `TRANSIENT_*` kodları → outbox retry (`RksvSpecialReceiptFinanzOnlineOutboxHandler.ClassifyRksvClientFailure` ile hizala).  
5. `RawResponseSnapshot`: secret’sız truncate XML/JSON özeti.

### Adım 6 — `RksvFinanzOnlineSubmissionClient` yeniden kablolama (Backend, ~3–4 İG)

```text
RksvFinanzOnlineSubmissionClient (Real)
  ├─ options + AllowOutboundNetworkCalls + cutover guard
  ├─ resolve mode (TEST/PROD) — P1-2 ile birlikte
  ├─ build belegpruefung command from payload
  ├─ map via DefaultFinanzOnlineCommandMapper (veya ince wrapper)
  ├─ obtain session (mevcut session service)
  └─ IFinanzOnlineRegistrierkassenClient.SubmitAsync
       └─ SoapFinanzOnlineRegistrierkassenTransport
```

DI: Real client’a `IFinanzOnlineRegistrierkassenClient`, session/credential bağımlılıkları inject et.  
Production: `ClientKind=Fake` yasakla (host startup veya cutover guard).

### Adım 7 — Outbox / Mode / cutover (Backend + Ops, ~2 İG)

1. P1-2: enqueue `Mode=TEST` sabitini kaldır.  
2. `docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` + `finanzonline-bmf-test-validation-runbook.md` çalıştır.  
3. FA’da Fake vs Real göstergesi (yanlış “Verified” güvenini önle).

---

## 4. Mevcut transport yeniden kullanılsın mı?

### Karar: **Yeni SOAP transport yazma — mevcut `SoapFinanzOnlineRegistrierkassenTransport`’ı kullan**

| Soru | Cevap |
|------|--------|
| Yeni `HttpClient` + envelope gerekir mi? | **Hayır** — rkdb zarfı, SOAPAction, fault parse, transient hata zaten var |
| Ne yazılmalı? | İnce **orchestration** katmanı: payload → belegpruefung → session → `IFinanzOnlineRegistrierkassenClient` → result map |
| Ne zaman yeni transport? | Yalnızca BMF yeni endpoint/operasyon isterse veya mevcut transport’a Sonderbeleg-özel davranış sığmazsa (tercihen kaçın) |

**Gerekçe:** `SoapFinanzOnlineRegistrierkassenTransport` zaten “WSDL: regKasseService.wsdl, operation rkdb” için tasarlanmış; `RkdbPayloadXml` / `belegpruefung` XML’ini gövdeye koyuyor. Parallel SOAP istemcisi drift ve çift bakım riski yaratır.

**Session:** `SoapFinanzOnlineSessionTransport` — ayrıca yeniden yazma.

```text
Önerilen bağımlılık yönü:

  RksvFinanzOnlineSubmissionClient
        → (session + mapper)
        → IFinanzOnlineRegistrierkassenClient
              → SoapFinanzOnlineRegistrierkassenTransport   ✅ reuse
```

---

## 5. Test stratejisi

### 5.1 Katmanlar

| Katman | Ne | Ortam |
|--------|-----|--------|
| **L0 Unit** | Machine-code ayırma; validator; result mapping; config guards; Fake hâlâ yeşil | CI, ağ yok |
| **L1 Handler** | Outbox handler + mock `IRksvFinanzOnlineSubmissionClient` / mock registrierkassen | Mevcut `RksvSpecialReceiptFinanzOnlineOutboxHandlerTests` genişlet |
| **L2 Simulation** | `UseSimulation=true` — outbox state machine, FA UI | Dev; BMF yok |
| **L3 BMF TEST** | `UseSimulation=false`, `EnableRealTestSubmission=true`, `art_uebermittlung=T` | FON TEST kimlik bilgileri; **tek Startbeleg smoke** |
| **L4 PROD** | Cutover guard + çift onay; ilk Jahresbeleg kontrollü | Ops + Compliance imzası |

### 5.2 Sandbox / simülasyon

1. **Varsayılan geliştirme:** Fake veya `UseSimulation` — mevcut `SimulatedFinanzOnlineRegistrierkassenClient` / `FinanzOnlineDeveloperSimulationEngine`.  
2. **Dev smoke:** `FinanzOnlineDevTestController` enqueue-smoke (sentetik belegpruefung) — rkdb kanıtı değil, boru hattı dumanı.  
3. **BMF TEST:** runbook [`docs/finanzonline-bmf-test-validation-runbook.md`](finanzonline-bmf-test-validation-runbook.md), E2E [`docs/release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md`](release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md).  
4. **WireMock / kayıtlı SOAP:** İsteğe bağlı — gerçek BMF yanıt XML’lerini redact ederek golden-file parse testleri (CI’da BMF’ye bağımlı olmadan).

### 5.3 Güvenlik testleri

- Loglarda pin/session/token yok.  
- `RawResponseSnapshot` truncate + redaction.  
- Production’da Fake client startup fail.  
- `AllowOutboundNetworkCalls=false` iken Real client ağ açmaz (feature flag).

### 5.4 Kabul kriterleri (P0-1 done)

- [x] `ClientKind=Real` + Enabled + AllowOutbound + geçerli config → `IFinanzOnlineSubmissionService` / belegpruefung yolu (unit + mock)
- [x] Jahresbeleg aynı `SubmitCoreAsync` yolu
- [x] Fake, Production host’ta `ValidateOnStart` ile reddedilir (`AllowFakeClientInProduction` escape hatch)
- [x] Unit + handler testleri (CI, ağ yok)
- [ ] BMF TEST’te en az bir **Startbeleg** `belegpruefung` senkron başarı (Ops)
- [ ] Aynı yol **Jahresbeleg** için BMF TEST (Ops)
- [ ] Outbox: Pending → … → Verified FA’da görünür (Ops + cutover checklist)
- [x] Assessment / Action Plan “P0-1” kod durumu güncellendi

---

## 6. Riskler ve açık noktalar

| Risk | Azaltma |
|------|---------|
| QR ≠ `beleg` XSD deseni | Machine code ayırma + validator; E2E’de gerçek Startbeleg QR ile doğrula |
| AES `benutzerschluessel` FON’da yok / yanlış | Kasa kaydı (`registrierung_kasse`) önkoşul checklist |
| Session süresi / concurrent outbox | Mevcut session cache + retry; stale Processing recover |
| Yanlış “Verified” (eski Fake) | Prod Fake ban + FA ortam etiketi |
| Monatsbeleg kapsamı | Bu P0 kapsamında değil → P1-1 |
| Ausfall | Ayrı P0-3; aynı rkdb transport’a `ausfall_*` XML eklenebilir (şimdilik kapsam dışı) |

---

## 7. Kabaca iş kırılımı (P0-1 ile uyumlu)

| Faz | İG (kabaca) |
|-----|-------------|
| Sözleşme + beleg mapping | 3–4 |
| Client wiring + session/mapper reuse | 4–6 |
| Response map + hata sınıflandırma + Fake prod ban | 3–4 |
| TEST E2E + runbook + FA göstergesi | 5–7 |
| Buffer (BMF sürpriz / XSD) | 3–4 |
| **Toplam** | **~18–25** ([`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) P0-1) |

**Sorumluluk:** Backend (lead), Ops (credentials/endpoint), Compliance (kabul/return-code), Frontend (Fake/Real / submission durumu netliği — küçük).

---

## 8. İlk kod dokunuşları (önerilen dosyalar)

| Dosya | Değişiklik |
|-------|------------|
| `RksvFinanzOnlineSubmissionClient.cs` | Orchestration; `SoapTransportNotImplemented` kaldır |
| `ApplicationHost.cs` | DI bağımlılıkları; Prod Fake guard |
| `FinanzOnlineRkdbBelegpruefungMapping.cs` / yeni helper | QR/JWS → beleg string |
| `RksvSpecialReceiptService.cs` | Mode ambient (P1-2 ile) |
| `RksvFinanzOnlineSubmissionClientTests.cs` (yeni/geniş) | Unit |
| FA receipt/outbox UI | Ortam / client kind göstergesi |

**Dokunulmadan reuse:** `SoapFinanzOnlineRegistrierkassenTransport`, `SoapFinanzOnlineSessionTransport`, `FinanzOnlineRkdbBelegpruefungXmlBuilder`, outbox handler (gerekirse sadece error-code sınıflandırma).

---

## 9. Referanslar

- Kod: `RksvFinanzOnlineSubmissionClient.cs`, `SoapFinanzOnlineRegistrierkassenTransport.cs`, `FinanzOnlineRegistrierkassenInfrastructure.cs`, `RksvSpecialReceiptFinanzOnlineOutboxHandler.cs`  
- Docs: `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`, `finanzonline-bmf-test-validation-runbook.md`, `release/FINANZONLINE_TEST_MODE_E2E_VERIFICATION.md`, `RKSV_CASH_REGISTER_OPERATIONS.md` §4, `ai/05_SECURITY_COMPLIANCE.md`  
- BMF: `regKasseService.wsdl`, Registrierkassen-Webservice PDF, Handbuch  

---

**Son güncelleme:** 2026-07-29 — P0-1 **kod tamamlandı** (Real client + beleg mapper + result/error sınıflandırma + Fake prod `ValidateOnStart` + P1-2 Mode). BMF TEST/PROD E2E kanıtı Ops/Compliance checklist’te açık.
