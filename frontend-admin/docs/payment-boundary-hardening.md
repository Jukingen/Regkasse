# Payment Boundary Hardening (Admin vs POS)

Bu not, admin ve POS payment sinirlarini canonical API yuzeylerine hizalar.

## Canonical boundary

- `frontend-admin`:
  - Admin payment browse/detail/cancel/refund/statistics canonical `/api/admin/payments/*` generated client uzerinden kullanilir.
  - `src/api/legacy/payment.ts` sadece POS legacy uyumluluk yardimcilari (date-range/id/cancel/refund) icin tutulur.
- `frontend` (POS/mobile):
  - Canonical payment lane `services/api/paymentService.ts` + `services/api/posPaymentPaths.ts` ile `/api/pos/payment/*`.
  - POS tarafi admin legacy wrapper import etmez.

## Neden bu sinir var?

- Admin payment UX ve contract artik state-rich admin DTO'larina (`/api/admin/payments`) dayanir.
- POS tarafi fiskal akisin kanonik uygulama noktasi; `/api/pos/payment` disina cikmak uyum/risk olusturur.
- Karisiklik (admin sayfadan pos client importu veya tersi) ileride sessiz regressions uretir.

## Bu sprintteki drift temizligi

- `frontend-admin/src/app/(protected)/payments/page.tsx`:
  - `@/api/generated/admin/admin` hooks/mutations ile canonical admin payment surface'e tasindi.
  - Legacy admin containment wrapper bagimliligi kaldirildi.

## Not

- Bu sprintte aktif admin caller'lar canonical admin payment endpoints'e alinmistir.
- `src/api/legacy/payment.ts` dosyasinda kalan fonksiyonlar sadece POS legacy uyumlulugu icindir.
