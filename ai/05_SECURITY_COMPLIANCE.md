# Security & Compliance

## Yasal / uyumluluk iddiası (kritik)
- Bu repodaki dokümantasyon veya kod yorumları **yasal uyumluluk garantisi vermez**. Özellikle BMF/FinanzOnline kabulü, TSE donanım onayı veya resmi DEP/RKSV beyanı iddia edilmez.
- **Fiscal export** (`GET /api/admin/fiscal-export`, `FiscalExportService`): paketler açık **“not legal proof”** uyarısı taşır; tanılama, iç analiz ve operasyonel el değişimi içindir; resmi RKSV kanıtı veya FinanzOnline yerine geçen belge değildir (`REGKASSE_AI_ONBOARDING.md`).

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
