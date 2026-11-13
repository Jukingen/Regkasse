import { describe, it, expect, jest } from '@jest/globals';

import { AuthAppError } from '../features/auth/authErrors';
import {
  createLoginFailedError,
  getLoginFailure,
  handleLoginError,
} from '../utils/loginErrorHandler';

jest.mock('../i18n', () => ({
  __esModule: true,
  default: {
    t: (key: string) => {
      const messages: Record<string, string> = {
        'auth:errors.posUnauthorized':
          'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
        'auth:errors.wrongPassword': 'Falsches Passwort. Bitte versuchen Sie es erneut.',
        'auth:errors.userNotFound': 'Benutzername oder E-Mail nicht gefunden.',
        'auth:errors.connectionFailed':
          'Verbindung zum Server fehlgeschlagen. Bitte überprüfen Sie Ihre Internetverbindung.',
        'auth:errors.tooManyAttempts':
          'Zu viele fehlgeschlagene Login-Versuche. Bitte warten Sie eine Minute.',
        'auth:errors.serverError': 'Serverfehler. Bitte versuchen Sie es später erneut.',
      };
      return messages[key] ?? key;
    },
  },
}));

describe('handleLoginError', () => {
  it('returns German POS unauthorized message for 403 role denial', () => {
    const result = handleLoginError(
      new AuthAppError('POS_UNAUTHORIZED_USER', 403, 'Bu kullanıcı bu uygulama için yetkili değil.')
    );

    expect(result.userMessage).toBe(
      'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.'
    );
    expect(result.technicalMessage).toBe('Bu kullanıcı bu uygulama için yetkili değil.');
    expect(result.userMessage).not.toContain('at ');
    expect(result.errorCode).toBe('POS_UNAUTHORIZED_USER');
  });

  it('maps wrong-password backend payload to user-friendly German copy', () => {
    const result = handleLoginError({
      status: 400,
      data: { message: 'Geçersiz şifre' },
    });

    expect(result.userMessage).toBe('Falsches Passwort. Bitte versuchen Sie es erneut.');
    expect(result.errorCode).toBe('INVALID_CREDENTIALS');
  });

  it('maps network failures to connection message without stack traces', () => {
    const result = handleLoginError(new Error('Network Error\n    at normalizeLoginError'));

    expect(result.userMessage).toBe(
      'Verbindung zum Server fehlgeschlagen. Bitte überprüfen Sie Ihre Internetverbindung.'
    );
    expect(result.userMessage).not.toContain('normalizeLoginError');
    expect(result.errorCode).toBe('NETWORK_ERROR');
  });

  it('maps rate limiting to a dedicated user message', () => {
    const result = handleLoginError({
      response: { status: 429, data: { message: 'Too many requests' } },
    });

    expect(result.userMessage).toBe(
      'Zu viele fehlgeschlagene Login-Versuche. Bitte warten Sie eine Minute.'
    );
    expect(result.technicalMessage).toBe('Too many requests');
  });

  it('maps server errors to German server message', () => {
    const result = handleLoginError({
      status: 500,
      data: { message: 'Internal server error' },
    });

    expect(result.userMessage).toBe('Serverfehler. Bitte versuchen Sie es später erneut.');
    expect(result.technicalMessage).toBe('Internal server error');
  });

  it('re-throws user-friendly errors from AuthContext without double normalization', () => {
    const thrown = createLoginFailedError({
      userMessage:
        'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
      errorCode: 'POS_UNAUTHORIZED_USER',
    });

    const resolved = getLoginFailure(thrown);

    expect(resolved.userMessage).toBe(thrown.message);
    expect(resolved.errorCode).toBe('POS_UNAUTHORIZED_USER');
    expect(resolved.userMessage).not.toContain('at ');
  });
});
