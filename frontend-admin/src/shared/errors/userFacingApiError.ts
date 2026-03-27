/**
 * API hataları: kullanıcıya yerelleştirilmiş kısa mesaj; ham metin yalnızca technicalConsole’da (İngilizce etiket).
 */
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { extractHttpStatusFromUnknownError, extractRawApiErrorMessage } from './extractRawApiErrorMessage';

export type TranslateFn = (key: string, options?: Record<string, string | number>) => string;

export type UserFacingApiErrorOptions = {
  /** Varsayılan: bilinmeyen / eşleşmeyen durumlar */
  fallbackKey?: string;
  /** true: 401 için giriş ekranına özel anahtar */
  loginContext?: boolean;
  /** technicalConsole etiketi (İngilizce sabit) */
  logContext: string;
  /** true: render içinde kullanımda tekrarlayan logları kes (ham metin yine extract edilmez) */
  skipLog?: boolean;
};

/**
 * Ham hatayı İngilizce teknik bağlamda loglar; UI için `t(...)` ile güvenli kısa mesaj döner.
 */
export function getUserFacingApiErrorMessage(
  t: TranslateFn,
  error: unknown,
  options: UserFacingApiErrorOptions,
): string {
  const status = extractHttpStatusFromUnknownError(error);
  const raw = extractRawApiErrorMessage(error);
  if (!options.skipLog) {
    technicalConsole.error(`[API Error] ${options.logContext}`, { httpStatus: status, rawMessage: raw ?? null });
  }

  if (options.loginContext && status === 401) {
    return t('common.auth.loginInvalidCredentials');
  }
  if (status === 400) return t('common.errors.http400');
  if (status === 401) return t('common.errors.http401');
  if (status === 403) return t('common.errors.http403');
  if (status === 404) return t('common.errors.http404');
  if (status === 409) return t('common.errors.http409');
  if (status === 422) return t('common.errors.http422');
  if (status === 429) return t('common.errors.http429');
  if (status === 500) return t('common.errors.http500');
  if (status === 503) return t('common.errors.http503');

  const msg = typeof (error as Error)?.message === 'string' ? (error as Error).message.toLowerCase() : '';
  if (status === undefined && (msg.includes('network') || msg.includes('fetch') || msg.includes('load failed'))) {
    return t('common.errors.network');
  }

  if (options.fallbackKey) return t(options.fallbackKey);
  return t('common.messages.unknownError');
}
