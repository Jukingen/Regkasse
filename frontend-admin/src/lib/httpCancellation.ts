import axios, { CanceledError } from 'axios';

/**
 * True when the request was aborted/cancelled (TanStack Query navigate-away, AbortSignal, Orval cancel).
 * Also treats non-timeout `ECONNABORTED` quietly for interceptor logging (timeouts stay visible).
 *
 * Prefer {@link CanceledError} / AbortSignal over the legacy CancelToken API (deprecated since axios 0.22).
 */
export function isRequestCanceled(error: unknown): boolean {
  if (axios.isCancel(error) || error instanceof CanceledError) {
    return true;
  }
  const err = error as {
    message?: string;
    code?: string;
    name?: string;
    config?: { signal?: AbortSignal };
  };
  if (err.code === 'ERR_CANCELED') {
    return true;
  }
  if (err.code === 'ECONNABORTED') {
    const msg = (err.message ?? '').toLowerCase();
    // Timeouts are real failures; other aborts stay quiet in logs.
    return !msg.includes('timeout');
  }
  if (err.name === 'AbortError' || err.name === 'CanceledError') {
    return true;
  }
  const message = err.message;
  if (message === 'canceled' || message === 'Query was cancelled') {
    return true;
  }
  if (err.config?.signal?.aborted) {
    return true;
  }
  return false;
}

/** User-facing toasts / QueryCache onError — never surface intentional aborts. */
export function shouldSuppressCanceledRequestToast(error: unknown): boolean {
  return isRequestCanceled(error);
}
