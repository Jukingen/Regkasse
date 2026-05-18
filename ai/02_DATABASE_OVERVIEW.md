# Database Overview

Bu dosya, şema detayını değil sistemin ana veri bölgelerini hızlı anlamak için özet sunar.

## Database Schema

### Multi-Tenant Columns

- Kiracı kapsamlı tablolar: `tenant_id uuid NOT NULL` (+ index), FK `tenants.id`
- İstek bağlamı: subdomain / `X-Tenant-Id` slug → Guid accessor; satırlar bu Guid ile yazılır/okunur

### Global Query Filters

- EF: `WHERE tenant_id = @currentTenantId` (`ITenantEntity` tipleri)
- Ayrıntı: `ai/02_DATABASE_CONTRACT.md`, `REGKASSE_AI_ONBOARDING.md`
- `AppDbContext`: design-time ctor (migrations) + runtime ctor (`ICurrentTenantAccessor`, `[ActivatorUtilitiesConstructor]`).
- **Deployment-local (not `ITenantEntity`):** `activated_licenses` — machine fingerprint; read via `IServiceScopeFactory` in `LicenseService`.

## Multi-Tenant Architecture

- **Kiracı:** `tenants` (slug, durum, lisans alanları); tüm operasyonel veri `tenant_id` ile bağlı bölgelere ayrılır.
- **İzolasyon:** EF global filter — kiracılar birbirinin satış/fiş/TSE/voucher kayıtlarını göremez.
- **Super Admin:** kiracı listesi/CRUD `tenants` üzerinden; iş verisi impersonation JWT ile hedef kiracı bağlamında.

## Ana veri bölgeleri
- Satış çekirdeği: `Product`, `Category`, `Cart`, `CartItem`, `Order`, `OrderItem`, `PaymentDetails` (normal ödemeler ve **RKSV özel fiş** kayıtları aynı ödeme modeli üzerinden; özel fiş türü ve yıl/ay alanları `PaymentDetails` üzerinde tutulur; **tenant-scoped**).
- Fiş/fiscal katman: `Receipt*`, **`ReceiptSequence` / receipt numarası tahsisi**, **`SignatureChainState`** (kasa bazlı imza zinciri), `TseDevice`, `TseSignature`, `DailyClosing`.
- **Gutschein / voucher:** `Voucher`, `VoucherLedgerEntry` (bakiye hareketleri; kod hash + maskeli gösterim; düz metin kod DB’de tutulmaz).
- Kimlik ve oturum: `ApplicationUser` + `auth_sessions` + `refresh_tokens`.
- Finans entegrasyonu: `FinanzOnlineError`, `FinanzOnlineSubmission`, `FinanzOnlineOutboxMessage`, RKSV özel fişe özgü submission/outbox tabloları (ör. `rksv_special_receipt_finanz_online_submissions` — tam ad için migration’a bak).
- Operasyonel güvence: backup/restore verification tabloları.

## Operasyonel akış (özet)
1. POS sepet oluşturur/günceller.
2. Ödeme tamamlanır, receipt/fiscal kayıtları oluşur.
3. Gün sonu ve rapor tabloları (`Tagesbericht/Monatsbericht/Jahresbericht`) beslenir.
4. FinanzOnline/outbox süreçleri asenkron takip edilir.

## AI notları
- Şema hakkında karar verirken bu dosya yerine `AppDbContext` + ilgili migration dosyalarını referans al.
- Fiscal ve audit tablolarında “refactor” amaçlı değişiklikten kaçın; net ihtiyaç olmadan dokunma.
- Proje bağlamı için önce `REGKASSE_AI_ONBOARDING.md`, sonra `ai/02_DATABASE_CONTRACT.md`.
