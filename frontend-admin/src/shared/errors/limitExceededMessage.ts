/**
 * HTTP 409 `LIMIT_EXCEEDED` (`LimitErrorDto`) → localized operator copy.
 * Interpolates `limit` / `current`; never shows the English backend `message`.
 */

export const LIMIT_EXCEEDED_CODE = 'LIMIT_EXCEEDED';
export const LIMIT_ERROR_TOAST_DURATION_SECONDS = 5;

export type LimitExceededTranslateFn = (
  key: string,
  options?: Record<string, string | number>
) => string;

export type LimitExceededPayload = {
  limitKey?: string;
  limit?: number;
  current?: number;
};

const KNOWN_LIMIT_KEYS = new Set([
  'maxActiveRegistersPerUser',
  'maxProductsPerTenant',
  'maxUsersPerTenant',
  'dailyMaxTransactions',
  'maxTransactionAmount',
  'dailyMaxRevenue',
  'maxBackupsPerTenant',
  'maxBackupSizeMB',
  'maxOfflineTransactions',
]);

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value != null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
}

function readNumeric(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) ? value : undefined;
}

function isLimitExceededCode(code: string | undefined): boolean {
  return typeof code === 'string' && code.trim().toUpperCase() === LIMIT_EXCEEDED_CODE;
}

function pickLimitSource(
  data: Record<string, unknown> | undefined
): Record<string, unknown> | undefined {
  if (!data) return undefined;
  return asRecord(data.limitError) ?? data;
}

/**
 * Reads classic 409 `LimitErrorDto` or a nested `limitError` envelope (payment v2).
 */
export function readLimitExceededPayload(error: unknown): LimitExceededPayload | undefined {
  const root = asRecord(error);
  const response = asRecord(root?.response);
  const data = asRecord(response?.data) ?? asRecord(root?.data);
  const source = pickLimitSource(data);
  if (!source) return undefined;

  const code =
    (typeof source.code === 'string' ? source.code : undefined) ??
    (typeof data?.code === 'string' ? data.code : undefined);
  const limitKey =
    (typeof source.limitKey === 'string' ? source.limitKey : undefined) ??
    (typeof data?.limitKey === 'string' ? data.limitKey : undefined);

  if (!isLimitExceededCode(code) && !(limitKey && KNOWN_LIMIT_KEYS.has(limitKey))) {
    return undefined;
  }

  return {
    limitKey,
    limit: readNumeric(source.limit) ?? readNumeric(data?.limit),
    current: readNumeric(source.current) ?? readNumeric(data?.current),
  };
}

export function isLimitExceededError(error: unknown): boolean {
  return readLimitExceededPayload(error) != null;
}

export function limitExceededI18nKey(limitKey?: string): string {
  switch (limitKey) {
    case 'maxProductsPerTenant':
      return 'tenants.limits.errors.maxProductsPerTenant';
    case 'maxUsersPerTenant':
      return 'tenants.limits.errors.maxUsersPerTenant';
    case 'maxActiveRegistersPerUser':
      return 'tenants.limits.errors.maxActiveRegistersPerUser';
    case 'maxBackupsPerTenant':
      return 'tenants.limits.errors.maxBackupsPerTenant';
    case 'maxBackupSizeMB':
      return 'tenants.limits.errors.maxBackupSizeMB';
    case 'dailyMaxTransactions':
      return 'tenants.limits.errors.dailyMaxTransactions';
    case 'maxTransactionAmount':
      return 'tenants.limits.errors.maxTransactionAmount';
    case 'dailyMaxRevenue':
      return 'tenants.limits.errors.dailyMaxRevenue';
    case 'maxOfflineTransactions':
      return 'tenants.limits.errors.maxOfflineTransactions';
    default:
      return 'tenants.limits.errors.generic';
  }
}

export function translateLimitExceededError(
  t: LimitExceededTranslateFn,
  error: unknown
): string | undefined {
  const payload = readLimitExceededPayload(error);
  if (!payload) return undefined;
  return t(limitExceededI18nKey(payload.limitKey), {
    limit: payload.limit ?? 0,
    current: payload.current ?? 0,
  });
}

export function translateLimitExceededOr(
  t: LimitExceededTranslateFn,
  error: unknown,
  fallback: string
): string {
  return translateLimitExceededError(t, error) ?? fallback;
}

type ErrorToastApi = {
  error: (content: string | { content: string; duration?: number }) => void;
};

/** Toast: localized limit copy (5s) when the error is `LIMIT_EXCEEDED`, otherwise `fallback`. */
export function toastLimitExceededOrFallback(
  messageApi: ErrorToastApi,
  t: LimitExceededTranslateFn,
  error: unknown,
  fallback: string
): void {
  const limitMsg = translateLimitExceededError(t, error);
  if (limitMsg) {
    messageApi.error({ content: limitMsg, duration: LIMIT_ERROR_TOAST_DURATION_SECONDS });
    return;
  }
  messageApi.error(fallback);
}
