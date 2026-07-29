# Ausfall- / Wiederinbetriebnahme-meldung — Tasarım Planı (P0-3)

**Tarih:** 2026-07-29  
**Aksiyon:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-3** (~12–16 İG)  
**Bağımlılık:** P0-1 (rkdb SOAP transport reuse) tercih edilir; simülasyon ile paralel geliştirilebilir  
**İlgili:** [`FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md`](FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md), [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md), [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> Bu doküman tasarım + operatör runbook taslağıdır. **Yasal tavsiye değildir**; BMF birincil kaynakları çelişirse onlar geçerlidir. Compliance onayı olmadan otomatik FON gönderimi açılmamalıdır.

---

## 1. Yasal / BMF kanalı (özet)

### 1.1 Ne bildirilir?

RKSV / FinanzOnline Registrierkassen-Webservice modeli, güvenlik birimi veya kasa **arıza (Ausfall)** ve **yeniden işletmeye alma (Wiederinbetriebnahme)** ile **kalıcı dışı bırakma (Außerbetriebnahme)** durumlarının BMF’ye iletilmesini destekler.

Bu, dahili “TSE Offline” logundan farklıdır: **FON’a resmi kayıt** gerekir.

### 1.2 Kanal ve form

| Kanal | Form / operasyon | Not |
|-------|------------------|-----|
| **Birincil (otomasyon)** | FinanzOnline **Registrierkassen-Webservice** — SOAP `rkdb` | WSDL: `https://finanzonline.bmf.gv.at/fonws/ws/regKasseService.wsdl` |
| **Manuel (portal)** | FinanzOnline web UI — Registrierkassen / güvenlik birimi işlemleri | Ops fallback |
| **Dosya yükleme** | (opsiyonel) asenkron paket / DataBox protokolü | Webservice dışı; bu P0’da ikincil |

**BMF dokümanları:**

- [Registrierkassen-Webservice PDF](https://www.bmf.gv.at/dam/jcr:19c193f4-99cd-42ff-9b23-655f2ab5734e/BMF_Registrierkassen_Webservice.pdf)
- [Handbuch Registrierkassen](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf)
- Hub: [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md)

### 1.3 `rkdb` içindeki ilgili elementler

Bir `rkdb` paketinde **tek işlem türü** (BMF kuralı); Ausfall için tipik seçenekler:

| Element | Kimlik | İçerik (özet) |
|---------|--------|----------------|
| `ausfall_se` | `zertifikatsseriennummer` | `ausfall` **veya** `ausserbetriebnahme` (`begruendung` + `beginn_ausfall`) |
| `wiederinbetriebnahme_se` | `zertifikatsseriennummer` | `ende_ausfall` |
| `ausfall_kasse` | `kassenidentifikationsnummer` | aynı `ausfall` / `ausserbetriebnahme` |
| `wiederinbetriebnahme_kasse` | `kassenidentifikationsnummer` | `ende_ausfall` |

- `beginn_ausfall` / `ende_ausfall`: xs:dateTime; **gelecekte olmamalı** (Ausfall başlangıcı).  
- `satznr`, opsiyonel `kundeninfo`, paket `paket_nr` + `ts_erstellung`.  
- Session: önce Session-Webservice `login`; `rkdbRequest` içinde `tid`, `benid`, `id`, `art_uebermittlung` (`T`/`P`).

**Regkasse eşlemesi (öneri):**

| Olay | Varsayılan rkdb türü | Gerekçe |
|------|----------------------|---------|
| SCU/TSE cihaz imza veremez, sertifika bilinen | **`ausfall_se`** | Signaturerstellungseinheit |
| Kasa kimliği düzeyinde kesinti / decommission | **`ausfall_kasse`** / `ausserbetriebnahme` | RegisterNumber = Kassen-ID |
| Failover sonrası birincil geri geldi | **`wiederinbetriebnahme_se`** (veya kasse) | `ende_ausfall` |

Compliance, “kaç dakika Offline = zorunlu Ausfall” eşiğini yazılı onaylamalıdır (aşağıda §3.3).

### 1.4 Ayrı kavram: fiş üzerindeki Ausnahmezustand

Beleg machine code / Besonderheit (`see-ausfall` vb.) **fiş içeriği**dir; FON `ausfall_se` kaydının yerine geçmez. İkisi tamamlayıcı olabilir; bu plan **FON rkdb Ausfall/Wiederinbetriebnahme** odaklıdır.

---

## 2. Mevcut durum (tespit var, FON yok)

### 2.1 Tespit ve iç bildirim

| Bileşen | Ne yapar | FON? |
|---------|----------|------|
| `TseHealthCheckService` | Periyodik probe; cached Online/Degraded/Offline | Hayır |
| `TseFailoverBackgroundService` | Primary’ler için `CheckAndFailoverAsync` + cert expiry | Hayır |
| `TseFailoverService` | Otomatik/manuel failover, revert | Hayır |
| `TseFailoverNotificationService` | Activity: `TseFailoverStarted/Activated/Failed/Reverted/…` | Hayır (yalnızca activity/email) |
| `TseIncidentService` | İç incident CRUD (`/admin/tse/incidents`) | Hayır |
| FA `/rksv/incident` | Correlation-ID soruşturma (replay + audit + FO **reconciliation** satırları) | Ausfall enqueue yok |
| FA `/admin/tse/failover` | Failover ops | FON yok |
| `FinanzOnlineSubmissionKind` | `Register` \| `SignatureUnit` | **Ausfall yok** |

### 2.2 Olay yakalama noktaları (hook’lar)

Otomatik enqueue için önerilen **tek yayın yüzeyi**:

```text
ITseAusfallEventPublisher  (yeni, ince)
  ← TseFailoverNotificationService.NotifyFailoverCompleted / Failed / Reverted
  ← TseHealthMonitor Offline geçişi (debounce sonrası)
  ← Manual API (FA “Ausfall melden”)
  ← Cash register Schlussbeleg / decommission (ausserbetriebnahme — ayrı akış)
```

**Yakalama stratejisi:**

1. **Failover activated** (primary unhealthy → backup): aday `ausfall_se` (eski primary sertifika) + opsiyonel kasa notu.  
2. **Revert to primary** / primary Online stabil: aday `wiederinbetriebnahme_se`.  
3. **Offline süresi ≥ eşik, failover yok**: aday Ausfall (Compliance eşiği).  
4. **Manuel:** operatör FA’dan form + onay.

Mevcut activity event’leri **kaynak sinyal** olarak kalır; FON outbox’a doğrudan Activity’den yazmak yerine merkezi publisher kullanılsın (idempotency + debounce).

---

## 3. Bildirim mekanizması (outbox)

### 3.1 Evet — yeni message type + handler

Mevcut `FinanzOnlineOutbox` altyapısı (retry, dead-letter, idempotency) **yeniden kullanılır**.

| Parça | Öneri |
|-------|--------|
| Message types | `RksvAusfallSeSubmission`, `RksvWiederinbetriebnahmeSeSubmission`, `RksvAusfallKasseSubmission`, `RksvWiederinbetriebnahmeKasseSubmission` (veya tek tip + `Kind` alanı) |
| Aggregate | `TseDevice` / `CashRegister` + `AusfallEpisodeId` |
| BusinessKey | `ausfall\|{tenant}\|se\|{certSerial}\|beginn\|{utc:o}` (tekrar gönderimi engeller) |
| Payload | `zertifikatsseriennummer` veya `kassenidentifikationsnummer`, `begruendung`, `beginn_ausfall` / `ende_ausfall`, `satznr`, mode |
| XML | Yeni builder: `FinanzOnlineRkdbAusfallXmlBuilder` (belegpruefung builder kalıbı) |
| Transport | **`SoapFinanzOnlineRegistrierkassenTransport`** + session (P0-1 ile aynı) — yeni SOAP istemcisi yok |
| Handler | `RksvAusfallFinanzOnlineOutboxHandler` → map → `IFinanzOnlineRegistrierkassenClient.SubmitAsync` |
| Durum tablosu | `rksv_ausfall_finanz_online_submissions` (Startbeleg FO submission satırına benzer) |

### 3.2 Akış

```text
Tetik (auto/manual)
  → Debounce / policy gate (Demo/Soft → skip; Production lock OK)
  → Create episode row (Open)
  → Enqueue outbox (Pending)
  → Worker + session + rkdb ausfall_* 
  → Submitted / Verified / Failed / ManualVerificationRequired
  → (recovery) Wiederinbetriebnahme enqueue (ende_ausfall)
  → Episode Closed
```

### 3.3 Otomatik vs onaylı otomatik (önerilen politika)

| Mod | Davranış |
|-----|----------|
| **`Ausfall:AutoEnqueue=false`** (varsayılan ilk sürüm) | Sadece Activity + FA “Bekleyen Ausfall önerisi”; operatör **onaylayınca** enqueue |
| **`Ausfall:AutoEnqueue=true`** | Eşik aşılınca doğrudan enqueue (Compliance onayı sonrası) |
| Demo / Soft / `TseMode=Off` | **Asla** FON’a gitme |

**Debounce:** örn. Offline ≥ `AusfallGraceMinutes` (default 30, config) ve hâlâ Offline → öneri/enqueue. Kısa glitch’ler bildirilmez.

**Begründung kodları:** BMF XSD/PDF’deki gerekçe alanına map (Compliance sabit listesi + i18n FA select).

### 3.4 P0-1 bağımlılığı

- Transport iskeletken: outbox + Fake/Simulation ile state machine test edilir; gerçek BMF TEST P0-1 sonrası.  
- `RKS_SOAP_TRANSPORT_NOT_IMPLEMENTED` → outbox retry/dead-letter (Startbeleg ile aynı sınıflandırma).

---

## 4. FA UI

### 4.1 Evet — kart + liste eklenmeli

| Yer | İçerik |
|-----|--------|
| **`/admin/tse/failover`** veya yeni **`/admin/tse/ausfall`** | Episode listesi: cihaz, sertifika, beginn/ende, FON status Tag, outbox link |
| **`/rksv/finanz-online-outbox`** | MessageType filtresi: Ausfall / Wiederinbetriebnahme |
| **`/rksv/incident`** | Correlation varsa FO Ausfall satırına link (mevcut FO reconciliation yanına) |
| **`/admin/tse-management`** | Cihaz detayında “Ausfall melden” / “Wiederinbetriebnahme” aksiyonları |
| Activity bell | Yeni event: `TseAusfallReported`, `TseWiederinbetriebnahmeReported`, `TseAusfallEnqueueSuggested` |

### 4.2 Kart alanları (öneri)

- Status: Suggested \| PendingApproval \| Submitted \| Verified \| Failed \| Closed  
- Scope: SE vs Kasse  
- `beginn_ausfall` / `ende_ausfall` (Vienna display)  
- Begründung  
- OutboxId → `/rksv/finanz-online-outbox?outboxId=`  
- Actions: Approve & send, Retry, Mark manual (portalda yapıldı), Cancel suggestion  

### 4.3 İzinler

- Görüntüleme: `finanzonline.view` veya TSE admin  
- Gönder / onay: `finanzonline.submit` (+ isteğe bağlı dual Super Admin Production’da)

i18n: `tseAusfall.*` (de/en/tr). Hardcoded string yok.

---

## 5. Operatör dokümantasyonu (runbook)

### 5.1 Ne zaman FON’a bildirim gerekir?

Compliance checklist (örnek — **onaylanmalı**):

1. İmza birimi (SCU) uzun süre imza üretemiyor ve yasal Ausfall süresi aşıldı.  
2. Planlı bakım / kart değişimi (Ausfall → sonra Wiederinbetriebnahme).  
3. Kasa kalıcı kapatma → `ausserbetriebnahme` (Schlussbeleg akışından ayrı/ortak netleştirme).  
4. Kısa ağ kesintisi + offline kuyruk limit içinde → genelde **FON Ausfall yok** (iç incident yeterli).

### 5.2 Otomatik öneri görüldüğünde

1. FA → **TSE Ausfall** listesi (veya Activity “Enqueue suggested”).  
2. Cihaz, sertifika serisi, `beginn_ausfall` doğru mu kontrol et.  
3. Begründung seç.  
4. **Approve & send** → outbox Pending.  
5. `/rksv/finanz-online-outbox` durumunu izle (retry / dead-letter).  
6. BMF TEST/PROD return code’u Verified değilse incident aç; portalden manuel düzelt.

### 5.3 Manuel tetikleme (FA)

1. `/admin/tse-management` → cihaz seç.  
2. **Ausfall melden** → form: SE/Kasse, Begründung, Beginn (varsayılan: tespit UTC).  
3. Onay modalı (Production’da güçlü uyarı).  
4. Gönder → outbox.  
5. İyileşme sonrası **Wiederinbetriebnahme** → `ende_ausfall` ≥ beginn.

### 5.4 Manuel tetikleme (FinanzOnline portal — fallback)

1. [FinanzOnline](https://finanzonline.bmf.gv.at/) giriş.  
2. Registrierkassen / güvenlik birimi menüsü (Handbuch güncel yolu).  
3. Ausfall / Wiederinbetriebnahme formunu doldur.  
4. FA’da ilgili episode’u **Mark manual (portal)** ile kapat; kanıt notu + zaman damgası.

### 5.5 İzleme

| Soru | Nerede |
|------|--------|
| Gönderildi mi? | Outbox + episode status |
| Retry? | Outbox AttemptCount / NextAttemptAt |
| İç failover oldu mu? | `/admin/tse/failover` + Activity |
| Correlation soruşturma | `/rksv/incident?correlationId=` |

### 5.6 Yapılmaması gerekenler

- Demo/Soft ortamda “Verified” sanmak.  
- Aynı `beginn_ausfall` + sertifika için çift enqueue (BusinessKey).  
- Wiederinbetriebnahme’yi Ausfall’sız göndermek (FON reddi riski).  
- Secret/PIN loglamak.

### 5.7 Rollback

- Yanlış Ausfall: Compliance + BMF süreç; yazılımda “cancel suggested” yalnızca henüz gönderilmemiş kayıtlarda.  
- Outbox DeadLetter: düzelt payload → manuel re-enqueue (idempotent key dikkat).

---

## 6. Uygulama kırılımı (P0-3)

| Faz | İş | Rol | İG |
|-----|-----|-----|-----|
| 0 | Compliance: eşik, Begründung listesi, auto vs approve | Compliance | 1–2 |
| 1 | Episode entity + migration + DTOs | Backend | 2 |
| 2 | XML builder + mapper + outbox types + handler | Backend | 3–4 |
| 3 | Hooks (failover notification + health debounce + manual API) | Backend | 2–3 |
| 4 | FA liste/kart/aksiyonlar + i18n | Frontend | 3–4 |
| 5 | Simulation testleri + BMF TEST (P0-1 sonrası) + bu runbook’u ops finalize | Backend / Ops | 2–3 |

**Toplam:** ~12–16 İG.

### Kabul kriterleri

- [x] En az `ausfall_se` + `wiederinbetriebnahme_se` XML + outbox handler (simulation/unit).  
- [x] Failover activated → Suggested veya Auto enqueue (config).  
- [x] Demo/Soft → FON enqueue yok.  
- [x] FA’da status + manuel tetik + outbox link (`/admin/tse/ausfall`).  
- [ ] Operatör runbook (§5) Ops tarafından imzalı.  
- [ ] BMF TEST’te bir Ausfall + Wiederinbetriebnahme round-trip (P0-1 sonrası).

---

## 7. Özet kararlar

| Soru | Karar |
|------|--------|
| Yasal kanal | FON **rkdb** `ausfall_*` / `wiederinbetriebnahme_*` (+ portal fallback) |
| Mevcut tespit | Failover/health **yakalanır**; FON’a **bağlı değil** — hook eklenir |
| Outbox? | **Evet** — yeni message type + handler; transport reuse |
| FA UI? | **Evet** — TSE Ausfall listesi + outbox + incident link |
| İlk sürüm auto? | Varsayılan **onaylı öneri**; full auto Compliance sonrası |

---

**Son güncelleme:** 2026-07-29 — P0-3 **kod tamamlandı** (`rksv_ausfall_episodes`, XML builder, failover hooks, FA). BMF E2E Ops’ta açık.
