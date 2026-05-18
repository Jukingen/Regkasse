# Security & Compliance

## Yasal / uyumluluk iddiası (kritik)
- Bu repodaki dokümantasyon veya kod yorumları **yasal uyumluluk garantisi vermez**. Özellikle BMF/FinanzOnline kabulü, TSE donanım onayı veya resmi DEP/RKSV beyanı iddia edilmez.
- **Fiscal export** (`GET /api/admin/fiscal-export`, `FiscalExportService`): paketler açık **“not legal proof”** uyarısı taşır; tanılama, iç analiz ve operasyonel el değişimi içindir; resmi RKSV kanıtı veya FinanzOnline yerine geçen belge değildir (`REGKASSE_AI_ONBOARDING.md`).

## Multi-Tenant Architecture

- Kiracı verisi EF global query filter ile ayrılır; yetkisiz çapraz kiracı erişimde **404** (bilgi sızıntısı yok).
- Super Admin: `SuperAdmin` rolü + `/api/admin/tenants`; impersonation kısa ömürlü token — audit log’a düşürülür.
- Geliştirme `X-Tenant-Id` yalnızca Development ortamında; üretimde host tabanlı çözümleme zorunlu.
- Offline kuyruk / voucher: kiracı ve fiscal sınırları korunur; voucher sırrı kuyrukta tutulmaz (mevcut kural).

Tam mimari: `docs/MULTI_TENANT.md`.

## Multi-Tenant Security

### Tenant isolation guarantees

| Garanti | Uygulama |
|--------|-----------|
| Veritabanı seviyesi filtreleme | `AppDbContext` global query filter tüm `ITenantEntity` tiplerinde; API istemcisi filtre bypass edemez |
| Singleton + EF | Root’tan scoped `AppDbContext` çözümü yasak; `IServiceScopeFactory` zorunlu (`LicenseService` örnek) |
| Accessor null | `TenantId == null` iken filtre devre dışı — yalnızca bilinçli kod yolları; normal API isteği önce tenant set eder |
| Cross-tenant IDOR | **404** (403 değil) — `TenantIsolationTests.AdminPayments_GetById_CrossTenant_Returns404_Not403` |
| `tenant_id` replay | `offline_transactions.tenant_id` NOT NULL; insert’te cash register / ambient tenant’tan stamp |
| Tüm tablolar kiracılı değil | Örn. `Customer` henüz `ITenantEntity` değil; değişiklik öncesi envanter çıkar |

### Tenant spoofing prevention

| Önlem | Uygulama |
|--------|-----------|
| Production: yalnızca subdomain | `SubdomainTenantProvider` — header/query yalnızca `IsDevelopment()` |
| Dev header’lar production’da kapalı | `ASPNETCORE_ENVIRONMENT=Production` |
| Super Admin ek kontrol | `[Authorize(Roles = SuperAdmin)]` + impersonation’da actor SuperAdmin doğrulaması |

### Bilinen boşluk (dokunmadan önce oku)

- **JWT `tenant_id` ↔ host subdomain:** `TenantContextMiddleware` auth sonrası accessor’ı JWT ile override edebilir; production’da host ile claim zorunlu eşleşmesi henüz middleware ile uygulanmıyor. Yeni güvenlik işi için `docs/MULTI_TENANT.md` “Known gaps” bölümüne bak.
- **Impersonation audit:** Sunucu logları var; `AuditLog.impersonated_by` alanı henüz yok.

## Kimlik doğrulama / yetkilendirme
- Auth: ASP.NET Core Identity + JWT.
- Yetki: `HasPermission(...)` tabanlı policy yaklaşımı ana standarttır; rol matrisi `backend/Authorization/RolePermissionMatrix.cs`.
- Yeni endpointlerde public gereksinim yoksa auth varsayımıyla ilerle.

## Gutschein / voucher
- Düz metin voucher kodu **loglanmamalı**, **kalıcı olarak saklanmamalı** (hash + maskeli gösterim modeli).
- POS offline kuyruğa voucher sırrı **asla** yazılmamalı (`pendingPaymentQueue.ts` + `paymentService.ts`).

## Fiscal/compliance hassas alanlar
- TSE imza ve doğrulama akışları; istemci bayraklarıyla imza/TSE bypass yok.
- **İmza zinciri** ve **`signature_chain_state` / receipt sequence** tutarlılığı
- RKSV özel fiş yaşam döngüsü (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg)
- **Decommissioned** kasa: yeni oturum/ödeme kabul edilmemeli (backend guardrail’leri nihai)
- Daily closing / rapor kapanışları
- FinanzOnline: oturum SOAP yolu yapılandırmaya bağlı olabilir; **RKSV Startbeleg/Jahresbeleg SOAP gönderimi bu repoda üretim-tamamlı değildir** (iskelet + Fake/Disabled varsayılanları)—yine de outbox/izleme tabloları hassastır.
- Audit log ve legal hold alanları

## Değişiklik kuralı
- Bu alanlarda davranış değişikliği yapmadan önce kapsamı daralt ve riskleri açık yaz.
- Sessiz hata yutma, audit düşürme, authorization gevşetme yapılmaz.
- Money/rounding davranışı mevcut üretim davranışıyla uyumlu kalmalıdır.
