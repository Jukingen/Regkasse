/**
 * Tagesabschluss API error codes → i18n. Call {@link ensureTagesabschlussApiErrorTranslations}
 * before resolving user-facing closing errors.
 */
import { ensureDefaultApiErrorTranslations } from '@/lib/api/errorTranslator';
import { registerApiErrorCodeTranslation } from '@/shared/errors/apiErrorCodeRegistry';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';
import {
  getUserFacingApiErrorMessage,
  type TranslateFn,
  type UserFacingApiErrorOptions,
} from '@/shared/errors/userFacingApiError';

export const TAGESABSCHLUSS_ERROR_CODE_I18N_KEYS: Readonly<Record<string, string>> = {
  ALREADY_CLOSED_TODAY: 'tagesabschluss.errors.alreadyClosedToday',
  ALREADY_CLOSED_MONTH: 'tagesabschluss.errors.alreadyClosedMonth',
  ALREADY_CLOSED_YEAR: 'tagesabschluss.errors.alreadyClosedYear',
  BACKDATED_REASON_REQUIRED: 'tagesabschluss.errors.backdatedReasonRequired',
  FUTURE_CLOSING_DATE: 'tagesabschluss.errors.futureDate',
  PAYMENTS_WITHOUT_INVOICE: 'tagesabschluss.errors.paymentsWithoutInvoice',
  CASH_REGISTER_UNAVAILABLE: 'tagesabschluss.errors.registerUnavailable',
  TSE_NOT_CONNECTED: 'tagesabschluss.errors.tseNotConnected',
  TENANT_CONTEXT_REQUIRED: 'tagesabschluss.errors.tenantContextRequired',
  TAGESABSCHLUSS_NO_REGISTER: 'tagesabschluss.errors.noRegister',
};

let registered = false;

/** Idempotent registration for Tagesabschluss machine codes. */
export function ensureTagesabschlussApiErrorTranslations(): void {
  if (registered) return;
  ensureDefaultApiErrorTranslations();
  for (const [code, messageKey] of Object.entries(TAGESABSCHLUSS_ERROR_CODE_I18N_KEYS)) {
    registerApiErrorCodeTranslation(code, messageKey);
  }
  registered = true;
}

/** Vitest isolation. */
export function resetTagesabschlussApiErrorTranslationsFlagForTests(): void {
  registered = false;
}

/** Infer code from English backend `error` text when `code`/`details` are absent (older responses). */
export function inferTagesabschlussErrorCode(rawMessage: string | undefined): string | undefined {
  if (!rawMessage?.trim()) return undefined;
  const msg = rawMessage.trim();
  if (/already performed for the current month/i.test(msg)) return 'ALREADY_CLOSED_MONTH';
  if (/already performed for the current year/i.test(msg)) return 'ALREADY_CLOSED_YEAR';
  if (/already performed for today/i.test(msg) || /already performed for \d{4}-\d{2}-\d{2}/i.test(msg)) {
    return 'ALREADY_CLOSED_TODAY';
  }
  if (/reason is required for backdated/i.test(msg)) return 'BACKDATED_REASON_REQUIRED';
  if (/future date/i.test(msg)) return 'FUTURE_CLOSING_DATE';
  if (/payment\(s\) without/i.test(msg)) return 'PAYMENTS_WITHOUT_INVOICE';
  if (/not available for/i.test(msg)) return 'CASH_REGISTER_UNAVAILABLE';
  if (/TSE/i.test(msg) && /not connected/i.test(msg)) return 'TSE_NOT_CONNECTED';
  if (/Tenant context required/i.test(msg)) return 'TENANT_CONTEXT_REQUIRED';
  if (/No cash register found/i.test(msg)) return 'TAGESABSCHLUSS_NO_REGISTER';
  return undefined;
}

/**
 * Localized toast/Alert text for Tagesabschluss failures (registers codes, infers from English message).
 */
export function getTagesabschlussUserFacingError(
  t: TranslateFn,
  error: unknown,
  options: Omit<UserFacingApiErrorOptions, 'fallbackKey'> & { fallbackKey?: string }
): string {
  ensureTagesabschlussApiErrorTranslations();
  const normalized = normalizeApiError(error);
  const inferred = !normalized.code ? inferTagesabschlussErrorCode(normalized.rawMessage) : undefined;
  const errorForMessage =
    inferred != null
      ? {
          response: {
            status: normalized.httpStatus,
            data: {
              code: inferred,
              error: normalized.rawMessage,
              message: normalized.rawMessage,
            },
          },
        }
      : error;

  return getUserFacingApiErrorMessage(t, errorForMessage, {
    ...options,
    fallbackKey: options.fallbackKey ?? 'tagesabschluss.errors.unknown',
  });
}
