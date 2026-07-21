/**
 * Maps backend API error codes to localized user-facing messages.
 * Technical details stay in technicalConsole / monitoring — never in the returned string.
 *
 * Prefer this (or `getUserFacingApiErrorMessage`, which delegates here) over raw `error.message`.
 */
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import {
  getRegisteredMessageKeyForApiErrorCode,
  registerApiErrorCodeTranslation,
} from '@/shared/errors/apiErrorCodeRegistry';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';
import { buildTechnicalApiErrorPayload } from '@/shared/errors/technicalApiErrorLog';

export type TranslateFn = (key: string, options?: Record<string, string | number>) => string;
/** Default i18n keys for frequent backend `code` values (auth, users, tenants, cash register). */
export const DEFAULT_API_ERROR_CODE_I18N_KEYS: Readonly<Record<string, string>> = {
  INVALID_CREDENTIALS: 'common.apiErrors.INVALID_CREDENTIALS',
  DUPLICATE_EMAIL: 'common.apiErrors.DUPLICATE_EMAIL',
  USERNAME_CONFLICT: 'common.apiErrors.USERNAME_CONFLICT',
  TENANT_NOT_FOUND: 'common.apiErrors.TENANT_NOT_FOUND',
  TENANT_CONTEXT_REQUIRED: 'common.apiErrors.TENANT_CONTEXT_REQUIRED',
  CASH_REGISTER_CLOSED: 'common.apiErrors.CASH_REGISTER_CLOSED',
  VALIDATION_ERROR: 'common.apiErrors.VALIDATION_ERROR',
  BUSINESS_RULE: 'common.apiErrors.BUSINESS_RULE',
  ROLE_REQUIRED: 'common.apiErrors.ROLE_REQUIRED',
  ROLE_NOT_FOUND: 'common.apiErrors.ROLE_NOT_FOUND',
  ROLE_ASSIGN_FAILED: 'common.apiErrors.ROLE_ASSIGN_FAILED',
  ROLE_ALREADY_EXISTS: 'users.createRole.errors.alreadyExists',
  ROLE_NAME_RESERVED: 'users.createRole.errors.reservedName',
  INHERIT_ROLE_NOT_FOUND: 'users.createRole.errors.inheritNotFound',
  INHERIT_SUPERADMIN_FORBIDDEN: 'users.createRole.errors.inheritSuperAdminForbidden',
  ROLE_CREATE_FAILED: 'users.createRole.errors.generic',
  ROLE_HAS_ASSIGNED_USERS: 'common.apiErrors.ROLE_HAS_ASSIGNED_USERS',
  SYSTEM_ROLE_NOT_EDITABLE: 'common.apiErrors.SYSTEM_ROLE_NOT_EDITABLE',
  SYSTEM_ROLE_NOT_DELETABLE: 'common.apiErrors.SYSTEM_ROLE_NOT_DELETABLE',
  PASSWORD_RESET_FAILED: 'common.apiErrors.PASSWORD_RESET_FAILED',
  USER_CREATE_TRANSACTION_FAILED: 'common.apiErrors.USER_CREATE_TRANSACTION_FAILED',
  UNAUTHORIZED: 'common.errors.http401',
  FORBIDDEN: 'common.errors.http403',
  SLUG_CONFLICT: 'common.apiErrors.SLUG_CONFLICT',
  TENANT_SLUG_TAKEN: 'common.apiErrors.SLUG_CONFLICT',
  SLUG_TAKEN: 'common.apiErrors.SLUG_CONFLICT',
};

let defaultsRegistered = false;

/** Idempotent: registers built-in code → i18n mappings (safe to call from many modules). */
export function ensureDefaultApiErrorTranslations(): void {
  if (defaultsRegistered) return;
  for (const [code, messageKey] of Object.entries(DEFAULT_API_ERROR_CODE_I18N_KEYS)) {
    registerApiErrorCodeTranslation(code, messageKey);
  }
  defaultsRegistered = true;
}

/** Vitest: allow re-registering after `clearApiErrorCodeRegistryForTests`. */
export function resetDefaultApiErrorTranslationsFlagForTests(): void {
  defaultsRegistered = false;
}

/**
 * True when a server string looks like a stack / framework dump — must not be shown in UI.
 */
export function isUnsafeTechnicalErrorDetail(message: string | undefined | null): boolean {
  if (message == null) return true;
  const m = message.trim();
  if (!m) return true;
  if (m.length > 280) return true;
  if (/Exception\b|StackTrace|stack trace|InnerException/i.test(m)) return true;
  if (/System\.|Microsoft\.(AspNetCore|EntityFramework)|Npgsql\.|at [A-Za-z0-9_$.]+\(/i.test(m))
    return true;
  if (/^\s*at\s+/m.test(m)) return true;
  return false;
}

function tryTranslateRegisteredCode(t: TranslateFn, code: string | undefined): string | undefined {
  const i18nKey = getRegisteredMessageKeyForApiErrorCode(code);
  if (!i18nKey) return undefined;
  const translated = t(i18nKey);
  if (translated === USER_FACING_MISSING_TRANSLATION_LABEL) return undefined;
  return translated;
}

function safeOptionalRawMessage(raw: string | undefined): string | undefined {
  if (!raw || isUnsafeTechnicalErrorDetail(raw)) return undefined;
  return raw;
}

export type TranslateApiErrorOptions = {
  /** Default for unknown / unmatched cases (i18n key). */
  fallbackKey?: string;
  /** Login screen: allow backend Accept-Language text on 401 when no code mapping. */
  loginContext?: boolean;
  /** English technicalConsole label */
  logContext?: string;
  /** Skip technicalConsole when caller already logged */
  skipLog?: boolean;
};

/**
 * Returns a short, localized message suitable for toasts / Alerts.
 * Never returns stack traces or unsafe technical dumps.
 */
export function translateApiError(
  t: TranslateFn,
  error: unknown,
  options: TranslateApiErrorOptions = {}
): string {
  ensureDefaultApiErrorTranslations();
  const normalized = normalizeApiError(error);

  if (!options.skipLog) {
    const ctx = options.logContext ?? 'translateApiError';
    technicalConsole.error(`[API Error] ${ctx}`, buildTechnicalApiErrorPayload(normalized));
  }

  const byCode = tryTranslateRegisteredCode(t, normalized.code);
  if (byCode) return byCode;

  // Login: backend may already localize via Accept-Language (safe short text only).
  if (options.loginContext && normalized.httpStatus === 401) {
    const safe = safeOptionalRawMessage(normalized.rawMessage);
    if (safe) return safe;
    return t('common.auth.invalidCredentials');
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

  const transportMsg =
    typeof (error as Error)?.message === 'string' ? (error as Error).message.toLowerCase() : '';
  if (
    normalized.httpStatus === undefined &&
    (transportMsg.includes('network') ||
      transportMsg.includes('fetch') ||
      transportMsg.includes('load failed'))
  ) {
    return t('common.errors.network');
  }

  if (options.fallbackKey) return t(options.fallbackKey);
  return t('common.messages.unknownError');
}
