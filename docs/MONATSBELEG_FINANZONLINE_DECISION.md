# Monatsbeleg → FinanzOnline — Karar (P1-1)

**Tarih:** 2026-07-29  
**Aksiyon:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P1-1**  
**Durum:** ✅ **NotRequired** — Ocak–Kasım Monatsbeleg için ayrı FON `belegpruefung` / outbox **yok**  
**Uyarı:** Bu belge Compliance + BMF birincil kaynaklarına dayanan **ürün kararıdır**; hukuki danışmanlık değildir. Resmî metinler çelişirse onlar geçerlidir.

---

## 1. Soru

Monatsbeleg, Startbeleg/Jahresbeleg gibi FinanzOnline’a (rkdb `belegpruefung` / Belegcheck) **ayrıca** gönderilmeli midir, yoksa Aralık Monatsbeleg = Jahresbeleg yolu yeterli midir?

---

## 2. Araştırma özeti (BMF / WKO / RKSV)

| Kaynak | İlgili nokta |
|--------|----------------|
| **RKSV § 8 Abs. 3** (WKO özeti) | Her takvim yılı sonunda, yıl sonu sayacını içeren **Monatsbeleg (Jahresbeleg)** basılmalı, **kontrol edilmeli** ve § 132 BAO’ya göre saklanmalı. |
| [WKO — Prüfung des Jahresbelegs](https://www.wko.at/steuern/pruefung-jahresbeleg-registrierkasse) | **Startbeleg** ve **Jahresbeleg** için FON Belegcheck zorunlu; süre genelde **izleyen yıl 15 Şubat**. Kassensystem webservice ile otomatik gönderebilir. |
| [BMF Handbuch Registrierkassen](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf) | Yıl sonunda Monatsbeleg (= Jahresbeleg) oluşturulup kontrol edilir. Belegcheck App / Webservice genel Belegprüfung için kullanılabilir; **aylık Monatsbeleg’lerin her birinin FON’a zorunlu gönderimi** metinde Jahresbeleg/Startbeleg kadar bağlayıcı yazılmaz. |
| Operatör özetleri (ör. şube/kasa rehberleri) | Monatsbeleg: **oluştur + DEP’te sakla**; FON sınavı **önerilir**, **Jahresbeleg için zorunlu**. |

**Teknik eşdeğerlik:** Regkasse’de Aralık Monatsbeleg isteği zaten **Jahresbeleg** üretim yoluna yönlendirilir (`RksvSpecialReceiptService`); Jahresbeleg FON outbox’ta izlenir.

---

## 3. Karar (Compliance ürün politikası)

| Tür | Kasa içi üretim (TSE imza + DEP) | FinanzOnline Belegcheck / rkdb `belegpruefung` outbox |
|-----|----------------------------------|------------------------------------------------------|
| **Monatsbeleg** (Ocak–Kasım) | ✅ Zorunlu (RKSV aylık kontrol) | ❌ **NotRequired** — ayrı otomatik gönderim yok |
| **Jahresbeleg** (= Aralık Monatsbeleg) | ✅ | ✅ Startbeleg ile aynı hat (`RksvJahresbelegSubmission`) |
| **Startbeleg** | ✅ | ✅ |

**Gerekçe:** Yasal olarak bağlayıcı FON Belegprüfung, uygulamada **Startbeleg** ve **Jahresbeleg** için netleştirilmiştir. Aylık Monatsbeleg’ler DEP bütünlüğü ve işletme denetimi için üretilir; her ayı FON’a göndermek **ek bir Regkasse yükümlülüğü olarak uygulanmaz**. Aralık dönemi Jahresbeleg üzerinden karşılanır.

**Manuel seçenek:** Operatör isterse herhangi bir Monatsbeleg QR’ını BMF Belegcheck App ile kontrol edebilir; bu Regkasse outbox’ına yazılmaz.

**Yeniden değerlendirme tetikleri:** BMF/RKSV metin değişikliği; Mandanten vergi danışmanı talebi; Compliance yazılı “tüm ayları gönder” politikası → o zaman P1-1 reverse: `SubmitMonatsbelegAsync` + outbox.

---

## 4. Uygulama yansıması

| Katman | Davranış |
|--------|----------|
| Backend | Monatsbeleg oluşturma **outbox enqueue etmez** (mevcut). `SubmitMonatsbelegAsync` → `RKS_MONATSBELEG_NOT_REQUIRED`. |
| FA Sonderbelege | `MonatsbelegInfoCard` — NotRequired + BMF/WKO linkleri |
| FA Fiş detayı | Monatsbeleg için FO “tracked” değil; bilgi notu |
| Assessment | P1-1 kapatıldı; risk satırı “NotRequired” olarak güncellendi |

---

## 5. Referanslar

- [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md)  
- [WKO Jahresbelegprüfung](https://www.wko.at/steuern/pruefung-jahresbeleg-registrierkasse)  
- [BMF Handbuch Registrierkassen (PDF)](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf)  
- [`docs/RKSV_CASH_REGISTER_OPERATIONS.md`](RKSV_CASH_REGISTER_OPERATIONS.md) §4.3  
- [`docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md`](RKSV_BMF_BELEGCHECK_WORKFLOW.md)

**Son güncelleme:** 2026-07-29 — P1-1 karar: **NotRequired** (ayrı Monatsbeleg FON outbox yok).
