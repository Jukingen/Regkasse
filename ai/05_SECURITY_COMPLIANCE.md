# Security & Compliance

## Kimlik doğrulama / yetkilendirme
- Auth: ASP.NET Core Identity + JWT.
- Yetki: `HasPermission(...)` tabanlı policy yaklaşımı ana standarttır.
- Yeni endpointlerde public gereksinim yoksa auth varsayımıyla ilerle.

## Fiscal/compliance hassas alanlar
- TSE imza ve doğrulama akışları
- Receipt zinciri ve sequence yönetimi
- Daily closing / rapor kapanışları
- FinanzOnline gönderim, outbox, reconciliation
- Audit log ve legal hold alanları

## Değişiklik kuralı
- Bu alanlarda davranış değişikliği yapmadan önce kapsamı daralt ve riskleri açık yaz.
- Sessiz hata yutma, audit düşürme, authorization gevşetme yapılmaz.
- Money/rounding davranışı mevcut üretim davranışıyla uyumlu kalmalıdır.
