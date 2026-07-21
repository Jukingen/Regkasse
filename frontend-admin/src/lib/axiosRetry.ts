import axios, { AxiosError, AxiosRequestConfig, CanceledError } from 'axios';

/** Max automatic retries for safe (idempotent) requests. */
export const AXIOS_MAX_RETRIES = 3;

/** Exponential backoff delays: 1s, 2s, 4s. */
export const AXIOS_RETRY_DELAYS_MS = [1000, 2000, 4000] as const;

const IDEMPOTENT_METHODS = new Set(['get', 'head', 'options']);

/** Mutations that must never be auto-retried (duplicate side effects). */
const NON_RETRYABLE_METHODS = new Set(['post', 'put', 'patch', 'delete']);

export type AxiosRetryConfig = AxiosRequestConfig & {
  /**
   * When `false`, skip automatic network/429/5xx retries for this request.
   * Default: retries enabled for GET/HEAD/OPTIONS when the error is retryable.
   */
  retry?: boolean;
  /** Internal: 401 token-refresh already attempted. */
  _retry?: boolean;
  /** Internal: network/5xx retry attempt count (0 = none yet). */
  _retryCount?: number;
};

export function isIdempotentMethod(method?: string): boolean {
  const normalized = (method ?? 'get').toLowerCase();
  return IDEMPOTENT_METHODS.has(normalized);
}

export function isMutationMethod(method?: string): boolean {
  const normalized = (method ?? 'get').toLowerCase();
  return NON_RETRYABLE_METHODS.has(normalized);
}

/**
 * True when the caller aborted/cancelled the request.
 * Timeouts (`ECONNABORTED`) are NOT treated as cancellation — they remain retryable.
 * Legacy CancelToken / `axios.Cancel` still work via {@link axios.isCancel}; prefer {@link CanceledError}.
 */
export function isAbortCancellation(error: unknown): boolean {
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

/**
 * Network failure (no HTTP response), HTTP 429 (rate limit), or 5xx server error.
 * Callers must still restrict retries to idempotent methods (GET/HEAD/OPTIONS).
 */
export function isRetryableAxiosError(error: unknown): boolean {
  if (isAbortCancellation(error)) {
    return false;
  }
  const axiosError = error as AxiosError;
  const status = axiosError.response?.status;
  if (status == null) {
    // No response: network error, DNS failure, connection refused, timeout, etc.
    return true;
  }
  if (status === 429) {
    return true;
  }
  return status >= 500 && status < 600;
}

export function getRetryDelayMs(attemptNumber: number): number {
  const index = Math.max(0, attemptNumber - 1);
  if (index < AXIOS_RETRY_DELAYS_MS.length) {
    return AXIOS_RETRY_DELAYS_MS[index];
  }
  return (
    AXIOS_RETRY_DELAYS_MS[AXIOS_RETRY_DELAYS_MS.length - 1] *
    2 ** (index - AXIOS_RETRY_DELAYS_MS.length + 1)
  );
}

/**
 * Whether this failed request should be retried automatically.
 * Never retries mutations; respects `config.retry === false`.
 */
export function shouldRetryAxiosRequest(
  config: AxiosRetryConfig | undefined,
  error: unknown
): boolean {
  if (!config) {
    return false;
  }
  if (config.retry === false) {
    return false;
  }
  if (isMutationMethod(config.method) || !isIdempotentMethod(config.method)) {
    return false;
  }
  if (!isRetryableAxiosError(error)) {
    return false;
  }
  const count = config._retryCount ?? 0;
  return count < AXIOS_MAX_RETRIES;
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}
