import type { AxiosError } from 'axios';

import { isPublicAuthEntryPath } from '@/features/auth/utils/isPublicAuthEntryPath';
import { showAntdError } from '@/lib/antdAppBridge';
import { shouldSuppressCanceledRequestToast } from '@/lib/httpCancellation';

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
