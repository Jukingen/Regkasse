# Mayıs 2027 Signaturkarte Zorunluluğu — Tasarım Planı (P1-3)

**Tarih:** 2026-07-29  
**Aksiyon:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P1-3** (~5–8 İG); runbook örtüşmesi: **P1-4**  
**İlgili:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md), [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5c, `TseCertificateService`

> Bu plan **operasyonel program** tasarımıdır. Kesin yasal metin / BMF duyurusu Compliance tarafından doğrulanmalıdır; tarih config ile ayarlanabilir tutulur. Soft/Demo cihazlar raporlarda “uygunsuz / hariç” olarak işaretlenir.
>
> **Durum (2026-07-29):** ✅ Implemented — `SignaturkarteProgram` config, `TseDevices` compliance columns, daily reminder hosted service, FA `/admin/tse/signaturkarte-program` + layout banner (expiry’den ayrı).

---

## 1. Zorunluluk (expiry’den bağımsız)

### 1.1 İki saat dilimi

| Saat | Kaynak | Anlam |
|------|--------|--------|
| **Sertifika `ExpiresAt`** | X.509 / `TseDevice.ExpiresAt` | Teknik süre dolumu; mevcut `ProcessExpiryWarningsAsync` (30 gün varsayılan) |
| **Program deadline `Mai2027`** | Operasyonel / düzenleyici hedef | Tüm production Signaturkarte / SCU’ların **yenilenmiş / değiştirilmiş** olması gereken son tarih |

Bunlar **birbirinin yerine geçmez**:

- Kart `ExpiresAt` = 2028 olsa bile Mayıs 2027 programı “değiştirildi” kanıtı isteyebilir.  
- Kart `ExpiresAt` = 2026-12 ise hem expiry hem 2027 programı tetiklenir (önce teknik yenileme).

### 1.2 Sabit (config)

```json
"SignaturkarteProgram": {
  "Enabled": true,
  "DeadlineUtc": "2027-05-31T21:59:59Z",
  "DisplayName": "Mai 2027 Signaturkarte",
  "ReminderDaysBefore": [180, 90, 30, 7],
  "ExcludeDemoAndSoftDevices": true,
  "RequireExplicitComplianceFlag": true
}
```

- **Deadline:** Varsayılan **2027-05-31** Vienna gün sonu → UTC’ye normalize (Compliance kesin günü onaylar: ay başı / ay sonu).  
- UI’da “Mai 2027” etiketi; teknik karşılaştırma `DeadlineUtc`.

### 1.3 “Yenilendi / uyumlu” tanımı

Cihaz **program-uyumlu** sayılır (öneri — Compliance onaylar):

1. `TseDevice` active + Production fiscal path (`TseMode=Device`, `Mode=Real`, Provider ≠ soft/fake), **ve**  
2. En az biri:  
   - `SignaturkarteProgramCompliantAtUtc != null` **ve** `>=` program başlangıç kesiti (Ops işaretledi / renew sync sonrası otomatik), **veya**  
   - Yeni sertifika `IssuedAt >= ProgramEpochUtc` (ör. 2026-06-01’den sonra basılmış kart — politika), **veya**  
   - Vendor ticket / audit notu ile Super Admin `MarkCompliant`.

İlk sürümde **açık bayrak** (`CompliantAtUtc` + actor) en güvenlisi; otomatik IssuedAt kuralı ikinci faz.

Yeni kolon(lar) (additive migration):

- `tse_devices.signaturkarte_program_compliant_at_utc` (nullable)  
- `tse_devices.signaturkarte_program_compliant_by` (nullable string)  
- opsiyonel: `signaturkarte_program_note`

---

## 2. Uyarı sistemi (milestone hatırlatmalar)

### 2.1 Kalıp

License / grace milestone modelini yeniden kullan (`GracePeriodReminderMilestones`, `LicenseReminderHostedService`):

| Bileşen | Rol |
|---------|-----|
| `SignaturkarteProgramMilestones` | `ReminderDaysBefore` → bugün eşleşiyor mu? |
| `ISignaturkarteProgramReminderService` | Günlük job: due milestone → Activity + email |
| Hosted service | Mevcut `LicenseReminderHostedService` yanına veya paylaşılan scheduler |
| Dedup | `signaturkarte-program:{deadline:yyyyMMdd}:{days}:{scope}` |

### 2.2 Milestone’lar (istenen)

| Kala | Gün (deadline’a) | Severity | Kime |
|------|------------------|----------|------|
| 6 ay | 180 | Info / Warning | Super Admin (+ Ops email list) |
| 3 ay | 90 | Warning | Super Admin + etkilenen Mandanten-Admin |
| 1 ay | 30 | Warning | Aynı + tenant bazlı sayılar |
| 1 hafta | 7 | Critical | Aynı; FA banner zorunlu |

Ek (opsiyonel): deadline günü `0`, deadline sonrası `Overdue` (Critical, günlük digest).

### 2.3 Kanallar

1. **Activity feed** — yeni tipler:  
   `SignaturkarteProgramReminder` (170+ aralığında yeni enum değerleri), metadata: `{ deadlineUtc, daysRemaining, nonCompliantDeviceCount, tenantId? }`  
2. **Email** — Super Admin dağıtım listesi + Mandanten-Admin (tenant’ta non-compliant cihaz varsa); composer: License reminder stili, secret yok.  
3. **FA banner** — §3.  
4. **Audit** — `SIGNATURKARTE_PROGRAM_REMINDER_SENT` (tenant/platform).

### 2.4 Kapsam kuralları

| Rol | Ne alır? |
|-----|----------|
| **Super Admin** | Platform özeti: X tenant / Y cihaz non-compliant |
| **Mandanten-Admin (`Manager`)** | Yalnızca kendi tenant cihazları |
| Soft/Demo / `TseMode=Off` | Sayım dışı (`ExcludeDemoAndSoftDevices`) |

### 2.5 Sertifika expiry ile ilişki

- `TseCertificateExpiringSoon` **ayrı** kalır (ExpiresAt).  
- Program reminder metni: *“Mai 2027 Signaturkarte-Pflicht — unabhängig vom Zertifikatsablauf.”*  
- FA’da iki rozet yan yana karışmasın: `Expires` vs `Program 2027`.

---

## 3. FA Banner / Widget (önerilen — P1-3’te dahil)

### 3.1 Banner (layout / RKSV hub)

Koşul: `Enabled && now < Deadline+grace && NonCompliantCount > 0` (veya Super Admin her zaman özet görür).

| Days remaining | UI |
|----------------|-----|
| \> 90 | İnce info Alert (dismissible 7 gün localStorage) |
| 30–90 | Warning Alert, dismiss yok (session) |
| ≤ 7 veya overdue | Critical Alert, sticky; link “Compliance report” |

i18n: `signaturkarteProgram.banner.*` (de/en/tr).

### 3.2 Widget (opsiyonel ama düşük maliyet)

- **`/admin/tse-management`** üst kart: countdown + non-compliant / total.  
- Super Admin dashboard mini-stat: `Mai 2027: 12 open`.  
- Link: `/admin/tse/signaturkarte-program` (rapor sayfası).

### 3.3 API

```text
GET /api/admin/tse/signaturkarte-program/status
→ { deadlineUtc, daysRemaining, totals: { compliant, nonCompliant, excluded }, milestonesNext }
```

Permission: Super Admin `system.critical`; Mandanten: kendi tenant özeti (`settings.view` / TSE view).

---

## 4. Raporlama

### 4.1 Rapor sayfası / export

**Rota:** `/admin/tse/signaturkarte-program` (Super Admin); Mandanten: `/settings/tse` veya tse-management filtresi.

| Kolon | Açıklama |
|-------|----------|
| Tenant | slug / name (SA only) |
| DeviceId / Serial | TSE cihaz |
| Provider | fiskaly / … |
| Certificate thumbprint / serial | kısa |
| ExpiresAt | teknik expiry |
| ProgramCompliantAt | null → **Open** |
| Status | Compliant \| Open \| Excluded (Demo/Soft) \| Revoked |
| Days to deadline | sayı |
| Actions | Mark compliant, Open renew runbook, Schedule renewal |

### 4.2 API

```text
GET /api/admin/tse/signaturkarte-program/devices?status=Open&tenantId=
POST /api/admin/tse/signaturkarte-program/devices/{id}/mark-compliant  { note }
GET /api/admin/tse/signaturkarte-program/export.csv
```

CSV/Excel: Ops haftalık review için. Audit her mark-compliant.

### 4.3 Özet metrikler

- % compliant (production devices)  
- Open by tenant (Top N)  
- Expiring before deadline ∩ Open (çift risk)  
- Trend: haftalık compliant delta (opsiyonel activity snapshot)

### 4.4 Mevcut servisle birleşim

`TseCertificateService.GetCertificateInfoAsync` / fleet overview’a `programCompliant` alanı eklenir; ayrı “yenile” hâlâ P1-4 runbook (fiskaly sync).

---

## 5. Operatör runbook / kontrol listesi

### 5.1 Program sahipliği

| Rol | Görev |
|-----|--------|
| **Compliance** | Deadline tarihi, “compliant” tanımı, zorunluluk metni |
| **Ops** | Vendor (fiskaly) kart değişim prosedürü, SCU id rotasyonu |
| **Super Admin** | Platform rapor, hatırlatma alıcıları |
| **Mandanten-Admin** | Kendi cihazlarını yeniletme / randevu |

### 5.2 Kontrol listesi (tenant başına)

- [ ] Production’da Soft/Demo TSE yok (`TSE_PRODUCTION_CONFIG_LOCK`)  
- [ ] Her aktif SCU için güncel fiskaly/A-Trust kart siparişi / değişim tarihi  
- [ ] Yeni sertifika cihaz kaydına sync (`RenewCertificateAsync` / provision)  
- [ ] FA’da **Mark compliant** + not (ticket no)  
- [ ] Startbeleg / FON kayıt gerekip gerekmediği (kart değişimi sonrası — Compliance)  
- [ ] DEP / imza zinciri smoke (1 test beleg)  
- [ ] Backup / TSE DR notu güncellendi  

### 5.3 Zaman çizelgesi (öneri)

| Dönem | Aksiyon |
|-------|---------|
| **≤ 2026-11** (≈ 6 ay kala) | Envanter raporu; vendor kapasite; ilk Super Admin mail |
| **2027-02** (≈ 3 ay) | Tüm Open tenant’lara Mandanten mail; haftalık SA review |
| **2027-04** (≈ 1 ay) | Kritik banner; günlük Open listesi; escalation |
| **2027-05 son hafta** | War room; yalnızca Open kalanlar |
| **Deadline sonrası** | Overdue Critical; yeni fiscal enablement politikası (opsiyonel gate — Compliance) |

### 5.4 Kart değişimi teknik adımlar (özet — P1-4 ile birleşir)

1. Vendor portalda yeni Signaturkarte / SCU.  
2. Config: yeni `SignatureCreationUnitId` / cert material (secret store).  
3. FA: Renew / sync metadata → `ExpiresAt` / thumbprint güncel.  
4. İmza smoke + isteğe bağlı FON güncelleme.  
5. Mark program-compliant.  
6. Eski kart güvenli imha / vendor iade.

### 5.5 İletişim şablonu (konu satırı örneği)

`[Regkasse] Mai 2027 Signaturkarte — noch {N} Geräte offen (Deadline {date})`

Gövde: sayılar, rapor linki, runbook linki; **secret yok**.

---

## 6. Uygulama kırılımı

| Faz | İş | Rol | İG |
|-----|-----|-----|-----|
| 1 | Config + migration (`CompliantAt`) + status DTO | Backend | 1–1.5 |
| 2 | Milestone service + hosted job + Activity/email + tests | Backend | 2–2.5 |
| 3 | Report API + CSV + mark-compliant | Backend | 1 |
| 4 | Banner + rapor sayfası + i18n | Frontend | 1.5–2 |
| 5 | Runbook finalize + Compliance tarih onayı | Ops / Compliance | 0.5–1 |

**Toplam:** ~5–8 İG (P1-3). P1-4 (vendor runbook detayı + fleet expiry UI) ayrı ~4–6 İG; ortak FA yüzeyleri paylaşılabilir.

### Kabul kriterleri

- [ ] Deadline config ile okunur; expiry uyarılarından ayrı milestone mail/activity  
- [ ] Super Admin platform raporu; Mandanten tenant filtresi  
- [ ] Mark compliant auditle kalıcı  
- [ ] FA banner milestone’lara göre şiddetlenir  
- [ ] Demo/Soft hariç  
- [ ] Bu doküman Ops checklist olarak imzalı  

---

## 7. Özet kararlar

| Soru | Karar |
|------|--------|
| Expiry’den bağımsız mı? | **Evet** — ayrı program deadline + compliant bayrağı |
| Hatırlatma | 180 / 90 / 30 / 7 gün — Activity + email (+ overdue) |
| FA banner? | **Evet** (P1-3 kapsamında) |
| Rapor | Cihaz listesi + CSV + % compliant |
| Runbook | §5 checklist + zaman çizelgesi |

---

**Son güncelleme:** 2026-07-29 — P1-3 Mayıs 2027 Signaturkarte program tasarımı.
