/**
 * Classifies transport-level API failures (rate limit / server / network)
 * and surfaces localized Ant Design notifications via the App bridge.
 *
 * Used from the axios response interceptor (non-React). React components
 * should prefer {@link useNotify} / {@link translateApiError} for domain errors.
 */
import type { AxiosError } from 'axios';

import {
  DEFAULT_TEXT_LOCALE,
  type TextLocale,
  getCatalog,
  normalizeTextLocale,
} from '@/i18n/config';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { isAbortCancellation } from '@/lib/axiosRetry';
import { notifyError } from '@/lib/notificationService';
import { technicalConsole } from '@/shared/dev/technicalConsole';

export const ApiTransportErrorKind = {
  RATE_LIMIT_EXCEEDED: 'RATE_LIMIT_EXCEEDED',
  SERVER_ERROR: 'SERVER_ERROR',
  NETWORK_ERROR: 'NETWORK_ERROR',
} as const;

export type ApiTransportErrorKind =
  (typeof ApiTransportErrorKind)[keyof typeof ApiTransportErrorKind];

const I18N_KEYS: Record<ApiTransportErrorKind, string> = {
  [ApiTransportErrorKind.RATE_LIMIT_EXCEEDED]: 'common.errors.http429',
  [ApiTransportErrorKind.SERVER_ERROR]: 'common.errors.http500',
  [ApiTransportErrorKind.NETWORK_ERROR]: 'common.errors.network',
};

/** Fallback when catalog lookup fails (DE, matching product default). */
const FALLBACK_MESSAGES: Record<ApiTransportErrorKind, string> = {
  [ApiTransportErrorKind.RATE_LIMIT_EXCEEDED]: 'Zu viele Anfragen. Bitte warten Sie einen Moment.',
  [ApiTransportErrorKind.SERVER_ERROR]:
    'Ein Serverfehler ist aufgetreten. Bitte versuchen Sie es später erneut.',
  [ApiTransportErrorKind.NETWORK_ERROR]:
    'Netzwerkverbindung fehlgeschlagen. Bitte prüfen Sie Ihre Internetverbindung.',
};

const NOTIFICATION_KEYS: Record<ApiTransportErrorKind, string> = {
  [ApiTransportErrorKind.RATE_LIMIT_EXCEEDED]: 'api-transport-rate-limit',
  [ApiTransportErrorKind.SERVER_ERROR]: 'api-transport-server-error',
  [ApiTransportErrorKind.NETWORK_ERROR]: 'api-transport-network-error',
};

type CommonErrorsBag = {
  http429?: string;
  http500?: string;
  http503?: string;
  network?: string;
};

function readCommonErrors(locale: TextLocale): CommonErrorsBag {
  const common = getCatalog(locale).common as { errors?: CommonErrorsBag } | undefined;
  return common?.errors && typeof common.errors === 'object' ? common.errors : {};
}

/**
 * Maps an axios (or axios-like) failure to a transport error kind.
 * Returns null for aborts and non-transport HTTP statuses (e.g. 4xx except 429).
 */
export function classifyApiTransportError(error: unknown): ApiTransportErrorKind | null {
  if (isAbortCancellation(error)) {
    return null;
  }

  const axiosError = error as AxiosError;
  const status = axiosError.response?.status;

  if (status == null) {
    // No HTTP response: network / DNS / connection / timeout.
    return ApiTransportErrorKind.NETWORK_ERROR;
  }

  if (status === 429) {
    return ApiTransportErrorKind.RATE_LIMIT_EXCEEDED;
  }

  if (status >= 500 && status < 600) {
    return ApiTransportErrorKind.SERVER_ERROR;
  }

  return null;
}

export function getApiTransportErrorI18nKey(kind: ApiTransportErrorKind): string {
  return I18N_KEYS[kind];
}

/**
 * Resolves a localized user-facing message for a transport error kind.
 * Reads from i18n catalogs synchronously (safe for axios interceptors).
 */
export function resolveApiTransportErrorMessage(
  kind: ApiTransportErrorKind,
  localeInput?: string | null
): string {
  const locale = normalizeTextLocale(localeInput ?? getStoredLanguage() ?? DEFAULT_TEXT_LOCALE);
  const errors = readCommonErrors(locale);

  if (kind === ApiTransportErrorKind.RATE_LIMIT_EXCEEDED && typeof errors.http429 === 'string') {
    return errors.http429;
  }
  if (kind === ApiTransportErrorKind.SERVER_ERROR) {
    // Prefer 503-specific copy when the response was 503; callers without status use http500.
    if (typeof errors.http500 === 'string') {
      return errors.http500;
    }
  }
  if (kind === ApiTransportErrorKind.NETWORK_ERROR && typeof errors.network === 'string') {
    return errors.network;
  }

  return FALLBACK_MESSAGES[kind];
}

export function resolveApiTransportErrorMessageForStatus(
  kind: ApiTransportErrorKind,
  status: number | undefined,
  localeInput?: string | null
): string {
  if (kind === ApiTransportErrorKind.SERVER_ERROR && status === 503) {
    const locale = normalizeTextLocale(localeInput ?? getStoredLanguage() ?? DEFAULT_TEXT_LOCALE);
    const errors = readCommonErrors(locale);
    if (typeof errors.http503 === 'string') {
      return errors.http503;
    }
  }
  return resolveApiTransportErrorMessage(kind, localeInput);
}

/** Development-only diagnostic log for transport failures. */
export function logApiTransportErrorDev(
  kind: ApiTransportErrorKind,
  error: unknown,
  meta?: { url?: string; method?: string; status?: number }
): void {
  if (process.env.NODE_ENV === 'production') {
    return;
  }
  const axiosError = error as AxiosError;
  technicalConsole.devLog(`[API] Transport error: ${kind}`, {
    method: meta?.method ?? axiosError.config?.method?.toUpperCase(),
    url: meta?.url ?? axiosError.config?.url,
    status: meta?.status ?? axiosError.response?.status ?? 'network',
    code: axiosError.code,
  });
}

export type NotifyApiTransportErrorOptions = {
  /** When true, skip the Ant Design notification (still classifies + may log). */
  silent?: boolean;
  locale?: string | null;
  url?: string;
  method?: string;
};

/**
 * Classifies the error, logs in development, and shows a corner notification
 * via `useAntdApp().notification` (registered through AntdAppBridgeRegistrar).
 *
 * @returns The classified kind, or null when not a transport error.
 */
export function notifyApiTransportError(
  error: unknown,
  options: NotifyApiTransportErrorOptions = {}
): ApiTransportErrorKind | null {
  const kind = classifyApiTransportError(error);
  if (!kind) {
    return null;
  }

  const axiosError = error as AxiosError;
  const status = axiosError.response?.status;
  logApiTransportErrorDev(kind, error, {
    url: options.url,
    method: options.method,
    status,
  });

  if (options.silent) {
    return kind;
  }

  if (typeof window === 'undefined') {
    return kind;
  }

  const message = resolveApiTransportErrorMessageForStatus(kind, status, options.locale);
  notifyError(message, {
    mode: 'notification',
    key: NOTIFICATION_KEYS[kind],
    duration: kind === ApiTransportErrorKind.RATE_LIMIT_EXCEEDED ? 6 : 8,
  });

  return kind;
}
