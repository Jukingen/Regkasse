/**
 * API errors: short localized user message; raw text is logged only via technicalConsole (English labels).
 * Code-based translation: `registerApiErrorCodeTranslation` + backend `code` (normalizeApiError).
 */
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { getRegisteredMessageKeyForApiErrorCode } from './apiErrorCodeRegistry';
import type { NormalizedApiError } from './normalizedApiError';
import { normalizeApiError } from './normalizedApiError';
import { buildTechnicalApiErrorPayload } from './technicalApiErrorLog';

export type TranslateFn = (key: string, options?: Record<string, string | number>) => string;

export type UserFacingApiErrorOptions = {
  /** Default for unknown / unmatched cases */
  fallbackKey?: string;
  /** true: use login-screen-specific key for 401 */
  loginContext?: boolean;
  /** technicalConsole label (English constant) */
  logContext: string;
  /** true: suppress duplicate logs when used in render (raw text still not extracted here) */
  skipLog?: boolean;
};

function tryCodeBasedUserMessage(t: TranslateFn, normalized: NormalizedApiError): string | undefined {
  const i18nKey = getRegisteredMessageKeyForApiErrorCode(normalized.code);
  if (!i18nKey) return undefined;
  const translated = t(i18nKey);
  if (translated === USER_FACING_MISSING_TRANSLATION_LABEL) return undefined;
  return translated;
}

/**
 * Logs the error in English technical context; returns a safe short UI string via `t(...)`.
 */
export function getUserFacingApiErrorMessage(
  t: TranslateFn,
  error: unknown,
  options: UserFacingApiErrorOptions,
): string {
  const normalized = normalizeApiError(error);
  if (!options.skipLog) {
    technicalConsole.error(`[API Error] ${options.logContext}`, buildTechnicalApiErrorPayload(normalized));
  }

  const byCode = tryCodeBasedUserMessage(t, normalized);
  if (byCode) return byCode;

  if (options.loginContext && normalized.httpStatus === 401) {
    return t('common.auth.loginInvalidCredentials');
  }
  if (normalized.httpStatus === 400) return t('common.errors.http400');
  if (normalized.httpStatus === 401) return t('common.errors.http401');
  if (normalized.httpStatus === 403) return t('common.errors.http403');
  if (normalized.httpStatus === 404) return t('common.errors.http404');
  if (normalized.httpStatus === 409) return t('common.errors.http409');
  if (normalized.httpStatus === 422) return t('common.errors.http422');
  if (normalized.httpStatus === 429) return t('common.errors.http429');
  if (normalized.httpStatus === 500) return t('common.errors.http500');
  if (normalized.httpStatus === 503) return t('common.errors.http503');

  const msg =
    typeof (error as Error)?.message === 'string' ? (error as Error).message.toLowerCase() : '';
  if (
    normalized.httpStatus === undefined &&
    (msg.includes('network') || msg.includes('fetch') || msg.includes('load failed'))
  ) {
    return t('common.errors.network');
  }

  if (options.fallbackKey) return t(options.fallbackKey);
  return t('common.messages.unknownError');
}
