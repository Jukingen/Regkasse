# Critical: High-risk areas (change only with explicit scope)

## 1) Cart → Payment → Receipt → DailyClosing
- Bu zincirde davranış değişikliği finansal ve yasal risk üretir.
- İstenen değişiklik dışında refactor/rewrite yapma.

## 2) TSE ve signature chain
- TSE imza üretimi, sequence/state ve doğrulama adımları korunmalı.
- İmza payload alanlarını ve akış sırasını sebepsiz değiştirme.

## 3) FinanzOnline / outbox / reconciliation
- Mapping alanları, retry taxonomisi, reconciliation semantiği hassastır.
- Hata yutma veya audit izini azaltan değişiklik yapılmaz.

## 4) Authorization/RBAC
- Permission adları, role-permission matrix ve guard akışları hassastır.
- Endpoint yetkilerini gevşetme; değişim varsa açık migration planı yaz.

## 5) Money precision / rounding
- Para hesaplarında mevcut precision ve rounding davranışını koru.

> Emin değilsen varsayım yapma: önce kapsamı daralt, risk ve belirsizliği açık yaz.
