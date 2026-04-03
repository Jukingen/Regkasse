# Database Contract (PostgreSQL + EF Core)

## Kaynak
- Gerçek model kaynağı: `backend/Data/AppDbContext.cs` ve `backend/Migrations/*`.
- Migration yönetimi EF Core ile yapılır; migration geçmişi authoritative kabul edilir.

## Veri modeli prensipleri
- Finansal alanlarda `decimal(18,2)` yaygındır; vergi/oran alanlarında daha dar precision kullanılabilir (örn. `decimal(5,2)`).
- Identity + uygulama tabloları aynı context içinde yönetilir.
- Auth session tabloları kritik: `auth_sessions`, `refresh_tokens`.
- JSON/esnek payload alanları mevcut; keyfi yeni json alanı açma.

## Hassas domain alanları
- Payment/receipt/daily closing ilişkileri
- TSE imza ve zincir state (`tse*`, `signature chain`, receipt sequence)
- FinanzOnline outbox/submission tabloları
- Backup/restore verification tabloları (operasyonel güvence için)

## Şema değişikliği kuralları
1. Önce mevcut entity mapping ve migration paternini incele.
2. Public contract etkisini (DTO/OpenAPI) ayrı değerlendir.
3. Geriye dönük uyumluluk olmadan destructive değişiklik yapma.
4. Hassas alanlarda index/constraint değişikliklerini testsiz bırakma.

## Minimum kontrol
- `dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
- `dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
