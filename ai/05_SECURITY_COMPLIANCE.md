# Security & Compliance

## Yasal / uyumluluk iddiası (kritik)
- Bu repodaki dokümantasyon veya kod yorumları **yasal uyumluluk garantisi vermez**. Özellikle BMF/FinanzOnline kabulü, TSE donanım onayı veya resmi DEP/RKSV beyanı iddia edilmez.
- **Fiscal export** (`GET /api/admin/fiscal-export`, `FiscalExportService`): paketler açık **“not legal proof”** uyarısı taşır; tanılama, iç analiz ve operasyonel el değişimi içindir; resmi RKSV kanıtı veya FinanzOnline yerine geçen belge değildir (`REGKASSE_AI_ONBOARDING.md`).
- **DEP §7 export** (`GET /api/admin/rksv/dep-export`, `RksvDepExportService`): BMF Signaturjournal formatı (F1–F5 tamamlandı); Prüftool ile doğrulanabilir hedeflenir ancak yine de yasal kabul garantisi verilmez. Resmi fiscal export’tan ayrıdır; `docs/DEP_EXPORT_DEVELOPMENT.md`.

## Multi-Tenant Architecture

- Kiracı verisi EF global query filter ile ayrılır; yetkisiz çapraz kiracı erişimde **404** (bilgi sızıntısı yok).
- Super Admin: `SuperAdmin` rolü + `/api/admin/tenants`; impersonation kısa ömürlü token — `audit_logs.impersonated_by` / `impersonated_tenant` doldurulur.
- **Production (Single POS UI):** `pos` / `api` / `admin` reserved host’larında pre-auth tenant bind yok; oturum sonrası **JWT `tenant_id`** otoriter (`TenantContextMiddleware`). `{slug}.regkasse.at` POS girişi değildir — `docs/POS_PRODUCTION_ARCHITECTURE.md`.
- **Development:** `X-Tenant-Id` / `?tenant=` yalnızca `IsDevelopment()`; Production’da kapalı.
- **Host / custom domain:** legacy slug host ve müşteri siteleri (`TenantDomain` → slug) için Host çözümlemesi devam eder; storefront `frontend-sites`.
- Offline: iki ayrı sistem (`offline_transactions` vs `offline_orders`); voucher sırrı hiçbir offline kuyruğa yazılmaz.

Tam mimari: `docs/MULTI_TENANT.md`.

## Multi-Tenant Security

### Tenant isolation guarantees

| Garanti | Uygulama |
|--------|-----------|
| Veritabanı seviyesi filtreleme | `AppDbContext` global query filter tüm `ITenantEntity` tiplerinde; API istemcisi filtre bypass edemez |
| Singleton + EF | Root’tan scoped `AppDbContext` çözümü yasak; `IServiceScopeFactory` zorunlu (`LicenseService` örnek) |
| Accessor null | `TenantId == null` iken filtre devre dışı — yalnızca bilinçli kod yolları; normal API isteği önce tenant set eder |
| Cross-tenant IDOR | **404** (403 değil) — `TenantIsolationTests.AdminPayments_GetById_CrossTenant_Returns404_Not403` |
| `tenant_id` stamp | `offline_transactions.tenant_id` ve `offline_orders.tenant_id` NOT NULL; insert’te ambient / register tenant’tan stamp |
| Platform tablolar | Identity / `tenants` kiracı-scoped değil; iş tabloları (ör. `Customer`) `ITenantEntity` — değişiklik öncesi envanter çıkar |

### Tenant spoofing prevention

| Önlem | Uygulama |
|--------|-----------|
| Production POS/API | Reserved hosts + JWT `tenant_id`; `X-Tenant-Id` / `?tenant=` yok |
| Host slug parsing | `SubdomainTenantProvider` / `TenantDomain` — legacy slug host ve siteler; POS production entry değil |
| Dev header’lar production’da kapalı | `ASPNETCORE_ENVIRONMENT=Production` |
| Super Admin ek kontrol | `[Authorize(Roles = SuperAdmin)]` + impersonation’da actor SuperAdmin doğrulaması |

### Bilinen boşluk (dokunmadan önce oku)

- **JWT `tenant_id` ↔ Host eşleşmesi:** Reserved `pos`/`api`/`admin` host’larında subdomain zorunluluğu yok; legacy slug host’larda claim↔Host zorunlu eşleşmesi middleware ile henüz uygulanmıyor — `docs/MULTI_TENANT.md` “Known gaps”.
- Impersonation audit kolonları **mevcut**; FA legacy `{slug}` handoff vs shared `admin.regkasse.at` hedefi için `docs/IMPERSONATION_FLOW.md`.

## Kimlik doğrulama / yetkilendirme
- Auth: ASP.NET Core Identity + JWT.
- Yetki: `HasPermission(...)` tabanlı policy yaklaşımı ana standarttır; rol matrisi `backend/Authorization/RolePermissionMatrix.cs`.
- **Admin FA oturumu:** Login ve `/me` yanıtında izinler `AdminAppPermissionProfile` ile filtrelenir (`app_context=admin`) — Cashier whitelist, Manager için POS-terminal strip. Menü/route sözleşmesi: `frontend-admin` `test:contract`, backend `RoleAdminMenuContractTests`.
- **Zugriff & Rollen hub:** `/admin/access`, `/admin/users`, `/admin/access/roles`, `/admin/access/matrix` — ayrıntı `frontend-admin/docs/ACCESS_AND_ROLES_HUB.md`.
- **SuperAdmin 2FA:** TOTP; Dev bypass — `docs/AUTH_TWO_FACTOR.md`.
- **CSRF:** `Security:Csrf` + `CsrfMiddleware` (mutation’larda double-submit); Dev bypass mümkün — `AGENTS.md` § CSRF.
- **Working hours:** yalnızca website/app online-order intake; POS/FA API’lerini asla kapatma — `docs/WORKING_HOURS.md`.
- **Backup RBAC:** Mandanten-Admin `backup.manage` (tenant); System dump / restore Super Admin — `ai/modules/backup_permissions.md`.
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
