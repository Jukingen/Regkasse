export type PaymentResultQuery = {
  paymentIntentId?: string;
  paymentIntentClientSecret?: string;
  redirectStatus?: string;
  onlinePaymentId?: string;
};

function firstParam(
  params: Record<string, string | string[] | undefined> | null | undefined,
  keys: string[]
): string | undefined {
  if (!params) return undefined;
  for (const key of keys) {
    const value = params[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
    if (Array.isArray(value) && value[0]?.trim()) return value[0].trim();
  }
  return undefined;
}

/** Stripe return / 3DS query params. Never log client_secret. */
export function parsePaymentResultParams(
  params: Record<string, string | string[] | undefined> | null | undefined
): PaymentResultQuery {
  return {
    paymentIntentId: firstParam(params, [
      'payment_intent',
      'paymentIntent',
      'paymentIntentId',
    ]),
    paymentIntentClientSecret: firstParam(params, [
      'payment_intent_client_secret',
      'paymentIntentClientSecret',
    ]),
    redirectStatus: firstParam(params, ['redirect_status', 'redirectStatus', 'status', 'state']),
    onlinePaymentId: firstParam(params, ['onlinePaymentId', 'id', 'paymentId']),
  };
}

export function isRedirectStatusSucceeded(status?: string): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'succeeded' || s === 'success' || s === 'paid' || s === 'completed';
}

export function isRedirectStatusFailed(status?: string): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'failed' || s === 'fail' || s === 'error' || s === 'declined';
}

export function isRedirectStatusCancelled(status?: string): boolean {
  const s = (status ?? '').toLowerCase();
  return s === 'canceled' || s === 'cancelled';
}
