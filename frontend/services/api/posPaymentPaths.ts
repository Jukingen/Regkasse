/**
 * Canonical POS payment HTTP prefix for the mobile app.
 * Axios `baseURL` is configured with `/api` (see `config.ts`); paths here are relative to that base
 * → effective URLs: `/api/pos/payment/*`.
 *
 * Do not duplicate `/pos/payment` or raw `/Payment` path fragments elsewhere in POS — import helpers here and call through `paymentService`.
 *
 * ADMIN: `frontend-admin/src/api/legacy/payment.ts` intentionally uses `/api/Payment/*` — never merge with POS.
 */
export const POS_PAYMENT_API_PREFIX = '/pos/payment';

/** Relative path for GET/POST payment methods list. */
export const POS_PAYMENT_METHODS_PATH = `${POS_PAYMENT_API_PREFIX}/methods` as const;

/** POST body: `{ voucherCode, amount? }` — relative to `/api`. */
export const POS_VOUCHERS_VALIDATE_PATH = '/pos/vouchers/validate' as const;

/** `/pos/payment/{paymentId}[/{tail}]` */
export function posPaymentByIdPath(paymentId: string, tail?: string): string {
  const id = encodeURIComponent(paymentId);
  return tail
    ? `${POS_PAYMENT_API_PREFIX}/${id}/${tail.replace(/^\/+/, '')}`
    : `${POS_PAYMENT_API_PREFIX}/${id}`;
}

/** Full URL for QR PNG fetch (fetch() does not use axios baseURL). */
export function posPaymentQrPngAbsoluteUrl(apiBaseUrl: string, paymentId: string): string {
  const base = apiBaseUrl.replace(/\/+$/, '');
  return `${base}${POS_PAYMENT_API_PREFIX}/${encodeURIComponent(paymentId)}/qr.png`;
}
