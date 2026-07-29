# RKSV Final Validation Checklist (Sign-off)

**Tarih:** 2026-07-29  
**Amaç:** P0–P2 kod iyileştirmeleri sonrası **operasyonel / BMF** doğrulama ve üretim sign-off.  
**Kaynak:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) · [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md)

> Bu checklist **yasal sertifika** değildir. Her madde için **kanıt** (ekran görüntüsü, log, ticket, CI run URL, cutover formu) ekleyin. İmza: Ops + Compliance (+ Backend lead isteğe bağlı).

---

## 1. Kod yüzeyi (referans — 2026-07-29)

| Paket | Kod durumu | Detay doküman |
|-------|------------|---------------|
| P0-1 Sonderbeleg SOAP | ✅ | `FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md` |
| P0-2 TSE Production Lock | ✅ | `TSE_PRODUCTION_CONFIG_LOCK.md` |
| P0-3 Ausfallmeldung | ✅ | `AUSFALL_BENACHRICHTIGUNG_PLAN.md` |
| P1-1 Monatsbeleg NotRequired | ✅ | `MONATSBELEG_FINANZONLINE_DECISION.md` |
| P1-2 Enqueue Mode | ✅ | Action plan |
| P1-3 Mayıs 2027 | ✅ | `MAI_2027_SIGNATURKARTE_PLAN.md` |
| P1-4 Signaturkarte runbook/fleet | ⬜ Açık | — |
| P2-1 DEP Prüftool CI | ✅ | `DEP_EXPORT_DEVELOPMENT.md`, `dep-prueftool.yml` |
| P2-2 Legacy JWS uyarı | ✅ | `DEP_EXPORT_DEVELOPMENT.md` § Legacy JWS |
| P2-3 Empty Signaturzertifikat | ✅ | `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` |

---

## 2. Validation checklist (işaretlenecek)

### P0 — Üretim güvenliği & FON

- [ ] **TSE Production Lock:** Production (veya Staging prod-like) ortamında Soft TSE / `TseMode=Off` / Fake signing **engelleniyor**; `/health/tse/mode` beklenen sonucu veriyor; FA “demo fiscal” banner’ı yalnızca uygun olmayan modda görünüyor.  
  - **Kanıt:** health JSON + config snippet (secrets maskeli) + FA screenshot  
  - **Sahip:** Ops + Backend

- [ ] **SOAP Sonderbeleg (BMF TEST):** Startbeleg ve Jahresbeleg gerçek **BMF TEST** `belegpruefung` hattına gidiyor; outbox → Verified (veya kabul edilen terminal durum); Fake client Production’da kullanılmıyor.  
  - **Kanıt:** outbox satır ID’leri, FO yanıt özeti, `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` TEST bölümü imzalı  
  - **Sahip:** Ops + Compliance (+ Backend)

- [ ] **Ausfallmeldung:** TSE failover / down senaryosunda Ausfall episode veya FA `/admin/tse/ausfall` **önerisi** görünüyor; (politikaya göre) outbox enqueue veya manuel gönderim yolu doğrulanmış.  
  - **Kanıt:** failover drill notu + FA screenshot / episode ID  
  - **Sahip:** Ops + Compliance

### P1 — Uyumluluk politikası

- [ ] **Mayıs 2027:** Super Admin banner / program sayfası (`/admin/tse/signaturkarte-program`) görünüyor; milestone reminder (activity/email) test ortamında tetiklenebiliyor.  
  - **Kanıt:** FA screenshot + reminder log/activity  
  - **Sahip:** Compliance + Ops

- [ ] **Monatsbeleg:** Ayrı FON outbox **yok** (NotRequired); FA Sonderbelege’de `MonatsbelegInfoCard` + doküman [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md); Aralık → Jahresbeleg yolu çalışıyor.  
  - **Kanıt:** FA screenshot + (isteğe bağlı) Aralık Jahresbeleg FO satırı  
  - **Sahip:** Compliance + Frontend spot-check

### P2 — DEP kalite

- [ ] **DEP Prüftool CI:** `.github/workflows/dep-prueftool.yml` son `main`/`PR` koşusu **yeşil** (fixture `-UseFixtures` + `Category=DepPrueftool`).  
  - **Kanıt:** GitHub Actions run URL  
  - **Sahip:** Backend / Ops

- [ ] **Legacy JWS:** Pre-F5 (JSON payload) imza içeren bir export’ta FA uyarı Alert’i ve/veya envelope `legacyJwsCount` > 0; history “Prüftool-kompatibel: Nein”.  
  - **Kanıt:** FA screenshot veya API envelope JSON  
  - **Sahip:** Backend + Frontend spot-check

- [ ] **Empty certificate hard-fail:** Bilinen eksik thumbprint grubunda DEP export **HTTP 500** `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` (veya servis exception); boş `Signaturzertifikat` JSON’da **yok**.  
  - **Kanıt:** API hata gövdesi veya unit test CI yeşili + manuel negatif test notu  
  - **Sahip:** Backend

### Opsiyonel / kalan

- [ ] **P1-4** Signaturkarte yenileme runbook + fleet “X gün” + i18n  
- [ ] **BMF PROD** Start/Jahres cutover (`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` PROD bölümü)  
- [ ] **`ai/05_SECURITY_COMPLIANCE.md`** cutover sonrası satır güncellemesi  

---

## 3. Go / No-Go önerisi (2026-07-29)

### Yazılım / platform (P0–P2 kod)

| Karar | **KOŞULLU GO** |
|-------|----------------|
| Anlam | Ürün kodu RKSV çekirdeği + P0–P2 iyileştirmeleri için **merge/release adayıdır**; staging’de doğrulamaya hazırdır. |
| Koşullar | Soft TSE prod’da kapalı kalmalı; FON ClientKind=Real + credentials; Ausfall politikası operatörlere iletilmiş olmalı. |

### Tam RKSV üretim / resmi “üretim hazır” iddiası

| Karar | **NO-GO** |
|-------|-----------|
| Anlam | Bugün **Betriebsprüfung / “tam FON üretim uyumlu”** iddiası için imza verilmemelidir. |
| Engeller | (1) BMF TEST Start/Jahres E2E kanıtı eksik veya bu checklist’te işaretlenmemiş; (2) canlı Ausfall drill eksik; (3) prod TSE kilidi Ops tarafından imzalanmamış; (4) P1-4 runbook açık; (5) BMF PROD cutover ayrıca gerekir. |

### Ne zaman tam **GO**?

Bölüm 2’deki **zorunlu** maddeler (TSE Lock, SOAP BMF TEST, Ausfall drill, Mayıs 2027, Monatsbeleg, DEP CI, Legacy JWS, Empty cert) **hepsi işaretli + kanıtlı** ve Compliance + Ops imzalı olduğunda:

1. Bu belgeye **GO — Production candidate (FON TEST validated)** yazın.  
2. PROD cutover için `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` ayrı imzalanır → **GO — Production FON**.  
3. [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5 Sonuç tablosunu güncelleyin.

---

## 4. Sign-off

| Rol | Ad | Tarih | İmza / onay |
|-----|-----|-------|-------------|
| **Ops** | | | ☐ |
| **Compliance** | | | ☐ |
| **Backend lead** (isteğe bağlı) | | | ☐ |
| **Product / Super Admin** (isteğe bağlı) | | | ☐ |

**Karar kutusu (işaretleyin):**

- [ ] **NO-GO** — üretim iddiası yok; yalnızca kod/staging devam  
- [ ] **KOŞULLU GO** — yazılım OK; BMF TEST + drill tamam; PROD FON henüz değil  
- [ ] **GO — Production candidate** — Bölüm 2 zorunlu maddeler + Ops/Compliance imzalı  
- [ ] **GO — Production FON** — + PROD cutover checklist imzalı  

---

**Son güncelleme:** 2026-07-29 — ilk final validation checklist (P0–P2 kod kapanışı sonrası).
