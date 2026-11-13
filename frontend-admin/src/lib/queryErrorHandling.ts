import type { AxiosError } from 'axios';

import { isPublicAuthEntryPath } from '@/features/auth/utils/isPublicAuthEntryPath';
import {
  DEFAULT_TEXT_LOCALE,
  getCatalog,
  normalizeTextLocale,
} from '@/i18n/config';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { showAntdError } from '@/lib/antdAppBridge';
import { translateApiError } from '@/lib/api/errorTranslator';
import { shouldSuppressCanceledRequestToast } from '@/lib/httpCancellation';

const FRIENDLY_HTTP_STATUSES = new Set([400, 403, 404, 409]);

function catalogTranslate(key: string): string {
  const locale = normalizeTextLocale(getStoredLanguage() ?? DEFAULT_TEXT_LOCALE);
  const catalog = getCatalog(locale) as Record<string, unknown>;
  const [namespace, ...path] = key.split('.');
  let current: unknown = catalog[namespace];
  for (const segment of path) {
    if (!current || typeof current !== 'object') return key;
    current = (current as Record<string, unknown>)[segment];
  }
  return typeof current === 'string' ? current : key;
}

export function getHttpStatusFromError(error: unknown): number | undefined {
  const e = error as {
    response?: { status?: number };
    normalized?: { status?: number };
  };
  return e?.response?.status ?? e?.normalized?.status;
}

export function shouldSuppressPublicAuthEntry401Toast(error: unknown): boolean {
  return getHttpStatusFromError(error) === 401 && isPublicAuthEntryPath();
}

export type QueryErrorHandlerMeta = {
  /** Per-query/mutation override; runs after login-401 suppression check. */
  errorHandler?: (error: unknown) => void;
  /**
   * When true, shows a generic toast for unhandled query/mutation errors (off by default).
   * Login-page 401 is always suppressed.
   */
  showErrorToast?: boolean;
};

function defaultQueryErrorHandler(error: unknown): void {
  if (shouldSuppressPublicAuthEntry401Toast(error)) {
    return;
  }
  const axiosError = error as AxiosError;
  const status = getHttpStatusFromError(error);
  if (status != null && FRIENDLY_HTTP_STATUSES.has(status)) {
    showAntdError(translateApiError(catalogTranslate, error, { skipLog: true }));
    return;
  }
  const toastMessage =
    typeof axiosError?.message === 'string' && axiosError.message.trim().length > 0
      ? axiosError.message
      : status != null
        ? `Request failed (${status})`
        : 'Request failed';
  showAntdError(toastMessage);
}

export function invokeQueryClientErrorHandler(
  error: unknown,
  meta: QueryErrorHandlerMeta | undefined
): void {
  // Intentional aborts (navigate-away / AbortSignal) must never toast or run meta handlers.
  if (shouldSuppressCanceledRequestToast(error)) {
    return;
  }

  if (shouldSuppressPublicAuthEntry401Toast(error)) {
    return;
  }

  if (typeof meta?.errorHandler === 'function') {
    meta.errorHandler(error);
    return;
  }

  if (meta?.showErrorToast === true) {
    defaultQueryErrorHandler(error);
  }
}
