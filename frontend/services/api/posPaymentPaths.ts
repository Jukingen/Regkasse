/**
 * Canonical POS payment HTTP prefix for the mobile app.
 * Axios `baseURL` is configured with `/api` (see `config.ts`); paths here are relative to that base
 * → effective URLs: `/api/pos/payment/*`.
 *
 * Do not duplicate `/pos/payment` or raw `/Payment` path fragments elsewhere in POS — import helpers here and call through `paymentService`.
 *
 * Admin payments/reporting use `/api/admin/payments` (and related admin surfaces), never this POS prefix.
 * Legacy `/api/Payment/*` is deprecated (Sunset 2026-09-30); do not reintroduce it in clients.
 */
export const POS_PAYMENT_API_PREFIX = '/pos/payment';

/** Relative path for GET payment methods list (append `?cashRegisterId=`). */
export const POS_PAYMENT_METHODS_PATH = `${POS_PAYMENT_API_PREFIX}/methods` as const;

export function posPaymentMethodsPath(cashRegisterId: string): string {
  const id = encodeURIComponent(cashRegisterId.trim());
  return `${POS_PAYMENT_METHODS_PATH}?cashRegisterId=${id}`;
}

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
