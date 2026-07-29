# TSE Production Configuration Lock — Tasarım Önerisi (P0-2)

**Tarih:** 2026-07-29  
**Aksiyon:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P0-2** (~3–5 İG)  
**İlgili:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md), [`FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md`](FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md)

> Amaç: `ASPNETCORE_ENVIRONMENT=Production` iken Soft/Fake/Off TSE ile “yasalmış gibi” imza üretilmesini **fail-closed** engellemek.
>
> **Durum (2026-07-29):** ✅ Implemented — `TseProductionOptionsValidator` (`ValidateOnStart`), `TseFiscalConfigHealthCheck` (`/health/tse/mode`), escape hatch `Tse:AllowUnsafeFiscalModesInProduction`, FA banner via `GET /api/rksv/environment` lock fields.

---

## 1. Mevcut durum

### 1.1 İki ayrı eksen (`TseOptions`)

Kaynak: `backend/Models/TseOptions.cs` (`SectionName = "Tse"`).

| Özellik | Değerler | Anlam |
|---------|----------|--------|
| **`TseMode`** | `Off` \| `Demo` \| `Device` | Ödeme / QR politikası |
| **`Mode`** | `Fake` \| `Real` | Closing / provider imza backend’i (`ITseProvider`) |
| **`Provider`** | `fiskaly` \| `epson` \| `swissbit` \| `fake` \| `soft` | Real (veya soft) vendor |
| **`Environment`** | örn. `Production` / `Test` | Vendor API etiketi (informational) |

Yardımcılar:

- `IsOff` → `TseMode == Off` (TSE kapalı; `tseRequired` yok sayılır; NON_FISCAL QR)
- `UseSoftTseWhenNoDevice` → `TseMode == Demo` (cihaz yoksa Soft TSE)
- `IsFakeSigningMode` → `Mode == Fake` (donanımsız simüle JWS)
- `AllowSimulatedDailyClosing` → yalnızca fake/simüle provider ile daily closing’e izin (dev safety valve)

Varsayılanlar (kod): `TseMode=Device`, `Mode=Real`.  
Prod örnek (`appsettings.Production.example.json`): `TseMode=Device`, `Mode=Real`, `Provider=fiskaly`.

### 1.2 `TseMode` / `Mode` enum değil

Bunlar **string** alanlardır; ayrı bir C# `enum TseMode` yok. Büyük/küçük harf `OrdinalIgnoreCase` ile karşılaştırılır.

### 1.3 İkinci config yüzeyi: `RKSV:*`

`RksvEnvironmentService` ayrıca okur:

- `RKSV:Mode` (Demo / Production)
- `RKSV:TseMode` (`Simulation` → `IsTseSimulated() == true`)

DEP / FA “DEMO” etiketleri buradan gelir. **Production kilidi yalnızca `Tse:*` değil, `RKSV:TseMode=Simulation` ve `RKSV:Mode=Demo` için de uygulanmalıdır.**

### 1.4 Çalışma zamanı davranışı

| Bileşen | Rol |
|---------|-----|
| `PaymentService` | `effectiveTseRequired`; Off iken imza zorunluluğu düşer |
| `TseService` / `SignaturePipeline` | İmza üretimi |
| `ApplicationHost` DI | `IsFakeSigningMode` → `FakeTseProvider`; aksi halde Real + fiskaly/soft |
| `TseProvisioningService` | Off iken provision skip; Demo/Fake ile “hazır” sayılabilir |
| `TseCachedHealthCheck` | Cihaz probe cache; Offline → **Degraded** (LB ready’yi düşürmez) |

**Bugünkü boşluk:** Production’da `TseMode=Off` / `Demo` veya `Mode=Fake` yapılandırılırsa API **startup’ta reddetmez**; mali işlemler soft/fake imza veya imzasız yola düşebilir.

---

## 2. Kritik kontrol — Production’da Fake / Off (ve Demo) engeli

### 2.1 Önerilen birincil mekanizma: `IValidateOptions<TseOptions>` + `ValidateOnStart`

Mevcut kalıp: `BackupOptionsValidator` (`ValidateOnStart` → Unsafe config → **process start fail**).

```text
TseProductionOptionsValidator : IValidateOptions<TseOptions>
  + IHostEnvironment
  + IConfiguration (RKSV:* için)

host.IsProduction() && !AllowUnsafeFiscalModesInProduction?
  TseMode in { Off, Demo }           → Fail
  Mode == Fake                       → Fail
  Provider in { fake, soft }          → Fail (Device beklenirken)
  AllowSimulatedDailyClosing == true → Fail
  RKSV:TseMode == Simulation         → Fail
  RKSV:Mode == Demo                  → Fail (Production host)
  Provider=fiskaly && SCU/Api boş    → Fail (veya ayrı Unhealthy — aşağıda)
```

Kayıt (`ApplicationHost`, Backup ile aynı stil):

```csharp
services.AddSingleton<IValidateOptions<TseOptions>, TseProductionOptionsValidator>();
services.AddOptions<TseOptions>()
    .Bind(configuration.GetSection(TseOptions.SectionName))
    .ValidateOnStart();
```

**Neden startup validation (middleware değil)?**

| Yaklaşım | Artı | Eksi |
|----------|------|------|
| **ValidateOnStart** | Pod hiç traffic almaz; fail-closed; Backup ile tutarlı | Config hot-reload ile gevşetilemez (bilinçli) |
| Middleware | İstek bazlı engel | Yanlış config ile process ayakta kalır; kısmi endpoint sızıntısı riski |
| Yalnızca payment gate | Ödeme engellenir | Closing / Sonderbeleg / provision hâlâ soft yolda olabilir |

**Öneri:** Startup **zorunlu**; isteğe bağlı olarak payment path’te ikinci savunma (defense in depth) — zorunlu değil.

### 2.2 Ortam tanımı

- Tetikleyici: `IHostEnvironment.IsProduction()` (`ASPNETCORE_ENVIRONMENT=Production`).
- **Staging:** Varsayılan olarak Production ile **aynı sıkı kurallar** (öneri). İsterseniz `Tse:EnforceProductionLockInStaging=true` (default true).
- **Development / Test:** Soft/Demo/Fake serbest (mevcut testler bozulmasın).

### 2.3 Acil kaçış vanası (dar)

FON cutover’a benzer, **varsayılan kapalı**:

```json
"Tse": {
  "AllowUnsafeFiscalModesInProduction": false,
  "UnsafeFiscalModesApprovalToken": null
}
```

- `AllowUnsafeFiscalModesInProduction=true` **yalnızca** açık dual-approval token ile (Ops runbook); her startup’ta **Critical** log + Activity `TseUnsafeProductionModeEnabled`.
- Normal prod’da bu bayrak **asla** true olmamalı; checklist maddesi.

Middleware tek başına yeterli değildir; kaçış vanası bile ValidateOnStart içinde değerlendirilir.

---

## 3. Sağlık kontrolü (Readiness)

### 3.1 Bugünkü `/health/ready`

```csharp
Predicate = check => check.Tags.Contains(DatabaseHealthCheck.ReadyTag)
// Degraded → 200, Unhealthy → 503
```

TSE **dahil değil**. `TseCachedHealthCheck` (`deps` tag) cihaz Online/Offline cache’idir; Offline → Degraded → ready’yi düşürmez.

### 3.2 Öneri: config kilidi ≠ cihaz probe

| Endpoint | Ne kontrol eder | Prod Fake/Off |
|----------|-----------------|---------------|
| `/health/live` | Process ayakta | Etkilenmez |
| `/health/ready` | DB (mevcut) | Config ValidateOnStart ile zaten process yok |
| **`/health/tse/mode`** (yeni) | Fiscal config posture | Unhealthy (503) |
| `/health/tse` veya mevcut `tse` deps | Cihaz probe cache | Degraded (trafik kalsın; fiscal path adapte) |

**`/health/ready` içine config Unhealthy eklemek?**

- **Lehine:** Orchestrator yanlış image’ı trafik almadan keser (ValidateOnStart atlanırsa bile).  
- **Aleyhine:** Ready’yi şişirir; DB-only ready semantiği bozulur.

**Tasarım kararı (önerilen):**

1. Startup `ValidateOnStart` = asıl fail-closed.  
2. Yeni `TseFiscalConfigHealthCheck` (name: `tse-fiscal-config`) → **`/health/tse/mode`** (FON `/health/finanzonline/mode`, Backup `/health/backup/mode` ile paralel).  
3. `/health/ready`’ye **ekleme** (şimdilik) — isteğe bağlı sonraki faz: `ReadyTag` + Unhealthy only for config (cihaz Offline hâlâ Degraded).

Cihaz “geçici Offline” ready’yi **düşürmemeli** (mevcut `TseCachedHealthCheck` politikası korunur).

### 3.3 Readiness’te fiskaly kimlik bilgisi?

- Eksik `SignatureCreationUnitId` / ApiKey: startup’ta Fail **veya** `tse-fiscal-config` Unhealthy.  
- Öneri: Production + `Provider=fiskaly` → SCU id + key secret referansı zorunlu (değer loglanmaz).

---

## 4. Hata yönetimi

### 4.1 Katmanlı davranış

| Durum | Davranış |
|-------|----------|
| Production + yasaklı mode, normal bayraklar | **Startup abort** (`OptionsValidationException`); container CrashLoop → deploy rollback |
| Production + kaçış vanası açık | Start **izin**; Critical log + audit/activity; `/health/tse/mode` = **Degraded** (veya Unhealthy policy’ye göre) |
| Development + Off/Demo/Fake | Normal; log Information |
| Runtime’da options değişimi (nadir) | `IOptionsMonitor` + health check bir sonraki probe’da yakalar; hot-reload ile Production’da unsafe’e geçiş **Validate** ile engellenmeli |

### 4.2 Loglama (English, no secrets)

```text
Critical: TSE production lock rejected TseMode={TseMode} Mode={Mode} Provider={Provider} RksvTseMode={RksvTseMode}
```

- ApiKey / ApiSecret / raw PEM asla loglanmaz.  
- Structured: `EventId` sabit (ör. `TseProductionConfigRejected`).

### 4.3 API yüzeyi (process ayaktaysa — kaçış veya bug)

- Fiscal payment / Sonderbeleg: mevcut imza zorunluluğu + ek guard `ITseFiscalProductionGate.EnsureAllowed()` → 503 `TSE_UNSAFE_PRODUCTION_CONFIG`.  
- FA diagnostics: `GET /api/rksv/environment` zaten Demo/Simulated döner; genişlet: `fiscalConfigLock: { ok, reasons[] }`.

### 4.4 Ne yapılmamalı

- Sessizce Soft TSE’ye düşmek.  
- Production’da `TseMode=Off` ile ödemeyi “başarılı non-fiscal” saymak.  
- Yalnızca UI uyarısı ile yetinmek (backend kilitsiz).

---

## 5. Admin uyarısı (FA)

### 5.1 Evet — banner zorunlu (defense in depth + operatör görünürlüğü)

Backend kilit olsa bile FA şunları göstermeli:

| Sinyal | Kaynak | UI |
|--------|--------|-----|
| Demo / Simulated | `GET /api/rksv/environment` (`isSimulated`, environment) | Kalıcı üst banner (RKSV hub + layout) |
| Fiscal config lock fail | Yeni alan veya `/health/tse/mode` (Super Admin) | Kırmızı Alert: “Production TSE lock: …” |
| Provider Soft/Fake (Staging kaçış) | environment DTO | Turuncu “nicht fiskal” |

### 5.2 Yerleşim

1. **Global (Super Admin / Mandanten-Admin fiscal sayfalar):** `RksvEnvironmentBanner` — mevcut DEMO etiketi güçlendirilsin.  
2. **`/admin/tse-management`:** Config posture satırı (`TseMode`, `Mode`, `Provider`, lock OK/FAIL).  
3. **i18n:** `tseManagement.productionLock.*` (de/en/tr) — hardcoded string yok.

### 5.3 Banner kopyası (örnek de)

> **Produktive TSE-Konfiguration unsicher.** TseMode/Demo/Off oder Mode=Fake ist in Production nicht zulässig. Signaturen sind nicht rechtsgültig. Bitte Ops/Compliance kontaktieren.

### 5.4 Yetki

- Detaylı config: `system.critical` / TSE admin.  
- “Demo ortamındasınız” özeti: fiscal permission’ı olan tüm FA kullanıcıları (yanlış güveni kırmak için).

---

## 6. Uygulama planı (kısa)

| Adım | İş | Rol | İG |
|------|-----|-----|-----|
| 1 | `TseProductionOptionsValidator` + `ValidateOnStart` + unit testler | Backend | 1–1.5 |
| 2 | `TseFiscalConfigHealthCheck` + `/health/tse/mode` | Backend | 0.5–1 |
| 3 | `RksvEnvironmentStatusDto` genişletme + OpenAPI | Backend | 0.5 |
| 4 | FA banner + tse-management göstergesi + i18n | Frontend | 1–1.5 |
| 5 | `appsettings.Production.example.json` + Ops checklist maddesi | Ops / Docs | 0.5 |

**Toplam:** ~3–5 İG (aksiyon planı ile uyumlu).

### Test stratejisi

- Unit: Production host mock → Off/Demo/Fake → `ValidateOptionsResult.Fail`; Device+Real+fiskaly → Success.  
- WebApplicationFactory: Production env + unsafe config → host build throws.  
- Development + Demo → Success.  
- FA: simulated environment → banner visible (component test).

---

## 7. Kabul kriterleri (P0-2 done)

- [ ] Production’da `Tse:TseMode=Off|Demo` veya `Tse:Mode=Fake` → process start etmez.  
- [ ] Production’da `RKSV:TseMode=Simulation` / `RKSV:Mode=Demo` → start etmez.  
- [ ] `Provider=fake|soft` + Production → start etmez (Device beklenirken).  
- [ ] `/health/tse/mode` Unhealthy döner (kaçış yoksa; ValidateOnStart sonrası normalde pod yok).  
- [ ] FA’da Demo/Simulated / lock-fail banner (i18n).  
- [ ] Development testleri (Demo/Fake) yeşil kalır.  
- [ ] Escape hatch dokümante + default `false`.

---

## 8. Özet karar matrisi

| Soru | Karar |
|------|--------|
| Nasıl engelle? | **`IValidateOptions` + `ValidateOnStart`** (birincil); middleware değil |
| `/health/ready`? | DB kalsın; config için **ayrı** `/health/tse/mode` |
| Cihaz Offline ready? | Hayır — Degraded (mevcut) |
| FA banner? | **Evet** |
| Escape hatch? | Dar, default off, audit + Critical log |

---

**Son güncelleme:** 2026-07-29 — P0-2 tasarım önerisi.
