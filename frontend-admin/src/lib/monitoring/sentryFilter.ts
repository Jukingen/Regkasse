/**
 * Pure helpers for deciding which errors reach Sentry.
 * Kept free of `@sentry/nextjs` so unit tests stay lightweight.
 */

/** HTTP statuses that are expected UX noise (not production incidents). */
const IGNORED_HTTP_STATUSES = new Set([
  400, // validation
  401, // auth / session
  403, // permission / CSRF
  404, // not found / cross-tenant
  409, // conflict
  422, // unprocessable
  429, // rate limit (user-visible; not a crash)
]);

const NETWORK_ERROR_CODES = new Set([
  'ERR_NETWORK',
  'ECONNABORTED',
  'ETIMEDOUT',
  'ENOTFOUND',
  'ECONNREFUSED',
  'ECONNRESET',
  'ERR_INTERNET_DISCONNECTED',
  'ERR_CANCELED',
]);

const NETWORK_MESSAGE_RE =
  /network error|failed to fetch|load failed|net::err_|timeout|aborterror|the user aborted|request aborted/i;

export type AxiosLikeError = {
  message?: string;
  code?: string;
  name?: string;
  response?: { status?: number };
  config?: { url?: string; method?: string; signal?: AbortSignal };
  isAxiosError?: boolean;
};

export function isProductionSentryEnabled(
  nodeEnv: string | undefined,
  dsn: string | undefined | null
): boolean {
  return nodeEnv === 'production' && typeof dsn === 'string' && dsn.trim().length > 0;
}

export function getIgnoredHttpStatus(error: unknown): number | null {
  const status = (error as AxiosLikeError)?.response?.status;
  return typeof status === 'number' ? status : null;
}

export function isIgnoredHttpStatus(status: number | null | undefined): boolean {
  return typeof status === 'number' && IGNORED_HTTP_STATUSES.has(status);
}

/** True for cancelled / aborted client requests (not worth reporting). */
export function isCanceledLikeError(error: unknown): boolean {
  const err = error as AxiosLikeError;
  if (!err || typeof err !== 'object') {
    return false;
  }
  if (err.code === 'ERR_CANCELED' || err.name === 'CanceledError' || err.name === 'AbortError') {
    return true;
  }
  if (err.config?.signal?.aborted) {
    return true;
  }
  const message = typeof err.message === 'string' ? err.message : '';
  return message === 'canceled' || message === 'Query was cancelled' || /abort/i.test(message);
}

/**
 * Network / transport failures without a useful HTTP response.
 * Includes axios "Network Error" and browser fetch failures.
 */
export function isNetworkLikeError(error: unknown): boolean {
  if (isCanceledLikeError(error)) {
    return true;
  }
  const err = error as AxiosLikeError;
  if (!err || typeof err !== 'object') {
    return false;
  }
  if (err.response?.status != null) {
    return false;
  }
  if (typeof err.code === 'string' && NETWORK_ERROR_CODES.has(err.code)) {
    // Timeouts with ECONNABORTED are still "network-like" noise for Sentry volume.
    return true;
  }
  if (typeof err.message === 'string' && NETWORK_MESSAGE_RE.test(err.message)) {
    return true;
  }
  // Axios often sets isAxiosError with no response for DNS / offline.
  if (err.isAxiosError === true && err.response == null) {
    return true;
  }
  return false;
}

/**
 * Whether an axios (or axios-like) failure should be reported to Sentry.
 * Reports 5xx and unexpected client bugs; skips 4xx / network / cancel.
 */
export function shouldReportAxiosErrorToSentry(error: unknown): boolean {
  if (isCanceledLikeError(error) || isNetworkLikeError(error)) {
    return false;
  }
  const status = getIgnoredHttpStatus(error);
  if (status == null) {
    // Non-axios unexpected throw — allow reporter to decide via beforeSend.
    return true;
  }
  if (isIgnoredHttpStatus(status)) {
    return false;
  }
  // Server failures are production signals; other statuses stay out of the noise.
  return status >= 500;
}

/** Message substrings / regexes passed to Sentry `ignoreErrors`. */
export const SENTRY_IGNORE_ERRORS: Array<string | RegExp> = [
  'ResizeObserver loop',
  'ResizeObserver loop limit exceeded',
  'Non-Error promise rejection captured',
  /^Network Error$/i,
  /^Failed to fetch$/i,
  /^Load failed$/i,
  /^cancelled$/i,
  /^canceled$/i,
  /ChunkLoadError/i,
  /Loading chunk [\d]+ failed/i,
];

/**
 * `beforeSend` filter: drop events that match ignored HTTP / network patterns
 * encoded in event tags or exception values.
 */
export function shouldDropSentryEvent(event: {
  exception?: { values?: Array<{ value?: string; type?: string }> };
  tags?: Record<string, unknown>;
  message?: string;
}): boolean {
  const httpStatusRaw = event.tags?.httpStatus ?? event.tags?.['http.status_code'];
  const httpStatus =
    typeof httpStatusRaw === 'number'
      ? httpStatusRaw
      : typeof httpStatusRaw === 'string'
        ? Number.parseInt(httpStatusRaw, 10)
        : NaN;
  if (Number.isFinite(httpStatus) && isIgnoredHttpStatus(httpStatus)) {
    return true;
  }

  const values = event.exception?.values ?? [];
  for (const v of values) {
    const text = `${v.type ?? ''} ${v.value ?? ''}`;
    if (NETWORK_MESSAGE_RE.test(text)) {
      return true;
    }
  }
  if (typeof event.message === 'string' && NETWORK_MESSAGE_RE.test(event.message)) {
    return true;
  }
  return false;
}
