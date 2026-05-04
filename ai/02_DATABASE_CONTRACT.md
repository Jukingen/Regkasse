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
