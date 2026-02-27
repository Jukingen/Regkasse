# Backend Path Migration – Kontrol Listesi ve Analiz

> **TAMAMLANDI** – Refactor uygulandı. Güncel yapı için bkz. `ai/BACKEND_PATH_REFACTOR_REPORT.md`.
> Backend kökü artık `Regkasse\backend\`.

**Hedef:** Backend proje kök yolunu `Regkasse\backend\KasseAPI_Final\KasseAPI_Final` → `Regkasse\backend` olacak şekilde sadeleştirmek.

**Kapsam:** Bu belge migration öncesi analizi dokümante eder.

---

## 1. Kontrol Listesi (Path Bağımlılıkları)

### 1.1 .sln ve Solution İçi Project Path’leri

| Dosya | Durum | Risk |
|-------|-------|------|
| `backend/KasseAPI_Final/KasseAPI_Final/KasseAPI_Final.sln` | .sln dosyası API projesiyle aynı klasörde. Proje referansı: `KasseAPI_Final.csproj` (aynı dizin). Tests projesi **solution’da yok**. | Orta – .sln konumu ve içeriği değişecek. Tests projesi eklenmeli. |
| Solution’da sadece 1 proje var | Tests projesi (`KasseAPI_Final.Tests`) solution’a dahil değil | Düşük – Yapı değişince Tests’i de eklemek mantıklı. |

**Öneri hedef yapı:**
- Yeni .sln: `backend/KasseAPI.sln` veya `backend/Regkasse.sln`
- Ana proje: `backend/KasseAPI_Final.csproj` (veya yeni ad)
- Test projesi: `backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj`

---

### 1.2 .csproj İçindeki Path Referansları

| Dosya | Referans | Tip | Risk |
|-------|----------|-----|------|
| `KasseAPI_Final.Tests.csproj` | `..\KasseAPI_Final\KasseAPI_Final.csproj` | ProjectReference | **Yüksek** – Göreli path taşıma sonrası kırılır. Yeni path: `..\KasseAPI_Final.csproj` |
| `KasseAPI_Final.csproj` | Sadece PackageReference | - | Düşük – Content/None/CopyToOutputDirectory yok |

**Not:** Ana .csproj içinde `Content`, `None`, `CopyToOutputDirectory` veya harici dosya path’i bulunmuyor.

---

### 1.3 Directory.Build.props / Directory.Build.targets

| Kontrol | Sonuç |
|---------|--------|
| `Directory.Build.props` | **Yok** |
| `Directory.Build.targets` | **Yok** |
| Path referansı | **Yok** |

---

### 1.4 launchSettings.json, appsettings, UserSecrets, Environment

| Dosya | Path / İçerik | Risk |
|-------|----------------|------|
| `Properties/launchSettings.json` | `commandName: "Project"` – path bağımsız | Düşük |
| `appsettings.json` | Connection string, JWT, CompanyProfile – path yok | Düşük |
| `appsettings.Development.json` | Logging, Tse.MockEnabled – path yok | Düşük |
| UserSecrets | `AddUserSecrets` kullanılmıyor | Yok |

**Uyarı:** UserSecrets kullanılırsa, UserSecrets ID proje assembly adına bağlı olabilir. `KasseAPI_Final` ismi değişirse secrets migration gerekebilir.

---

### 1.5 Docker / docker-compose / K8s / CI Pipeline

| Kaynak | Durum | Path / Risk |
|--------|-------|-------------|
| Dockerfile | **Yok** | - |
| docker-compose | **Yok** | - |
| K8s manifest | **Yok** | - |
| GitHub Actions | **Yok** (`.github/` yok) | - |
| `scripts/ci-smoke-test.sh` | **Var** | `cd backend/KasseAPI_Final/KasseAPI_Final` → **Yüksek** – Bu path değişmeli |

**ci-smoke-test.sh kritik satırlar:**
```bash
cd backend/KasseAPI_Final/KasseAPI_Final
dotnet build
dotnet ef database update ...
dotnet run --no-build
```

---

### 1.6 EF Core Migrations / Startup Assembly / Design-Time Factory

| Konu | Durum | Risk |
|------|--------|------|
| Migrations klasörü | `backend/KasseAPI_Final/KasseAPI_Final/Migrations/` | **Yüksek** – Taşınacak. Namespace: `KasseAPI_Final.Migrations` |
| Designer.cs | `[DbContext(typeof(AppDbContext))]` – assembly’e bağlı değil | Düşük |
| Design-time factory | `IDesignTimeDbContextFactory` kullanılmıyor | Yok |
| `dotnet ef` komutları | Çalışma dizinine bağlı – yeni `backend/` altında çalıştırılacak | Orta |

**Not:** `dotnet ef migrations add` ve `dotnet ef database update` proje dizininden çalıştırılır. Yeni dizin `backend/` olacak.

---

### 1.7 Build Output, Publish Script, dotnet run

| Kaynak | İçerik | Risk |
|--------|--------|------|
| `scripts/ci-smoke-test.sh` | `cd backend/KasseAPI_Final/KasseAPI_Final` | **Yüksek** |
| `MONOREPO_MAP.md` | `dotnet run --project <path-to-api>` – generic | Düşük |
| `DEVELOPER_ONBOARDING.md` | `cd backend/Registrierkasse_API` – **eski/yanlış path** | Orta – Dokümantasyon tutarsız |
| `onboarding_guide.md` | `backend/KasseAPI_Final` – **kısmen doğru** (iç KasseAPI_Final eksik) | Orta |
| `README.md` | `cd backend/Registrierkasse_API` | Orta |
| `PROJECT_STRUCTURE.md` | `backend/Registrierkasse_API` | Orta |
| `SWAGGER_FIX_README.md` | `cd backend/KasseAPI_Final/KasseAPI_Final` | Orta |
| `PAYMENT_SYSTEM_README.md` | `cd backend/KasseAPI_Final/KasseAPI_Final` | Orta |
| `API_OPTIMIZATION_README.md` | `cd backend/KasseAPI_Final/KasseAPI_Final` | Orta |

---

### 1.8 Frontend-Admin API Base URL / Proxy / Rewrite

| Kaynak | İçerik | Path Bağımlılığı | Risk |
|--------|--------|-------------------|------|
| `src/lib/axios.ts` | `NEXT_PUBLIC_API_BASE_URL` \|\| `http://localhost:5183` | Port/servis URL – path değil | **Yok** |
| `.env.example` | `NEXT_PUBLIC_API_BASE_URL=http://localhost:5183` | - | Yok |
| `orval.config.ts` | `target: '../backend/KasseAPI_Final/KasseAPI_Final/swagger.json'` | **Swagger JSON path** | **Yüksek** |
| `next.config.js` | Proxy/rewrite yok | - | Yok |

**Kritik:** Orval, swagger.json’ı bu path’ten okuyor. Backend taşındığında path `../backend/swagger.json` veya `../backend/KasseAPI_Final/swagger.json` olacak (tercih edilen yapıya göre).

---

### 1.9 Diğer Bağımlılıklar

| Kaynak | Path / İçerik | Risk |
|--------|---------------|------|
| `frontend-admin/swagger.json` | `backend/.../swagger.json` – orval input | Yüksek |
| `swagger.json` içeriği | `"KasseAPI_Final"` (OpenAPI info) | Düşük – sadece API adı |
| `frontend/workspace.code-workspace` | `"path": "../KasseAPI"` | Orta – muhtemelen eski/başka proje |
| `frontend/config.ts` | `http://localhost:5183/api` | Yok – port/servis |
| `frontend/contexts/AuthContext.tsx` | Hardcoded `http://localhost:5183/api/auth/logout` | Orta – env’e taşınmalı (path değil) |
| `frontend/utils/networkUtils.ts` | `http://${ipAddress}:5183/api/health` | Yok |

---

### 1.10 .cursor / AI / Rules Path Referansları

| Dosya | Path Referansları | Risk |
|-------|-------------------|------|
| `.cursor/rules/00-global-architecture.mdc` | `backend/KasseAPI_Final/KasseAPI_Final` | Orta |
| `.cursor/rules/01-backend-standards.mdc` | `backend/KasseAPI_Final/KasseAPI_Final/...` | Orta |
| `.cursor/rules/02-database-standards.mdc` | `backend/KasseAPI_Final/KasseAPI_Final/...` | Orta |
| `.cursor/rules/05-rksv-compliance.mdc` | `backend/KasseAPI_Final/KasseAPI_Final/...` | Orta |
| `.cursor/rules/07-do-not-touch.mdc` | `backend/KasseAPI_Final/KasseAPI_Final/...` | Orta |
| `.cursor/rules/README.md` | Çok sayıda path | Orta |
| `ai/00_CONTEXT_README.md` | `Regkasse / KasseAPI_Final` | Düşük |
| `ai/01_BACKEND_CONTRACT.md` | Namespace: KasseAPI_Final.* | Düşük |
| `ai/03_API_CONTRACT.md` | KasseAPI_Final.DTOs | Düşük |
| `ai/06_TASK_TEMPLATE.md` | Regkasse / KasseAPI_Final | Düşük |
| `ai/08_FILE_MAP.md` | `backend/KasseAPI_Final/` | Orta |
| `ai/RKSV_*.md` | KasseAPI_Final.Tests/ | Düşük |

---

## 2. Riskli Noktalar Özeti

### Yüksek risk
1. **KasseAPI_Final.Tests.csproj** – `ProjectReference` path’i (`..\KasseAPI_Final\KasseAPI_Final.csproj`) yeni dizine göre güncellenmeli.
2. **scripts/ci-smoke-test.sh** – `cd backend/KasseAPI_Final/KasseAPI_Final` → yeni backend köküne güncellenmeli.
3. **frontend-admin/orval.config.ts** – swagger input path’i: `../backend/KasseAPI_Final/KasseAPI_Final/swagger.json` → yeni path’e güncellenmeli.
4. **Solution (.sln)** – Konum ve proje path’leri değişecek; Tests projesi eklenebilir.

### Orta risk
5. **EF Core migrations** – `Migrations/` klasörü taşınacak; `dotnet ef` çalışma dizini değişecek.
6. **Dokümantasyon** – README, DEVELOPER_ONBOARDING, onboarding_guide, PROJECT_STRUCTURE, SWAGGER_FIX_README, PAYMENT_SYSTEM_README, API_OPTIMIZATION_README içindeki path’ler tutarsız; güncellenmeli.
7. **.cursor/rules** – Tüm mdc ve README dosyalarındaki canonical path’ler güncellenmeli.
8. **ai/08_FILE_MAP.md** – Backend root path güncellenmeli.

### Düşük risk
9. **Namespace/assembly adı** – `KasseAPI_Final` korunursa migration kolay; değişirse tüm namespace ve referanslar gözden geçirilmeli.
10. **frontend/workspace.code-workspace** – `../KasseAPI` muhtemelen harici workspace; proje yapısına göre düzeltilmeli.

---

## 3. Değişiklik Sırası

1. **Hazırlık**
   - Hedef dizin yapısını netleştir (`backend/` = proje kökü mü, yoksa `backend/KasseAPI_Final/` mi?).
   - Git’te yeni branch aç; mevcut durumu yedekle.

2. **Backend taşıma**
   - `backend/KasseAPI_Final/KasseAPI_Final/*` → `backend/` (veya `backend/KasseAPI_Final/`).
   - `backend/KasseAPI_Final/KasseAPI_Final.Tests/` → `backend/KasseAPI_Final.Tests/` (veya `backend/Tests/`).

3. **Solution ve proje referansları**
   - Yeni .sln oluştur veya mevcut olanı taşı.
   - `KasseAPI_Final.Tests.csproj` içindeki `ProjectReference` path’ini güncelle.
   - Tests projesini solution’a ekle (isteğe bağlı).

4. **CI / build script**
   - `scripts/ci-smoke-test.sh` içindeki `cd` path’ini güncelle.

5. **Frontend-admin**
   - `orval.config.ts` içindeki swagger input path’ini güncelle.
   - `npm run generate:api` ile API client’ı yeniden üret.

6. **Dokümantasyon**
   - README.md, DEVELOPER_ONBOARDING.md, onboarding_guide.md, PROJECT_STRUCTURE.md, SWAGGER_FIX_README.md, PAYMENT_SYSTEM_README.md, API_OPTIMIZATION_README.md.

7. **.cursor ve AI kuralları**
   - `.cursor/rules/*.mdc`, `.cursor/rules/README.md`.
   - `ai/08_FILE_MAP.md` ve ilgili ai/ dosyaları.

8. **swagger.json**
   - Statik ise `backend/` altına taşı veya build/publish sürecinde oluşturulacak path’i dokümante et.
   - Orval’in bu path’i kullandığını doğrula.

---

## 4. Test ve Doğrulama Planı

| Adım | Kontrol | Komut / Yöntem |
|------|---------|-----------------|
| 1 | Backend build | `cd backend && dotnet build` |
| 2 | Tests build & run | `cd backend && dotnet test` |
| 3 | EF migrations list | `cd backend && dotnet ef migrations list` |
| 4 | DB update | `cd backend && dotnet ef database update` |
| 5 | API çalıştırma | `cd backend && dotnet run` → `http://localhost:5183` |
| 6 | Swagger erişimi | `http://localhost:5183/swagger` açılıyor mu? |
| 7 | Orval API üretimi | `cd frontend-admin && npm run generate:api` hatasız mı? |
| 8 | CI smoke test | `./scripts/ci-smoke-test.sh` (Linux/WSL) |
| 9 | Frontend-admin API çağrıları | Admin panel login + birkaç sayfa gezinti |
| 10 | Frontend (Expo) API | `npx expo start` → giriş ve temel işlemler |

---

## 5. Hedef Dizin Yapısı (Örnek)

**Seçenek A – Tam sadeleştirme (backend = proje kökü):**
```
backend/
├── KasseAPI.sln
├── KasseAPI_Final.csproj
├── Program.cs
├── Controllers/
├── Services/
├── Models/
├── Data/
├── Migrations/
├── Properties/
├── appsettings.json
├── swagger.json
└── KasseAPI_Final.Tests/
    └── KasseAPI_Final.Tests.csproj
```

**Seçenek B – Klasör koruma (backend/KasseAPI_Final = proje kökü):**
```
backend/
├── KasseAPI.sln
├── KasseAPI_Final/
│   ├── KasseAPI_Final.csproj
│   ├── Program.cs
│   ├── Controllers/
│   ├── ...
│   └── swagger.json
└── KasseAPI_Final.Tests/
    └── KasseAPI_Final.Tests.csproj
```

Seçenek A, `Regkasse\backend` hedefine daha uygun; Seçenek B ise `KasseAPI_Final` klasörünü korur, taşıma daha az agresif olur.

---

*Belge oluşturulma: Analiz aşaması – değişiklik yapılmadı.*

---

## 6. Küçük, Güvenli Adımlar – Yürütme Planı

“UYGULA” dediğinde bu adımlar sırayla uygulanacak.

---

### Adım 0: Ön Hazırlık (Opsiyonel)

| Alan | İçerik |
|------|--------|
| **Amaç** | Mevcut durumu doğrula, rollback için branch oluştur |
| **Değişecek dosyalar** | Yok |
| **İşlem** | `git checkout -b refactor/backend-path-simplify` <br> `cd backend/KasseAPI_Final/KasseAPI_Final && dotnet build && dotnet test` |
| **Doğrulama** | `dotnet build` ve `dotnet test` başarılı; çıktıda hata yok |

---

### Adım 1: Tests Projesini `backend/` Altına Taşı

| Alan | İçerik |
|------|--------|
| **Amaç** | Tests projesini `backend/KasseAPI_Final.Tests/` konumuna al |
| **Değişecek dosyalar** | `backend/KasseAPI_Final/KasseAPI_Final.Tests/*` → `backend/KasseAPI_Final.Tests/` |
| **İşlem** | `git mv backend/KasseAPI_Final/KasseAPI_Final.Tests backend/KasseAPI_Final.Tests` |
| **Doğrulama** | `cd backend/KasseAPI_Final/KasseAPI_Final && dotnet build` hâlâ başarılı (API projesi değişmedi). `dotnet test` solution’da Tests olmadığı için eski davranış korunur. |

---

### Adım 2: API Projesi İçeriğini `backend/` Altına Taşı

| Alan | İçerik |
|------|--------|
| **Amaç** | `backend/KasseAPI_Final/KasseAPI_Final/` içeriğini `backend/` köküne taşı |
| **Değişecek dosyalar** | `Program.cs`, `KasseAPI_Final.csproj`, `KasseAPI_Final.sln`, `appsettings*.json`, `Controllers/`, `Services/`, `Models/`, `Data/`, `DTOs/`, `Migrations/`, `Properties/`, `Tse/`, `Swagger/`, `swagger.json`, `AddDemoData.cs` vb. (bin, obj, .vs hariç) |
| **İşlem** | Tüm kaynak dosya ve klasörleri `backend/KasseAPI_Final/KasseAPI_Final/` → `backend/` (git mv). `bin/`, `obj/`, `.vs/` taşınmaz. |
| **Doğrulama** | `cd backend && dotnet build KasseAPI_Final.csproj` başarılı |

---

### Adım 3: Tests ProjectReference Güncelle

| Alan | İçerik |
|------|--------|
| **Amaç** | Tests projesinin API projesine doğru path ile referans vermesini sağla (API artık `backend/` altında) |
| **Değişecek dosyalar** | `backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj` |
| **İşlem** | `ProjectReference Include="..\KasseAPI_Final\KasseAPI_Final.csproj"` → `ProjectReference Include="..\KasseAPI_Final.csproj"` |
| **Doğrulama** | `cd backend && dotnet build` ve `dotnet test` başarılı |

---

### Adım 4: Boş `KasseAPI_Final/` Klasörünü Kaldır

| Alan | İçerik |
|------|--------|
| **Amaç** | Eski dizin yapısını temizle |
| **Değişecek dosyalar** | `backend/KasseAPI_Final/` (silinecek) |
| **İşlem** | `backend/KasseAPI_Final/` ve varsa içindeki boş alt klasörler silinir |
| **Doğrulama** | `backend/KasseAPI_Final` artık yok; `cd backend && dotnet build` hâlâ başarılı |

---

### Adım 5: Solution Dosyasını Güncelle ve Tests Ekle

| Alan | İçerik |
|------|--------|
| **Amaç** | `.sln` içindeki proje path’lerini güncelle, Tests projesini ekle |
| **Değişecek dosyalar** | `backend/KasseAPI_Final.sln` |
| **İşlem** | Proje path’lerini yeni yapıya göre güncelle; `dotnet sln add KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj` ile Tests ekle |
| **Doğrulama** | `cd backend && dotnet build` ve `dotnet test` başarılı |

---

### Adım 6: CI Smoke Test Script Güncelle

| Alan | İçerik |
|------|--------|
| **Amaç** | CI script’inin yeni backend kökünü kullanmasını sağla |
| **Değişecek dosyalar** | `scripts/ci-smoke-test.sh` |
| **İşlem** | `cd backend/KasseAPI_Final/KasseAPI_Final` → `cd backend` |
| **Doğrulama** | `bash scripts/ci-smoke-test.sh` (veya WSL) başarılı; script hata vermez |

---

### Adım 7: Orval Swagger Path Güncelle

| Alan | İçerik |
|------|--------|
| **Amaç** | Orval’in swagger.json’ı yeni konumdan okumasını sağla |
| **Değişecek dosyalar** | `frontend-admin/orval.config.ts` |
| **İşlem** | `target: '../backend/KasseAPI_Final/KasseAPI_Final/swagger.json'` → `target: '../backend/swagger.json'` |
| **Doğrulama** | `cd frontend-admin && npm run generate:api` hatasız tamamlanır |

---

### Adım 8: Dokümantasyon Path’lerini Güncelle

| Alan | İçerik |
|------|--------|
| **Amaç** | Tüm dokümanlardaki backend path referanslarını tutarlı hale getir |
| **Değişecek dosyalar** | `README.md`, `DEVELOPER_ONBOARDING.md`, `onboarding_guide.md`, `PROJECT_STRUCTURE.md`, `SWAGGER_FIX_README.md`, `PAYMENT_SYSTEM_README.md`, `API_OPTIMIZATION_README.md`, `MONOREPO_MAP.md` |
| **İşlem** | `backend/Registrierkasse_API`, `backend/KasseAPI_Final/KasseAPI_Final` vb. → `backend` |
| **Doğrulama** | Güncellenen dosyalarda `cd backend` veya `backend/` kullanımı tutarlı |

---

### Adım 9: .cursor ve AI Kurallarını Güncelle

| Alan | İçerik |
|------|--------|
| **Amaç** | Cursor rules ve ai/ dosyalarındaki canonical path’leri yeni yapıya göre güncelle |
| **Değişecek dosyalar** | `.cursor/rules/00-global-architecture.mdc`, `01-backend-standards.mdc`, `02-database-standards.mdc`, `05-rksv-compliance.mdc`, `07-do-not-touch.mdc`, `.cursor/rules/README.md`, `ai/08_FILE_MAP.md` |
| **İşlem** | `backend/KasseAPI_Final/KasseAPI_Final` → `backend` |
| **Doğrulama** | Path referansları `backend/` ile tutarlı |

---

### Yürütme Sırası Özeti

| Sıra | Adım | Bağımlılık |
|------|------|------------|
| 0 | Ön hazırlık (branch, build/test) | - |
| 1 | Tests projesini taşı | - |
| 2 | API içeriğini taşı | - |
| 3 | ProjectReference güncelle | 1, 2 |
| 4 | Boş klasörü kaldır | 1, 2 |
| 5 | Solution güncelle | 3, 4 |
| 6 | ci-smoke-test.sh güncelle | 2 |
| 7 | orval.config.ts güncelle | 2 |
| 8 | Dokümantasyon güncelle | - |
| 9 | .cursor / ai güncelle | - |

**UYGULA dediğinde:** Adım 0 (opsiyonel) ile başla, ardından **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9** sırasıyla uygulanacak.
