import i18n from '../../i18n';

export type AuthErrorCode =
  | 'POS_UNAUTHORIZED_USER'
  | 'INVALID_CREDENTIALS'
  | 'LICENSE_ACCESS_DENIED'
  | 'NETWORK_ERROR'
  | 'SESSION_EXPIRED'
  | 'UNKNOWN_AUTH_ERROR';

export class AuthAppError extends Error {
  readonly code: AuthErrorCode;
  readonly status?: number;
  /** Backend'den gelen mesaj (örn. "Geçersiz şifre"); UI'da aynen gösterilir. */
  readonly backendMessage?: string;

  constructor(code: AuthErrorCode, status?: number, backendMessage?: string) {
    super(backendMessage ?? code);
    this.code = code;
    this.status = status;
    this.backendMessage = backendMessage;
    this.name = 'AuthAppError';
  }
}

export function isAuthError(error: unknown): error is AuthAppError {
  if (error instanceof AuthAppError) return true;
  if (!error || typeof error !== 'object') return false;
  const candidate = error as Record<string, unknown>;
  return candidate.name === 'AuthAppError' && typeof candidate.code === 'string';
}

// --- Normalization ---

function extractStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined;
  const e = error as Record<string, unknown>;
  const response = e.response as Record<string, unknown> | undefined;
  if (response?.status != null) return response.status as number;
  if (typeof e.status === 'number') return e.status;
  return undefined;
}

/** Axios veya config interceptor'dan gelen hatadan backend message çıkarır. */
function extractBackendMessage(error: unknown): string | undefined {
  if (!error || typeof error !== 'object') return undefined;
  const e = error as Record<string, unknown>;
  const data = (e.response as Record<string, unknown> | undefined)?.data ?? e.data;
  const msg = (data as Record<string, unknown> | undefined)?.message;
  return typeof msg === 'string' ? msg : undefined;
}

export function normalizeLoginError(error: unknown): AuthAppError {
  if (isAuthError(error)) return error;

  const status = extractStatus(error);
  const backendMessage = extractBackendMessage(error);

  switch (status) {
    case 400:
    case 401:
      return new AuthAppError('INVALID_CREDENTIALS', status ?? 401, backendMessage);
    case 403:
      return new AuthAppError('POS_UNAUTHORIZED_USER', 403, backendMessage);
    default:
      if (status == null) return new AuthAppError('NETWORK_ERROR', undefined, backendMessage);
      return new AuthAppError('UNKNOWN_AUTH_ERROR', status, backendMessage);
  }
}

// --- Message lookup (i18n-backed) ---

const AUTH_ERROR_I18N_KEYS: Record<AuthErrorCode, string> = {
  POS_UNAUTHORIZED_USER: 'auth:errors.posUnauthorized',
  INVALID_CREDENTIALS: 'auth:errors.invalidCredentials',
  LICENSE_ACCESS_DENIED: 'auth:errors.licenseAccessDenied',
  NETWORK_ERROR: 'auth:errors.networkError',
  SESSION_EXPIRED: 'auth:errors.sessionExpired',
  UNKNOWN_AUTH_ERROR: 'auth:errors.unknownError',
};

/** Normalizes any login failure into a typed AuthAppError. */
export function toAuthError(error: unknown): AuthAppError {
  if (error instanceof AuthAppError) return error;
  if (isAuthError(error)) {
    return new AuthAppError(error.code, error.status, error.backendMessage);
  }
  return normalizeLoginError(error);
}

function mapInvalidCredentialsMessage(backendMessage?: string): string {
  if (!backendMessage) {
    return i18n.t('auth:errors.invalidCredentials');
  }

  const lower = backendMessage.toLowerCase();
  if (
    lower.includes('şifre') ||
    lower.includes('password') ||
    lower.includes('passwort')
  ) {
    return i18n.t('auth:errors.wrongPassword');
  }
  if (
    lower.includes('bulunamadı') ||
    lower.includes('not found') ||
    lower.includes('benutzer nicht')
  ) {
    return i18n.t('auth:errors.userNotFound');
  }
  if (
    lower.includes('aktif değil') ||
    lower.includes('gesperrt') ||
    lower.includes('inactive') ||
    lower.includes('locked') ||
    lower.includes('deaktiviert')
  ) {
    return i18n.t('auth:errors.accountInactive');
  }

  return i18n.t('auth:errors.invalidCredentials');
}

/** POS login UI: always returns a short German user message (never stack traces). */
export function resolveLoginErrorMessage(error: unknown): string {
  const authError = toAuthError(error);

  switch (authError.code) {
    case 'INVALID_CREDENTIALS':
      return mapInvalidCredentialsMessage(authError.backendMessage);
    case 'POS_UNAUTHORIZED_USER':
      return i18n.t('auth:errors.posUnauthorized');
    case 'LICENSE_ACCESS_DENIED':
      return i18n.t('auth:errors.licenseAccessDenied');
    case 'NETWORK_ERROR':
      return i18n.t('auth:errors.networkError');
    case 'SESSION_EXPIRED':
      return i18n.t('auth:errors.sessionExpired');
    case 'UNKNOWN_AUTH_ERROR':
      if (authError.status === 500) {
        return i18n.t('auth:errors.serverError');
      }
      return i18n.t('auth:errors.unknownError');
    default:
      return i18n.t('auth:errors.unknownError');
  }
}

/** AuthAppError ise önce backendMessage döner; yoksa i18n. Code verilirse sadece i18n. */
export function getAuthErrorMessage(errorOrCode: AuthAppError | AuthErrorCode): string {
  if (errorOrCode instanceof AuthAppError) {
    if (errorOrCode.backendMessage) return errorOrCode.backendMessage;
    return i18n.t(AUTH_ERROR_I18N_KEYS[errorOrCode.code]);
  }
  return i18n.t(AUTH_ERROR_I18N_KEYS[errorOrCode]);
}
