/**
 * Opt-in POS payment flow tracing. Enable with __DEV__ or set EXPO_PUBLIC_POS_PAYMENT_DEBUG=1.
 */

export const POS_PAYMENT_DEBUG =
  (typeof __DEV__ !== 'undefined' && __DEV__) ||
  (typeof process !== 'undefined' &&
    typeof process.env?.EXPO_PUBLIC_POS_PAYMENT_DEBUG === 'string' &&
    process.env.EXPO_PUBLIC_POS_PAYMENT_DEBUG === '1');

const PREFIX = '[POS_PAYMENT_TRACE]';

/**
 * Searchable console trace for POS payment debugging.
 * Warnings for blocked/error-style events when debug is on.
 */
export function debugPosPaymentTrace(event: string, data?: unknown): void {
  if (!POS_PAYMENT_DEBUG) return;
  const isWarn =
    event.includes('blocked') ||
    event.includes('_error') ||
    event.includes('error') ||
    event.includes('fail');
  if (isWarn) {
    if (data !== undefined) console.warn(`${PREFIX} ${event}`, data);
    else console.warn(`${PREFIX} ${event}`);
  } else {
    if (data !== undefined) console.log(`${PREFIX} ${event}`, data);
    else console.log(`${PREFIX} ${event}`);
  }
}
