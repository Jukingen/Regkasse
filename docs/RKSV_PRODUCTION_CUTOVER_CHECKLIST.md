# RKSV Production Cutover Checklist

**Tarih:** 2026-07-29  
**Amaç:** Bilinçli **simülasyon** ortamından (`Soft TSE` / `RKSV:Mode=Demo` / `FinanzOnline:UseSimulation=true`) **üretim fiskal** moda geçiş adımları.  
**Plan:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) · **Hazırlık:** [`RKSV_IMPLEMENTATION_READINESS.md`](RKSV_IMPLEMENTATION_READINESS.md)  
**FON ek detay:** [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)  
**TSE kilidi:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md)

> Cutover tamamlanmadan “RKSV üretim uyumlu” veya “BMF Verified production” iddiası **yapılmaz**.  
> Secrets bu dosyaya yazılmaz.

---

## 0. Önkoşullar (simülasyon fazı yeşil)

- [ ] **P0-S1** Simulation Mode indicator FA + POS (+ API/logs) canlı ve anlaşıldı  
- [ ] DEP export + (mümkünse) Prüftool CI yeşil  
- [ ] Mayıs 2027 program yüzeyi (P1-3) en azından banner/rapor  
- [ ] Ausfall episode/FA (P1-A) simülasyonda doğrulandı; **send kapalı** olduğu dokümante  
- [ ] Compliance + Ops cutover penceresi onaylı  

---

## 1. TSE / SCU (Soft TSE → gerçek)

- [ ] Soft TSE / Demo cihaz envanteri (hangi tenant/register)  
- [ ] Gerçek **Signaturerstellungseinheit** (ör. fiskaly) credential + `SignatureCreationUnitId`  
- [ ] `TseMode` Production değeri: Device / vendor (Demo/Off/Fake **yasak**)  
- [ ] `TseProductionOptionsValidator` / `/health/tse/mode` **fail-closed** beklenen sonucu veriyor  
- [ ] Escape hatch yok veya Compliance yazılı onayı var  
- [ ] Smoke: bir test ödemesi **gerçek** compact JWS + thumbprint stamp  

**Referans:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md)

---

## 2. RKSV uygulama modu

- [ ] `RKSV:Mode` (veya eşdeğeri) Demo → Production/Test politikasına göre  
- [ ] Simulation banner Production’da **kapanıyor** veya “Production” sinyeline dönüyor  
- [ ] Legal notice / DEP `IsDemo` bayrakları production metnine geçiyor  

---

## 3. FinanzOnline

- [ ] `FinanzOnline:UseSimulation=false` (hedef ortam)  
- [ ] Webservice kullanıcısı (tid/benid/pin) — önce **TEST**, sonra **PROD** (ayrı kapılar)  
- [ ] Kasa + SCU FON’da kayıtlı; AES / benutzerschlüssel doğru  
- [ ] `RksvSubmission` **ClientKind=Real** (Fake Production’da yasak)  
- [ ] Outbox Mode ambient = hedef ortam (TEST sabiti yok)  
- [ ] Startbeleg + Jahresbeleg **belegpruefung** E2E (önce TEST)  

**Referans:** [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)

---

## 4. Ausfallmeldung

- [ ] Episode + FA yolu simülasyonda doğrulandı  
- [ ] Compliance: auto-enqueue politikası (on/off) yazılı  
- [ ] Canlı Ausfall / Wiederinbetriebnahme gönderimi **açıldı** (gate kaldırıldı)  
- [ ] Staging failover drill → episode + (politikaya göre) outbox  

**Referans:** [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md)

---

## 5. DEP / Prüftool

- [ ] Production crypto material / register-özel anahtar süreci biliniyor  
- [ ] Leaf `Signaturzertifikat` hard-fail davranışı prod SCU ile smoke  
- [ ] Legacy JWS envanteri (varsa) biliniyor; Prüftool beklentisi yönetildi  
- [ ] CI `dep-prueftool` yeşil (regresyon kapısı)  

---

## 6. Operasyonel / iletişim

- [ ] Simulation banner kaldırıldı / Production mesajı  
- [ ] Mandanten / iç ekibe cutover duyurusu  
- [ ] Rollback planı (UseSimulation geri, Soft TSE yalnızca non-prod)  
- [ ] [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) zorunlu maddeler işaretli  
- [ ] [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5 Sonuç güncellendi  
- [ ] `ai/05_SECURITY_COMPLIANCE.md` cutover satırları (P2-4)  

---

## 7. Sign-off

| Rol | Ad | Tarih | Onay |
|-----|-----|-------|------|
| **Ops** | | | ☐ Soft TSE off + FON + SCU |
| **Compliance** | | | ☐ Politikalar + Ausfall send |
| **Backend lead** | | | ☐ Config + health |
| **Product** | | | ☐ Go-live |

**Ortam:** ☐ BMF TEST cutover tamam · ☐ BMF PROD cutover tamam  

---

**Son güncelleme:** 2026-07-29 — simulation-first P0-S2 teslimatı.
