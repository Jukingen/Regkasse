import {
  type AuthErrorCode,
  resolveLoginErrorMessage,
  toAuthError,
} from '../features/auth/authErrors';
import i18n from '../i18n';

export interface LoginError {
  userMessage: string;
  technicalMessage?: string;
  /** Used by login UI for field highlighting and password reset. */
  errorCode: AuthErrorCode;
}

function extractHttpStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined;
  const candidate = error as Record<string, unknown>;
  const response = candidate.response as Record<string, unknown> | undefined;
  if (typeof response?.status === 'number') return response.status;
  if (typeof candidate.status === 'number') return candidate.status;
  return undefined;
}

function extractBackendMessage(error: unknown): string {
  if (!error || typeof error !== 'object') return '';
  const candidate = error as Record<string, unknown>;
  const data = (candidate.response as Record<string, unknown> | undefined)?.data ?? candidate.data;
  if (!data || typeof data !== 'object') return '';

  const body = data as Record<string, unknown>;
  for (const key of ['message', 'error', 'title'] as const) {
    const value = body[key];
    if (typeof value === 'string' && value.trim()) return value;
  }
  return '';
}

function sanitizeTechnicalMessage(
  backendMessage: string | undefined,
  error: unknown
): string | undefined {
  if (backendMessage?.trim()) return backendMessage;

  if (!error || typeof error !== 'object') return undefined;
  const message = (error as Record<string, unknown>).message;
  if (typeof message !== 'string' || !message.trim()) return undefined;

  // Never expose stack traces or AuthAppError wrappers to UI consumers.
  if (message.includes('\n    at ') || message.startsWith('AuthAppError:')) {
    return undefined;
  }
  return message;
}

function includesAny(haystack: string, needles: readonly string[]): boolean {
  const lower = haystack.toLowerCase();
  return needles.some((needle) => lower.includes(needle.toLowerCase()));
}

export const handleLoginError = (error: unknown): LoginError => {
  const authError = toAuthError(error);
  const status = authError.status ?? extractHttpStatus(error);
  const errorMessage = authError.backendMessage || extractBackendMessage(error);
  const technicalMessage = sanitizeTechnicalMessage(errorMessage, error);

  if (authError.code === 'NETWORK_ERROR') {
    return {
      userMessage: i18n.t('auth:errors.connectionFailed'),
      technicalMessage,
      errorCode: 'NETWORK_ERROR',
    };
  }

  if (status === 429) {
    return {
      userMessage: i18n.t('auth:errors.tooManyAttempts'),
      technicalMessage,
      errorCode: 'UNKNOWN_AUTH_ERROR',
    };
  }

  if (status === 400 || status === 401) {
    if (
      includesAny(errorMessage, [
        'nicht autorisiert',
        'not authorized',
        'yetkili',
        'not authorized for',
      ])
    ) {
      return {
        userMessage: i18n.t('auth:errors.posUnauthorized'),
        technicalMessage,
        errorCode: 'POS_UNAUTHORIZED_USER',
      };
    }
    if (includesAny(errorMessage, ['passwort', 'password', 'şifre'])) {
      return {
        userMessage: i18n.t('auth:errors.wrongPassword'),
        technicalMessage,
        errorCode: 'INVALID_CREDENTIALS',
      };
    }
    if (includesAny(errorMessage, ['benutzer', 'user', 'bulunamadı', 'not found'])) {
      return {
        userMessage: i18n.t('auth:errors.userNotFound'),
        technicalMessage,
        errorCode: 'INVALID_CREDENTIALS',
      };
    }

    return {
      userMessage: resolveLoginErrorMessage(authError) || i18n.t('auth:errors.credentialsCheck'),
      technicalMessage,
      errorCode: authError.code,
    };
  }

  if (status === 403) {
    if (authError.code === 'LICENSE_ACCESS_DENIED') {
      return {
        userMessage: i18n.t('auth:errors.licenseAccessDenied'),
        technicalMessage,
        errorCode: authError.code,
      };
    }
    if (authError.code === 'POS_UNAUTHORIZED_USER') {
      return {
        userMessage: i18n.t('auth:errors.posUnauthorized'),
        technicalMessage,
        errorCode: authError.code,
      };
    }

    return {
      userMessage: i18n.t('auth:errors.forbiddenOrLicense'),
      technicalMessage,
      errorCode: authError.code,
    };
  }

  if (status != null && status >= 500) {
    return {
      userMessage: i18n.t('auth:errors.serverError'),
      technicalMessage,
      errorCode: authError.code,
    };
  }

  return {
    userMessage: resolveLoginErrorMessage(authError),
    technicalMessage,
    errorCode: authError.code,
  };
};

/** Error re-thrown from AuthContext after user-friendly normalization. */
export type LoginFailedError = Error & { errorCode: AuthErrorCode };

export function createLoginFailedError(result: LoginError): LoginFailedError {
  const error = new Error(result.userMessage) as LoginFailedError;
  error.errorCode = result.errorCode;
  return error;
}

/** Resolves login failures from AuthContext re-throws or raw API/auth errors. */
export function getLoginFailure(err: unknown): LoginError {
  if (err instanceof Error && typeof (err as LoginFailedError).errorCode === 'string') {
    return {
      userMessage: err.message,
      errorCode: (err as LoginFailedError).errorCode,
    };
  }
  return handleLoginError(err);
}
