import { POS_PAYMENT_DEBUG } from './debugPosPaymentTrace';
import { safeLog } from './loggingUtils';

/** True when POS payment debug flag is on (same as payment trace). */
export const AUTH_TRACE = POS_PAYMENT_DEBUG;

export function authTrace(event: string, data?: unknown): void {
  if (!AUTH_TRACE) return;
  const tag = '[AUTH_TRACE]';
  if (data !== undefined) safeLog(`${tag} ${event}`, data);
  else safeLog(`${tag} ${event}`);
}
