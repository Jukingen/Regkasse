import type { AxiosError, AxiosRequestConfig } from 'axios';

/** Header tracking how many network retries were already attempted. */
export const NETWORK_RETRY_HEADER = 'x-network-retry-count';

export const MAX_NETWORK_RETRIES = 2;
export const NETWORK_RETRY_BASE_DELAY_MS = 300;

type RetryableConfig = AxiosRequestConfig & {
  headers?: Record<string, unknown>;
};

/**
 * True when the failure looks like transport-level (no HTTP response).
 * Does not treat HTTP 5xx as a network error — callers handle those separately.
 */
export function isAxiosNetworkError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  const ax = error as AxiosError;
  if (ax.response) return false;
  if (ax.code === 'ERR_CANCELED' || ax.name === 'CanceledError') return false;
  const code = ax.code ?? '';
  return (
    code === 'ERR_NETWORK' ||
    code === 'ECONNABORTED' ||
    code === 'ETIMEDOUT' ||
    code === 'ENOTFOUND' ||
    code === 'ECONNRESET' ||
    ax.message === 'Network Error'
  );
}

function normalizeMethod(method: string | undefined): string {
  return (method ?? 'get').toLowerCase();
}

/**
 * Auto-retry only for idempotent reads. Never retry payment / auth / fiscal mutations
 * (duplicate POSTs can create double charges or duplicate receipts).
 */
export function isIdempotentHttpMethod(method: string | undefined): boolean {
  const m = normalizeMethod(method);
  return m === 'get' || m === 'head' || m === 'options';
}

export function isNonRetryableApiPath(url: string | undefined): boolean {
  if (!url) return false;
  const path = url.split('?')[0]?.toLowerCase() ?? '';
  return (
    path.includes('/auth/login') ||
    path.includes('/auth/refresh') ||
    path.includes('/payment') ||
    path.includes('/pos/payment') ||
    path.includes('/card-payment') ||
    path.includes('/tagesabschluss') ||
    path.includes('/rksv/') ||
    path.includes('/tse/signature') ||
    path.includes('/vouchers')
  );
}

export function getNetworkRetryCount(config: RetryableConfig | undefined): number {
  const headers = config?.headers;
  if (!headers) return 0;
  const raw =
    headers[NETWORK_RETRY_HEADER] ??
    (typeof (headers as { get?: (k: string) => unknown }).get === 'function'
      ? (headers as { get: (k: string) => unknown }).get(NETWORK_RETRY_HEADER)
      : undefined);
  const n = typeof raw === 'string' || typeof raw === 'number' ? Number(raw) : NaN;
  return Number.isFinite(n) && n >= 0 ? n : 0;
}

export function shouldRetryAxiosNetworkError(error: unknown): boolean {
  if (!isAxiosNetworkError(error)) return false;
  const ax = error as AxiosError;
  const config = ax.config as RetryableConfig | undefined;
  if (!config) return false;
  if (config.signal?.aborted) return false;
  if (!isIdempotentHttpMethod(config.method)) return false;
  if (isNonRetryableApiPath(config.url)) return false;
  return getNetworkRetryCount(config) < MAX_NETWORK_RETRIES;
}

export function networkRetryDelayMs(attemptAfterFailure: number): number {
  // attemptAfterFailure is 1-based count of the retry about to run
  const exp = Math.max(0, attemptAfterFailure - 1);
  return NETWORK_RETRY_BASE_DELAY_MS * 2 ** exp;
}

export function withIncrementedRetryCount(config: RetryableConfig): RetryableConfig {
  const next = getNetworkRetryCount(config) + 1;
  return {
    ...config,
    headers: {
      ...(config.headers ?? {}),
      [NETWORK_RETRY_HEADER]: String(next),
    },
  };
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Safe AbortController factory for POS API callers. */
export function createApiAbortController(): AbortController {
  return new AbortController();
}
