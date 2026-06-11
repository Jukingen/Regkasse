import { describe, it, expect, jest } from '@jest/globals';
import {
  AuthAppError,
  resolveLoginErrorMessage,
  toAuthError,
} from '../features/auth/authErrors';

jest.mock('../i18n', () => ({
  __esModule: true,
  default: {
    t: (key: string) => {
      const messages: Record<string, string> = {
        'auth:errors.posUnauthorized':
          'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
        'auth:errors.wrongPassword': 'Falsches Passwort. Bitte versuchen Sie es erneut.',
        'auth:errors.userNotFound': 'Benutzername oder E-Mail nicht gefunden.',
        'auth:errors.invalidCredentials': 'Benutzername oder Passwort ist falsch.',
        'auth:errors.unknownError': 'Anmeldung fehlgeschlagen. Bitte versuchen Sie es erneut.',
      };
      return messages[key] ?? key;
    },
  },
}));

describe('auth login error messages', () => {
  it('maps POS unauthorized to German UI copy (not backend Turkish)', () => {
    const error = new AuthAppError(
      'POS_UNAUTHORIZED_USER',
      403,
      'Bu kullanıcı bu uygulama için yetkili değil.',
    );

    expect(resolveLoginErrorMessage(error)).toBe(
      'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
    );
  });

  it('maps invalid password backend message to German wrong-password copy', () => {
    const error = new AuthAppError('INVALID_CREDENTIALS', 400, 'Geçersiz şifre');

    expect(resolveLoginErrorMessage(error)).toBe(
      'Falsches Passwort. Bitte versuchen Sie es erneut.',
    );
  });

  it('maps user-not-found backend message to German copy', () => {
    const error = new AuthAppError('INVALID_CREDENTIALS', 400, 'Kullanıcı bulunamadı');

    expect(resolveLoginErrorMessage(error)).toBe(
      'Benutzername oder E-Mail nicht gefunden.',
    );
  });

  it('normalizes axios-style 403 payload without exposing stack traces', () => {
    const axiosLike = {
      status: 403,
      data: { message: 'Bu kullanıcı bu uygulama için yetkili değil.' },
      message: 'Request failed with status code 403',
    };

    const authError = toAuthError(axiosLike);
    const message = resolveLoginErrorMessage(authError);

    expect(authError.code).toBe('POS_UNAUTHORIZED_USER');
    expect(message).not.toContain('at ');
    expect(message).not.toContain('normalizeLoginError');
    expect(message).toBe(
      'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
    );
  });
});
