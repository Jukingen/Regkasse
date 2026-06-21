import { describe, expect, it, jest } from '@jest/globals';

jest.mock('../i18n', () => ({
  __esModule: true,
  default: {
    t: (key: string, opts?: { ns?: string }) => {
      const fullKey = opts?.ns ? `${opts.ns}:${key}` : key;
      const messages: Record<string, string> = {
        'auth:validation.loginIdentifierRequired': 'E-Mail oder Benutzername ist erforderlich',
        'auth:validation.loginIdentifierMin': 'Mindestens 3 Zeichen',
        'auth:validation.loginIdentifierMax': 'Maximal 50 Zeichen',
        'auth:validation.loginIdentifierPattern': 'Nur Buchstaben, Zahlen, Unterstrich und Bindestrich erlaubt',
        'auth:validation.passwordRequired': 'Passwort ist erforderlich',
        'auth:validation.passwordMin': 'Mindestens 8 Zeichen',
        'auth:validation.passwordMax': 'Passwort darf maximal 128 Zeichen haben',
        'auth:invalidEmail': 'Ungültige E-Mail-Adresse',
      };
      return messages[fullKey] ?? fullKey;
    },
  },
}));

import {
  validateAmount,
  validateEmail,
  validatePassword,
  validateUsername,
} from '../utils/validation';

describe('login validation helpers', () => {
  it('requires login identifier', () => {
    expect(validateUsername('   ').isValid).toBe(false);
    expect(validateUsername('   ').message).toBe('E-Mail oder Benutzername ist erforderlich');
  });

  it('enforces min length for username', () => {
    expect(validateUsername('ab').isValid).toBe(false);
    expect(validateUsername('ab').message).toBe('Mindestens 3 Zeichen');
  });

  it('accepts email login identifier', () => {
    expect(validateUsername('user@firma.at').isValid).toBe(true);
  });

  it('accepts canonical username', () => {
    expect(validateUsername('manager1').isValid).toBe(true);
  });

  it('rejects invalid username characters', () => {
    expect(validateUsername('bad name').isValid).toBe(false);
    expect(validateUsername('bad name').message).toBe(
      'Nur Buchstaben, Zahlen, Unterstrich und Bindestrich erlaubt',
    );
  });

  it('requires password and enforces min length', () => {
    expect(validatePassword('').isValid).toBe(false);
    expect(validatePassword('').message).toBe('Passwort ist erforderlich');
    expect(validatePassword('short').isValid).toBe(false);
    expect(validatePassword('short').message).toBe('Mindestens 8 Zeichen');
    expect(validatePassword('long-enough').isValid).toBe(true);
  });

  it('validates optional email field', () => {
    expect(validateEmail('').isValid).toBe(true);
    expect(validateEmail('not-an-email').isValid).toBe(false);
    expect(validateEmail('not-an-email').message).toBe('Ungültige E-Mail-Adresse');
  });

  it('keeps validateAmount behavior for payment flows', () => {
    expect(validateAmount(0.01)).toBe(false);
    expect(validateAmount(0.02)).toBe(true);
  });
});
