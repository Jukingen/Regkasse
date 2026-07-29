# RKSV Uygulama Hazırlığı — Go / No-Go (Simulation-first)

**Tarih:** 2026-07-29  
**Faz:** **Simulation-first**  
**Ortam varsayımı:** Soft TSE / `TseMode=Demo` · `RKSV:Mode=Demo` · `FinanzOnline:UseSimulation=true` (bilinçli)

**Plan:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md)  
**Cutover:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)  
**Üretim sign-off:** [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md)  
**Assessment:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> Bu belge karar desteğidir; BMF sertifikası veya yasal onay yerine geçmez.  
> Simülasyonda Soft TSE **kabul edilir**; Production Soft TSE **cutover’da yasaklanır**.

---

## Executive decision

### **Karar: GO — Simulation phase** · **NO-GO — Production / BMF live**

| Paket (yeni ID) | Go/No-Go | Gerekçe |
|-----------------|----------|---------|
| **P0-S1** Simulation indicator | **GO — hemen** | Operatör false confidence riskini keser; Soft TSE ile uyumlu |
| **P0-S2** Production Cutover Checklist | **GO — hemen** | Soft TSE kapatma + Real SOAP + Ausfall açma tek kapıda toplanır |
| **P1-3** Mayıs 2027 | **GO — paralel** | Simülasyondan bağımsız; zaman baskılı |
| **P1-A** Ausfall episode + FA (no send) | **GO — simülasyonda** | Mekanik öğrenilir; canlı FON cutover’da |
| **P1-F** Mock/simüle FON Sonderbeleg | **GO — simülasyonda** | Gerçek SOAP cutover’a |
| **P2-1** DEP Prüftool CI | **GO — simülasyonda** | DEP Soft TSE ile test edilebilir |
| **P2-L** TSE Production Lock | **GO — tasarım; NO-GO zorla prod şimdi** | Soft TSE bu fazda kasıtlı; kilitleme cutover adımı |
| **P2-S / Cutover Real SOAP** | **GO — tasarım; NO-GO canlı gönderim şimdi** | `UseSimulation=true` iken gerçek BMF zorunlu değil |
| **Canlı Ausfall SOAP** | **NO-GO ta ki cutover** | Episode OK; send = cutover |

**Tek seferde “Production fiscal ready” ilanı:** **No-Go** — ortam bilinçli simülasyon.

**Minimum bu hafta:** P0-S1 kickoff + P0-S2 checklist sahipliği (Ops) + P1-3 devam.

---

## 1. Teknik fizibilite

### 1.1 Simulation-first prensipleri

1. **Görünürlük > erken prod kilidi** — Soft TSE açıkken net “Simülasyon” bandı zorunlu.  
2. **Mekanik simülasyonda, wire cutover’da** — outbox/episode/UI evet; gerçek BMF ağ çağrısı hayır (gate).  
3. **DEP kalitesi simülasyonda** — Prüftool CI Soft TSE fixture ile anlamlı.  
4. **Tek cutover kapısı** — Soft TSE kapat + Real SCU + `UseSimulation=false` + Real SOAP + Ausfall send.

### 1.2 Paket fizibilitesi

| Paket | Fizibilite | Zorluk | Not |
|-------|------------|--------|-----|
| P0-S1 Indicator | Yüksek | Düşük–Orta | `RKSV:Mode` / `TseMode` / `UseSimulation` birleşik bayrak; FA + POS i18n |
| P0-S2 Cutover doc | Yüksek | Düşük | Mevcut FON cutover + TSE lock docs birleştirilir |
| P1-3 Mai 2027 | Yüksek | Düşük–Orta | Bağımsız |
| P1-A Ausfall (no send) | Yüksek | Orta | Episode + FA; `AutoEnqueue`/send gate |
| P1-F Mock FON | Yüksek | Düşük–Orta | Fake client zaten var; yapılandırılabilir fail path netleştir |
| P2-1 DEP CI | Yüksek | Orta | JDK 17 + JAR ensure |
| P2-L Prod lock | Yüksek | Düşük | Kod/docs mevcut; **Production ValidateOnStart** cutover’da |
| Real SOAP (cutover) | Orta–Yüksek | Yüksek | BMF TEST credentials dış bağımlılık |

### 1.3 Bilinen uyum notları (repo)

- Soft TSE / Demo / `UseSimulation=true` **şu an bilinçli** — P0-S1 ile her yüzeyde işaretlenmeli.  
- Real SOAP istemci / TSE prod validator / Ausfall episodes **kodda bulunabilir**; bu fazda “canlı production fiscal” anlamına gelmez.  
- Cutover’da P2-L + Real SOAP + Ausfall send + Soft TSE kapatma birlikte doğrulanır.

---

## 2. Zaman ve kaynak

### 2.1 İş günü (simulation fazı odaklı)

| ID | İG (yaklaşık) |
|----|----------------|
| P0-S1 Simulation indicator | 3–5 |
| P0-S2 Cutover checklist (doküman + Ops walkthrough) | 2–3 |
| P1-3 Mayıs 2027 | 5–8 |
| P1-A Ausfall (no send) | 8–12 |
| P1-F Mock FON | 4–8 |
| P2-1 DEP CI | 3–5 |
| P2-2 / P2-3 | 3–6 |
| P2-S Real SOAP tasarım | 6–10 |
| **Simülasyon fazı ara toplam** | **~34–57 İG** |

**Cutover paketi** (ayrı; BMF credentials sonrası): Soft TSE kapatma + Real SOAP E2E + Ausfall send + prod lock — kabaca **+15–30 İG** (Ops/Compliance yoğun).

### 2.2 Kadro

| Rol | Simülasyon fazı |
|-----|-----------------|
| Backend 1 | Indicator, mock FON, Ausfall episode, DEP CI |
| FA 0.5 + POS 0.25 | Simulation banner |
| Ops 0.25 | Cutover checklist sahiplik |
| Compliance 0.25 | 2027 + “gönderim kapalı” politikası |

Takvim (paralel): **~6–10 hafta** simülasyon fazı; cutover ayrı pencere.

---

## 3. Riskler

| Risk | Etki | Azaltma |
|------|------|---------|
| Soft TSE “üretim gibi” algısı | Yasal / müşteri yanlış güven | **P0-S1** zorunlu banner + API `isSimulation` |
| Erken Real SOAP / Fake Verified | False confidence | Cutover’a kadar simülasyon client; Fake prod ban cutover’da |
| Auto-Ausfall simülasyonda bile yanlış gönderim | FON kirliliği | Send gate / `UseSimulation` / `AutoEnqueue=false` |
| Cutover checklist eksik | Soft TSE prod’da unutulur | **P0-S2** + Ops imza |
| 2027 gecikmesi | Operasyonel kriz | P1-3 paralel, simülasyondan bağımsız |
| DEP CI yok | Format regresyonu | P2-1 |

**En büyük üç risk (bu faz):** (1) görünür simülasyon sinyali eksikliği, (2) cutover’suz Soft TSE prod sızıntısı, (3) yanlış canlı FON gönderimi.

---

## 4. Öncelik sırası

```text
Faz S0:  P0-S1 (banner) + P0-S2 (cutover doc)
Faz S1:  P1-3 ∥ P1-A ∥ P1-F
Faz S2:  P2-1 ∥ P2-2 ∥ P2-3 ∥ P2-S (tasarım)
Faz C:   Production Cutover Checklist imzası
         → Soft TSE off, Real SCU, UseSimulation=false,
           Real SOAP, Ausfall send, P2-L ValidateOnStart
```

### Bilinçli No-Go

| Koşul | Etki |
|-------|------|
| Simulation banner olmadan Soft TSE’yi “prod ready” ilan | **No-Go üretim iddiası** |
| `UseSimulation=true` iken “BMF Verified production” pazarlama | **No-Go** |
| Cutover’suz Soft TSE Production deploy | **No-Go P2-L zorla açık bırakma** — ya Demo env ya kilitle |
| Compliance Ausfall send onaylamadan auto-send | **No-Go Ausfall send** |

---

## 5. Go / No-Go özeti (karar formu)

| Soru | Cevap |
|------|--------|
| Simülasyon fazı başlatılsın mı? | **GO** |
| Soft TSE bu fazda OK mi? | **Evet (bilinçli)** + **P0-S1 zorunlu** |
| Real BMF SOAP şimdi mi? | **Hayır — cutover** |
| Canlı Ausfall şimdi mi? | **Hayır — episode/UI evet, send cutover** |
| Mayıs 2027 şimdi mi? | **GO (P1-3)** |
| DEP CI şimdi mi? | **GO (P2-1)** |
| Production fiscal ready mi? | **NO-GO** ta ki cutover imzalı |

### İmza / onay

| Rol | Ad | Tarih | Karar |
|-----|-----|-------|-------|
| Engineering lead | | | Simulation GO / NO-GO |
| Ops | | | Cutover checklist sahibi: E / H |
| Compliance | | | Simülasyon politikası + 2027: E / H |
| Product | | | Kaynak: E / H |

---

## 6. İlk 10 iş günü (simulation backlog)

1. Birleşik `isSimulation` / demo bayrağı sözleşmesi (Backend)  
2. FA Simulation Mode banner (tüm korumalı yüzeyler)  
3. POS Simulation Mode indicator (kasa UI)  
4. API/health veya `/me` benzeri yanıtta `simulationMode` (opsiyonel ama tercih)  
5. Structured log: `FiscalMode=Simulation`  
6. Ops: [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) walkthrough  
7. P1-3 Mayıs 2027 MVP doğrulama / boşluk  
8. P1-A Ausfall episode list + FA, send disabled  
9. P1-F mock FON success/fail config  
10. P2-1 DEP Prüftool CI iskeleti (JDK 17 + `-UseFixtures`)

---

**Son güncelleme:** 2026-07-29 — **simulation-first** Go/No-Go; üretim kilidi ve canlı FON **cutover gate**.
