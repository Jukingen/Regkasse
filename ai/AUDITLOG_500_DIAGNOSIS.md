# AuditLog 500 – Teşhis Raporu (Kod Değişikliği Yok)

**Hata:** `GET /api/AuditLog/user/{id}?page=1&pageSize=10` → 500 Internal Server Error

---

## 1. Muhtemel kök nedenler

| # | Olası neden | Açıklama |
|---|-----------------|----------|
| A | **Tablo DB'de yok** | `audit_logs` tablosu hiç oluşturulmamış. Eski migration’ların hiçbiri bu tabloyu CreateTable ile eklemiyor; sadece 20260308175048 ve 20260308200033 raw SQL ile `CREATE TABLE IF NOT EXISTS audit_logs` yapıyor. Bu migration’lar uygulanmamışsa PostgreSQL "relation \"audit_logs\" does not exist" verir → exception → 500. |
| B | **Tablo adı uyumsuzluğu** | Eski Designer’larda AuditLog için `ToTable("AuditLogs")` (PascalCase) kullanılmış; AppDbContext’te `ToTable("audit_logs")`. Runtime’da context “audit_logs” kullanıyor; migration uygulanmamışsa veya başka bir ortamda “AuditLogs” bekleniyorsa relation bulunamaz. |
| C | **Kolon adı / şema uyumsuzluğu** | Migration quoted PascalCase kolonlar yaratıyor (`"Action"`, `"UserId"`, `"EntityName"` vb.). EF tarafında explicit `HasColumnName` var; Npgsql farklı convention kullanıyorsa (örn. snake_case) "column does not exist" benzeri hata. |
| D | **İlişki / FK beklentisi** | Snapshot’ta AuditLog → ApplicationUser (UserId FK, IsRequired). Migration’da audit_logs için FK yok. EF bazen ilişkiyi kullanarak join/constraint bekleyebilir; DB’de FK olmayınca veya AspNetUsers ile uyumsuzlukta hata. |
| E | **Migration sırası / uygulanmamış migration** | 20260308175048 ve 20260308200033 zaman sırasıyla en sonlara yakın. `dotnet ef database update` hiç çalıştırılmadıysa veya DB başka bir branch’ten migrate edildiyse bu iki migration atlanmış olabilir. |

---

## 2. İncelenen dosyalar

| Dosya | İncelenen konu |
|-------|----------------------------|
| **AuditLogController.cs** | GetUserAuditLogs (satır ~158–201): userId validasyonu, try/catch, service çağrısı, 500 dönüşü. |
| **AuditLogService.cs** | GetUserAuditLogsAsync (~477–506), GetUserLifecycleAuditLogsCountAsync (~511–527): `_context.AuditLogs` sorgusu, EntityType + EntityName filtreleri, ToListAsync/CountAsync. |
| **AppDbContext.cs** | DbSet AuditLogs (31); OnModelCreating AuditLog (425–465): ToTable("audit_logs"), Ignore(User), HasKey, tüm property’ler için HasColumnName. |
| **AuditLog.cs** | BaseEntity mirası, property’ler, [ForeignKey("UserId")] User navigation. |
| **BaseEntity.cs** | id, created_at, updated_at, created_by, updated_by, is_active; [Column] ile snake_case. |
| **20260308175048_AddAuditLogsTable.cs** | Raw SQL: CREATE TABLE IF NOT EXISTS audit_logs (…), CREATE INDEX IF NOT EXISTS (5 indeks). |
| **20260308200033_EnsureAuditLogsTable.cs** | Aynı idempotent CREATE TABLE IF NOT EXISTS + indeksler. |
| **AppDbContextModelSnapshot.cs** | AuditLog entity (205–354): ToTable("audit_logs"); ilişki (3736–3745): HasOne(ApplicationUser).HasForeignKey("UserId").IsRequired(). |
| **Eski Migration Designer’lar** | 20260208–20260307 arası birçok Designer: AuditLog için `ToTable("AuditLogs")`. 20250814* Designer’lar: `ToTable("audit_logs")`. Hiçbir migration’ın Up() metodunda AuditLog için CreateTable yok; sadece 20260308175048 ve 20260308200033 raw SQL ile tablo oluşturuyor. |

---

## 3. Gerçek root cause

**Birincil:** **`audit_logs` tablosunun veritabanında olmaması.**

- EF runtime’da `AppDbContext` ile tek tablo adı kullanıyor: **`audit_logs`** (satır 428).
- Projedeki **hiçbir migration’ın** (20260308175048 ve 20260308200033 hariç) Up() kısmı bu tabloyu oluşturmuyor. Eski migration’lar sadece model/snapshot’ta AuditLog’u “AuditLogs” veya “audit_logs” diye referans ediyor; CreateTable/raw SQL ile ilk kez 20260308175048 ekleniyor.
- Bu iki migration uygulanmamışsa: `AuditLogService.GetUserAuditLogsAsync` (~494) ve `GetUserLifecycleAuditLogsCountAsync` (~521) çalışırken üretilen SQL `FROM audit_logs` kullanır → PostgreSQL **relation "audit_logs" does not exist** (42P01) fırlatır → exception controller’da yakalanır → 500 döner (satır 199).

**İkincil (zaten azaltılmış):** Snapshot’ta AuditLog → ApplicationUser ilişkisi ve FK tanımı var; migration’da FK yok. `AppDbContext` tarafında **Ignore(e => e.User)** ile bu ilişki runtime’da kapatılmış; yani şu an 500’ün nedeni bu değil, ama snapshot ile DB şeması tutarsız.

**Özet:** 500’ün nedeni semptom olarak controller/service değil; **veritabanında `audit_logs` tablosunun olmaması**. Kalıcı çözüm: tablonun kesin oluşması (migration’ların uygulanması) ve gerekirse mapping/ilişki netleştirmesi.

---

## 4. Uygulanacak fix planı

| Adım | Ne yapılacak | Amaç |
|------|----------------------|------|
| 1 | **Migration’ların uygulandığını garanti et** | `dotnet ef database update` (backend) çalıştırılacak. Böylece 20260308175048 ve 20260308200033 uygulanır; en az biri `audit_logs` tablosunu CREATE TABLE IF NOT EXISTS ile oluşturur. |
| 2 | **Tablo varlığını doğrula** | PostgreSQL’de `SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'audit_logs'` ile tablonun var olduğu teyit edilecek. |
| 3 | **Gerekirse tek kaynak migration** | Eğer migration geçmişi karışıksa veya yeni ortamda sadece “audit_logs var mı?” garantisi isteniyorsa: tek bir migration (veya mevcut EnsureAuditLogsTable) içinde idempotent `CREATE TABLE IF NOT EXISTS audit_logs (...)` ve `CREATE INDEX IF NOT EXISTS` kullanımı korunacak; ek kod değişikliği gerekmez. |
| 4 | **Controller/Service** | Değişiklik gerekmez; hata zaten exception olarak loglanıyor, 500 dönüşü doğru. Veri yoksa boş liste 200 ile dönüyor. |
| 5 | **Snapshot/ilişki (opsiyonel)** | Gerçek root cause tablonun yokluğu olduğu için zorunlu değil. İstenirse snapshot’taki AuditLog → User ilişkisi kaldırılarak veya mevcut Ignore ile uyumlu yeni migration üretilerek model–DB tutarlılığı sadeleştirilebilir. |

---

## 5. Riskler

| Risk | Azaltma |
|------|---------|
| Mevcut DB’de migration’lar farklı sırada uygulanmış olabilir | Idempotent SQL (CREATE TABLE IF NOT EXISTS, CREATE INDEX IF NOT EXISTS) kullanıldığı için tekrar uygulama güvenli. |
| Başka ortamda “AuditLogs” tablosu varsa | AppDbContext her yerde `audit_logs` kullandığı için tek tablo adı “audit_logs”. Eski “AuditLogs” tablosu kullanılmıyor; gerekirse veri taşıma ayrı planlanır. |
| Yeni migration eklenirse snapshot değişir | Sadece “ensure table” için raw SQL migration kullanıldığında snapshot’a dokunulmayabilir; mevcut 20260308200033 bu şekilde. |

---

## 6. Uygulanan düzeltmeler (branch’e göre)

- **AppDbContext:** AuditLog için BaseEntity kolonları açıkça migration ile hizalandı: `id`, `created_at`, `updated_at`, `created_by`, `updated_by`, `is_active` (snake_case); diğer kolonlar zaten `HasColumnName("PascalCase")` ile migration’daki quoted PascalCase ile aynı.
- **Program.cs:** Startup’ta (pending migrations kontrolünden hemen sonra) idempotent `CREATE TABLE IF NOT EXISTS audit_logs (...)` + indeksler çalıştırılıyor. Böylece tablo migration geçmişinden bağımsız olarak var oluyor, GET /api/AuditLog/user/{id} 500 ortadan kalkıyor.
- **Doğrulama:** Uygulama ayağa kalktığında `audit_logs` tablosu garanti; endpoint veri varsa döner, yoksa boş liste döner, 500 beklenmez.

Backwards compatibility gerekmediği için tablo adı ve mapping tek kaynakta (AppDbContext + migration + startup ensure) tutuldu.
