# RKSV Önceliklendirilmiş Aksiyon Planı

**Tarih:** 2026-07-29  
**Faz:** **Simulation-first** (bilinçli)  
**Ortam varsayımı:** `TseMode=Demo` / Soft TSE · `RKSV:Mode=Demo` · `FinanzOnline:UseSimulation=true`  
**Kaynak:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)  
**Cutover:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)  
**Sign-off (üretim):** [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md)

> Bu plan yasal sertifika iddiası değildir. **Simülasyon = geliştirme/test**; “yeşil” UI üretim BMF kabulü değildir.  
> Tahminler kabaca **iş günü (İG)** cinsindendir.

---

## 1. Bağlam: neden öncelikler değişti?

Mevcut çalışma ortamı **bilinçli simülasyondur**. Bu fazda:

| Yapılır | Yapılmaz (cutover’a kadar) |
|---------|----------------------------|
| Soft TSE / Demo imza, simüle FON yanıtları | Production’da Soft TSE yasağını “şimdi” zorunlu kılmak |
| Mock/simüle SOAP istemcisi (yapılandırılabilir başarı/hata) | Canlı BMF `belegpruefung` zorunlu kapanış |
| Ausfall episode + FA UI + outbox mekaniği (gönderim kapalı) | Gerçek Ausfall SOAP gönderimi |
| DEP export + Prüftool CI | “Üretim hazır” iddiası |
| Mayıs 2027 takip (simülasyondan bağımsız) | — |

**Önceki P0-1 / P0-2 / P0-3 (üretim kesici paketler)** bu fazda **P1/P2 veya cutover gate** olarak yeniden sınıflandırılır. Kodda zaten bulunan yüzeyler (SOAP Real istemci, TSE prod validator, Ausfall episodes) **simülasyonda kullanılabilir / cutover’da açılır**; canlı BMF/prod kilidi cutover checklist’ine bağlıdır.

---

## 2. Öncelik matrisi (simulation-first)

### P0 — Hemen (bu fazın kapısı)

| ID | Aksiyon | Tahmin (İG) | Sorumluluk | Not |
|----|---------|-------------|------------|-----|
| **P0-S1** | **Simulation Mode indicator** — FA + POS + backend log + seçilmiş API yanıtlarında net “Simülasyon / Demo — fiskal değil” sinyali | **3–5** | Backend, Frontend (FA+POS) | Yanlış “Verified / Production” algısını keser. Mevcut TSE/RKSV demo bayrakları tek `isSimulation` okuma modeline bağlanmalı. |
| **P0-S2** | **Production Cutover Checklist** — Soft TSE kapatma, gerçek SCU, `UseSimulation=false`, Real SOAP, Ausfall gönderim açma, FON kaydı, smoke testler | **2–3** | Ops, Compliance, Backend | ✅ Doküman: [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md). FON-spesifik ek: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md). |

**P0 ara toplam (simülasyon fazı):** ~**5–8 İG**.

### P1 — Yüksek (simülasyonda ilerler; canlı gönderim cutover’da)

| ID | Aksiyon | Tahmin (İG) | Sorumluluk | Not |
|----|---------|-------------|------------|-----|
| **P1-3** | **Mayıs 2027 Signaturkarte** programı (banner, liste, milestone) | **5–8** | Compliance, Ops, Backend, Frontend | Simülasyondan **bağımsız** — zaman baskılı. Kod yüzeyi mevcutsa doğrulama + boşluk kapatma. [`MAI_2027_SIGNATURKARTE_PLAN.md`](MAI_2027_SIGNATURKARTE_PLAN.md) |
| **P1-A** | **Ausfall episode + FA UI + outbox iskeleti** — gerçek FON gönderimi **kapalı** (`AutoEnqueue=false` / simülasyon gate) | **8–12** | Backend, Frontend, Compliance | Eski P0-3’ün simülasyon dilimi. Canlı Ausfall → cutover. [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md) |
| **P1-F** | **Simüle / mock Sonderbeleg FON istemcisi** — yapılandırılabilir success/fail; Fake prod yasağı tasarımı cutover’a | **4–8** | Backend | Eski P0-1’in simülasyon dilimi. Real SOAP **cutover paketi** (P1-C / P2 tasarım + cutover uygulama). |
| **P1-1** | Monatsbeleg FON **NotRequired** karar + FA notu | **1–2** (kalan) | Compliance, Frontend | ✅ Karar dokümanı mevcut; UI doğrula. [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md) |
| **P1-2** | Outbox Mode ambient/config (TEST sabiti yok) | **1–2** | Backend, Ops | Simülasyonda da doğru etiket. |
| **P1-4** | Signaturkarte runbook + fleet / i18n | **4–6** | Ops, Backend, Frontend | P1-3 sonrası |

**Eski üretim P0’ları (yeniden sınıflandırma):**

| Eski ID | Yeni yer | Gerekçe |
|---------|----------|---------|
| P0-1 Real SOAP + BMF E2E | **Cutover (P1-C) + P2 tasarım** | Simülasyonda gerçek SOAP yok |
| P0-2 TSE Production Lock | **P2-L + cutover** | Soft TSE bu fazda kabul; banner + cutover’da kilitle |
| P0-3 Canlı Ausfall gönderimi | **Cutover** | Episode/UI = P1-A; SOAP gönderim = cutover |

### P2 — Orta (kalite + cutover hazırlığı)

| ID | Aksiyon | Tahmin (İG) | Sorumluluk | Not |
|----|---------|-------------|------------|-----|
| **P2-1** | DEP Prüftool CI (`-UseFixtures` + seeded smoke) | **3–5** | Backend, Ops | Simülasyonda DEP çalışır; CI zorunlu. |
| **P2-2** | Pre-F5 legacy JWS uyarı | **2–4** | Backend, Frontend | Soft TSE legacy payload riski için önemli. |
| **P2-3** | Boş `Signaturzertifikat` hard-fail | **1–2** | Backend | Simülasyon export kalitesi. |
| **P2-L** | TSE Production Lock **tasarımı + cutover adımı** (validator / health) — simülasyonda soft TSE açık kalır | **3–5** | Backend, Ops | Eski P0-2 demote. Soft TSE kapatma = cutover. [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| **P2-S** | Real SOAP istemci **tasarımı / iskelet** — deploy ve BMF E2E **cutover’da** | **6–10** (tasarım) | Backend | Eski P0-1’in erken tasarımı; canlı gönderim yok. |
| **P2-4** | Cutover sonrası assessment / AI docs senkronu | **1–2** | Compliance, Backend | Cutover imzasından sonra. |

---

## 3. Önerilen sıra (simulation-first)

```text
Faz S0 (Hafta 1) — görünürlük + cutover belgesi
  P0-S1  Simulation Mode indicator (FA + POS + API/logs)
  P0-S2  Production Cutover Checklist (bu repo dokümanı + Ops sahiplik)

Faz S1 (Hafta 1–4) — simülasyonda uyumluluk
  P1-3   Mayıs 2027 (paralel, bağımsız)
  P1-A   Ausfall episode + FA (gönderim kapalı)
  P1-F   Mock/simüle FON Sonderbeleg client
  P1-1 / P1-2  kalan doğrulamalar

Faz S2 (Hafta 3–6) — DEP kalite
  P2-1   Prüftool CI
  P2-2 / P2-3  legacy JWS + empty cert
  P2-S   Real SOAP tasarım notları (implementasyon cutover’a)

Faz C — Production Cutover (ayrı kapı; S0–S2 yeşil + Compliance)
  Soft TSE / Demo / UseSimulation kapat
  Real SCU + Real SOAP + Ausfall gönderim aç
  P2-L kilidi Production’da ValidateOnStart
  FINANZONLINE + RKSV cutover checklist imzası
```

---

## 4. Rol matrisi

| Rol | Simülasyon fazı | Cutover |
|-----|-----------------|---------|
| **Backend** | Indicator API, mock FON, Ausfall episode (no send), DEP CI | Real SOAP, prod lock, Ausfall send |
| **Frontend FA/POS** | Simulation banner; Ausfall/2027 UI | Prod banner’lar; Soft TSE yokluğu |
| **Ops** | Cutover checklist sahipliği; sim env dokümantasyonu | Credentials, SCU, `UseSimulation=false` |
| **Compliance** | Mayıs 2027; Monatsbeleg NotRequired; Ausfall “gönderim kapalı” politikası | Canlı FON / Ausfall onayı |

---

## 5. İlerleme takibi

### P0 (simulation-first)

- [ ] P0-S1 Simulation Mode indicator (FA + POS + backend/API)
- [x] P0-S2 Production Cutover Checklist dokümanı ([`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md))

### P1

- [ ] P1-3 Mayıs 2027 (doğrulama / boşluk; kod varsa checklist)
- [ ] P1-A Ausfall episode + FA, submission disabled
- [ ] P1-F Mock/simüle Sonderbeleg FON client
- [x] P1-1 Monatsbeleg NotRequired karar
- [ ] P1-2 Mode ambient (doğrula)
- [ ] P1-4 Signaturkarte runbook + fleet/i18n

### P2

- [ ] P2-1 DEP Prüftool CI
- [ ] P2-2 Legacy JWS uyarı
- [ ] P2-3 Empty Signaturzertifikat hard-fail
- [ ] P2-L TSE Production Lock (cutover’da aktif)
- [ ] P2-S Real SOAP tasarım (deploy cutover’da)
- [ ] P2-4 Cutover sonrası docs

### Cutover gate (üretim)

- [ ] [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) Ops+Compliance imzalı
- [ ] [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) zorunlu maddeler

> Not: Repo’da daha önce üretim-öncelikli P0–P2 kod yüzeyi eklenmiş olabilir. Bu plan **simülasyon fazı önceliklerini** tanımlar; cutover’a kadar Soft TSE ve `UseSimulation=true` **bilinçli kabul**dür. Mevcut Real SOAP / prod lock / Ausfall kodu “erken teslimat” sayılır — **canlı BMF/prod açılışı** yine cutover’a bağlıdır.

---

**İlgili:** [`RKSV_IMPLEMENTATION_READINESS.md`](RKSV_IMPLEMENTATION_READINESS.md) · [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)

**Son güncelleme:** 2026-07-29 — **simulation-first** yeniden önceliklendirme (P0-S1/S2; eski P0-1/2/3 demote).
