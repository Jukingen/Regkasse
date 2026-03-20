# FinanzOnline Operations Console (Admin)

Bu sprintte `frontend-admin` tarafinda FinanzOnline yalnizca "status karti + reconciliation queue" gorunumunden cikarilip operasyonel bir destek konsoluna genisletildi.

## Admin'de yeni yuzeye cikarilan backend kabiliyetleri

- `GET /api/FinanzOnline/status` (detayli durum)
- `GET /api/FinanzOnline/config` (read-only konfigurasyon gorunumu)
- `POST /api/FinanzOnline/test-connection` (manual baglanti testi)
- `GET /api/FinanzOnline/errors` (recent error listesi)
- `GET /api/FinanzOnline/history/{invoiceId}` (invoice bazli submission history)

## Bilerek gizli birakilanlar

- `POST /api/FinanzOnline/submit-invoice` admin konsola eklenmedi.
  - Endpoint backend tarafinda backward-compat/deprecated notu tasiyor.
  - Operasyonel olarak mutating action ve reconciliation akisiyla karisabilecek risk tasiyor.
  - Admin tarafinda mutating FO operasyonu icin mevcut guvenli yol `FinanzOnline Abgleich` (retry/reconciliation) sayfasi olarak korunuyor.

## UX siniri

- Sayfa, "pasif diagnostik" (status/config/errors/history) ve "kontrollu aksiyon" (test connection) ayrimini net tutar.
- Reconciliation queue davranisi korunur; yeni konsol onu degistirmez, tamamlar.
