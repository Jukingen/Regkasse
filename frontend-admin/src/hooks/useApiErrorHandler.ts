'use client';

/**
 * Standardized API error surfacing for FA features (toast / notification / message string).
 * Builds on `translateApiError` / `getUserFacingApiErrorMessage` + NotificationService.
 */
import { useCallback } from 'react';

import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { translateApiError } from '@/lib/api/errorTranslator';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';
import { buildTechnicalApiErrorPayload } from '@/shared/errors/technicalApiErrorLog';
import type { UserFacingApiErrorOptions } from '@/shared/errors/userFacingApiError';

export type ApiErrorHandlerOptions = UserFacingApiErrorOptions;

export function useApiErrorHandler() {
  const { t } = useI18n();
  const notify = useNotify();

  const getMessage = useCallback(
    (error: unknown, options: ApiErrorHandlerOptions) => translateApiError(t, error, options),
    [t]
  );

  const logError = useCallback((error: unknown, logContext: string) => {
    technicalConsole.error(
      `[API Error] ${logContext}`,
      buildTechnicalApiErrorPayload(normalizeApiError(error))
    );
  }, []);

  /** Ant Design message toast (preferred for mutation failures). */
  const notifyError = useCallback(
    (error: unknown, options: ApiErrorHandlerOptions) => {
      notify.apiError(error, options);
    },
    [notify]
  );

  /** Ant Design notification (preferred for page-level / long-lived failures). */
  const notifyErrorNotification = useCallback(
    (error: unknown, options: ApiErrorHandlerOptions & { titleKey?: string }) => {
      notify.apiErrorNotification(error, options);
    },
    [notify]
  );

  return {
    t,
    getMessage,
    logError,
    notifyError,
    notifyErrorNotification,
  };
}
