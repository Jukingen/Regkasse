# Critical: High-risk areas (change only with explicit scope)

## 1) Cart → Payment → Receipt → DailyClosing
- Bu zincirde davranış değişikliği finansal ve yasal risk üretir.
- İstenen değişiklik dışında refactor/rewrite yapma.

## 2) TSE ve signature chain
- TSE imza üretimi, **receipt numbering / sequence** ve **`signature_chain_state`** doğrulama adımları korunmalı.
- İmza payload alanlarını ve akış sırasını sebepsiz değiştirme; istemci veya flag ile zinciri “atlatma” yok.

## 3) RKSV özel fiş yaşam döngüsü
- Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg: tekillik kuralları, kasa durumu ve TSE kullanılabilirlik koşulları kodda sıkıdır.
- Schlussbeleg’i günlük kapanışla karıştırma; decommissioned kasa geçişi ile ilişkilidir.

## 4) Decommissioned register guardrails
- **Decommissioned** kasa yeni oturum veya ödeme kabul etmemelidir; bu guard’ları gevşetme veya bypass etme.

## 5) Voucher ledger ve bakiye
- `Voucher` / `VoucherLedgerEntry` tutarlılığı ve denetim izi; düz metin kod saklama yok.
- Ledger hareket türleri ve tutarların sıfır altına inmesi gibi kurallar dikkatle ele alınır.

## 6) FinanzOnline / outbox / reconciliation
- Mapping alanları, retry taxonomisi, reconciliation semantiği hassastır.
- RKSV submission iskeleti üretim-tamamlı değil olsa da outbox satırları ve durum alanları audit için önemlidir.
- Hata yutma veya audit izini azaltan değişiklik yapılmaz.

## 7) Authorization/RBAC
- Permission adları, role-permission matrix ve guard akışları hassastır.
- Endpoint yetkilerini gevşetme; değişim varsa açık migration planı yaz.

## 8) Money precision / rounding
- Para hesaplarında mevcut precision ve rounding davranışını koru.

## 9) Tenant isolation / query filters
- `AppDbContext` global query filter ve `ICurrentTenantAccessor` akışını gevşetme veya kaldırma.
- Singleton servislerde root `IDbContextFactory` / `AppDbContext` kullanımına geri dönme (`IServiceScopeFactory` pattern’ini bozma — `LicenseService`).
- `AppDbContext` üzerinde DI’ın çözeceği birden fazla runtime constructor bırakma (`[ActivatorUtilitiesConstructor]` + design-time ctor dışında).
- `IgnoreQueryFilters()` kullanımını yalnızca bilinçli Super Admin / migration yollarında bırak.
- Çapraz kiracı 404 semantiğini 403 veya boş 200 ile değiştirme.
- Middleware sırasını (`TenantResolutionMiddleware` → auth → `TenantContextMiddleware`) sebepsiz değiştirme.

> Emin değilsen varsayım yapma: önce kapsamı daralt, risk ve belirsizliği açık yaz. Özet bağlam: `REGKASSE_AI_ONBOARDING.md`.
