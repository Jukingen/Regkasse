# Database Contract (PostgreSQL + EF Core)

## Database Schema

### Multi-Tenant Columns

`ITenantEntity` uygulayan tüm kiracı kapsamlı tablolarda:

- `tenant_id uuid NOT NULL` — FK `tenants.id`
- Performans için index (`AppDbContext` `HasIndex(e => e.TenantId)`)
- Değer istek başına uygulama katmanından gelir: alt alan adı / dev header’daki **slug** → `CurrentTenantService` → `ICurrentTenantAccessor.TenantId` (Guid); login sonrası JWT `tenant_id` claim geçerli olabilir

Kök / global örnekler: `tenants`, Identity kullanıcı tabloları. Dış anahtar string: `tenants.slug` (alt alan çözümlemesi).

### Global Query Filters

EF Core tüm `ITenantEntity` sorgularına otomatik filtre ekler:

```text
WHERE tenant_id = @currentTenantId
```

Kaynak: `AppDbContext.CreateTenantQueryFilter` → `_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId`.

### AppDbContext constructors ve DI

| Constructor | Amaç |
|-------------|------|
| `AppDbContext(DbContextOptions<AppDbContext> options)` | Design-time / `dotnet ef` — `NullCurrentTenantAccessor`, filtre kapalı |
| `AppDbContext(options, ICurrentTenantAccessor)` | Runtime — `[ActivatorUtilitiesConstructor]` |

`IDbContextFactory<AppDbContext>` singleton servislerde yalnızca **`IServiceScopeFactory` scope’u içinden** kullanılmalı (`LicenseService`).

**Not tenant-scoped:** `activated_licenses` (deployment-local lisans aktivasyonu).

## Multi-Tenant Architecture

- Kiracı kök tablosu: `tenants` (`Tenant` entity — global, `ITenantEntity` değil).
- Kiracı kapsamlı tablolar: `tenant_id` (UUID, non-null) + `ITenantEntity` / `BaseTenantEntity`.
- Kullanıcı–kiracı: `user_tenant_memberships` (login’de tek aktif üyelik beklentisi; çoklu üyelik loglanır).
- Çapraz kiracı okuma/yazma: uygulama katmanında **404**; `IgnoreQueryFilters()` yalnızca Super Admin / seed / bilinçli bypass.
- Migration’larda `tenant_id` ekleme: fiscal/audit dalgalarına bak (`AddTenantIdToFiscalAndAuditTables` vb.).

## Migrating existing databases

Mevcut tek-kiracılı PostgreSQL kurulumları için repoda **dalga dalga** migration zinciri kullanılır (tek `AddTenantIdToAllTables` yok).

### Pattern (EF Core)

1. `tenants` tablosu + default kiracı seed (`20260403190133_AddTenantsAndSettingsTenantId`).
2. İlgili tablolara `tenant_id uuid NOT NULL` ekle — geçici/default: `LegacyDefaultTenantIds.Primary` (sabit Guid; string `'legacy'` değil).
3. Veri backfill migration’ları (ör. `BackfillUserTenantMembershipsData`).
4. Wave migrations: payment methods / cash registers (Wave2), categories / products (Wave3A), modifiers (Wave3B), fiscal / audit / offline (`20260516101549_AddTenantIdToFiscalAndAuditTables`).
5. `HasIndex(e => e.TenantId)` — `AppDbContext` içinde.

### Komutlar

```bash
dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

Yeni tabloya `tenant_id` eklerken:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

### Dikkat

- Fiscal / receipt / TSE tablolarında destructive migration yok sayma; additive + default Guid kullan.
- `IgnoreQueryFilters()` yalnızca Super Admin servisleri, seed veya bilinçli bakım yollarında.
- Ayrıntı: `docs/MULTI_TENANT.md`, `REGKASSE_AI_ONBOARDING.md` (Database Schema).

## Kaynak
- Gerçek model kaynağı: `backend/Data/AppDbContext.cs` ve `backend/Migrations/*`.
- Migration yönetimi EF Core ile yapılır; migration geçmişi authoritative kabul edilir.

## Veri modeli prensipleri
- Finansal alanlarda `decimal(18,2)` yaygındır; vergi/oran alanlarında daha dar precision kullanılabilir (örn. `decimal(5,2)`).
- Identity + uygulama tabloları aynı context içinde yönetilir.
- Auth session tabloları kritik: `auth_sessions`, `refresh_tokens`.
- JSON/esnek payload alanları mevcut; keyfi yeni json alanı açma.

## Hassas domain alanları
- **PaymentDetails** ve ilişkili ödeme satırları: normal satış + RKSV özel fiş alanları (`RksvSpecialReceiptKind`, yıl/ay metadatası, `RksvNullbelegActsAsJahresbeleg`, storno/refund ve offline replay metadatası vb.—gerçek sütun listesi için `AppDbContext` + migration’lar).
- **Receipt** / **ReceiptSequence** / **`signature_chain_state`**: fiş numarası sırası ve imza zinciri tutarlılığı; ayrı tablolarda kırılmaması gerekir.
- **Voucher:** `vouchers`, `voucher_ledger_entries` (bakiye ve denetim izi; düz metin voucher kodu saklanmaz—hash/masked gösterim modeli).
- TSE cihaz/imza tabloları (`tse_devices`, `tse_signatures`, vb.)
- `offline_transactions` ve payload hash / replay ile ilişkili alanlar
- `DailyClosing` ve rapor kapanışı ile ilişkili tablolar
- FinanzOnline outbox/submission tabloları
- Backup/restore verification tabloları (operasyonel güvence için)

## Şema değişikliği kuralları
1. Önce mevcut entity mapping ve migration paternini incele.
2. Public contract etkisini (DTO/OpenAPI) ayrı değerlendir.
3. Geriye dönük uyumluluk olmadan destructive değişiklik yapma.
4. **Fiscal/RKSV alanlarında tercih:** ihtiyaç halinde nullable/additive migration ve geri dönüşü testli küçük adımlar (`REGKASSE_AI_ONBOARDING.md` migration notu ile uyumlu).
5. Hassas alanlarda index/constraint değişikliklerini testsiz bırakma.

## Minimum kontrol
- `dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
- `dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
