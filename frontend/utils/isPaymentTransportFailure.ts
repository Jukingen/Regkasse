/**
 * Detects axios/fetch transport failures (no HTTP response) so POS can enqueue
 * non-fiscal offline payments when the connection drops mid-request.
 */
export function isPaymentTransportFailure(error: unknown): boolean {
  const err = error as {
    response?: unknown;
    message?: string;
    code?: string;
  };
  const msg = typeof err?.message === 'string' ? err.message : '';
  if (msg.includes('Token expired')) return false;
  if (err?.response != null) return false;
  if (err?.code === 'ECONNABORTED' || err?.code === 'ERR_NETWORK') return true;
  if (msg === 'Network Error' || /network/i.test(msg)) return true;
  if (/aborted|timeout/i.test(msg)) return true;
  return false;
}
