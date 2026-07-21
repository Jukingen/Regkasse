import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  ensureDefaultApiErrorTranslations,
  isUnsafeTechnicalErrorDetail,
  resetDefaultApiErrorTranslationsFlagForTests,
  translateApiError,
} from '@/lib/api/errorTranslator';
import {
  clearApiErrorCodeRegistryForTests,
  registerApiErrorCodeTranslation,
} from '@/shared/errors/apiErrorCodeRegistry';

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn() },
}));

const t = (key: string) => key;

describe('translateApiError', () => {
  afterEach(() => {
    clearApiErrorCodeRegistryForTests();
    resetDefaultApiErrorTranslationsFlagForTests();
  });

  it('maps INVALID_CREDENTIALS to i18n key', () => {
    expect(
      translateApiError(
        t,
        { response: { status: 401, data: { code: 'INVALID_CREDENTIALS', message: 'nope' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.INVALID_CREDENTIALS');
  });

  it('maps DUPLICATE_EMAIL, TENANT_NOT_FOUND, CASH_REGISTER_CLOSED', () => {
    expect(
      translateApiError(
        t,
        { response: { status: 409, data: { code: 'DUPLICATE_EMAIL' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.DUPLICATE_EMAIL');
    expect(
      translateApiError(
        t,
        { response: { status: 404, data: { code: 'TENANT_NOT_FOUND' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.TENANT_NOT_FOUND');
    expect(
      translateApiError(
        t,
        { response: { status: 409, data: { code: 'CASH_REGISTER_CLOSED' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.CASH_REGISTER_CLOSED');
  });

  it('maps USERNAME_CONFLICT and SLUG_CONFLICT', () => {
    expect(
      translateApiError(
        t,
        { response: { status: 409, data: { code: 'USERNAME_CONFLICT' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.USERNAME_CONFLICT');
    expect(
      translateApiError(
        t,
        { response: { status: 409, data: { code: 'SLUG_CONFLICT' } } },
        { skipLog: true }
      )
    ).toBe('common.apiErrors.SLUG_CONFLICT');
  });

  it('falls back to HTTP status keys when code is unknown', () => {
    expect(
      translateApiError(
        t,
        { response: { status: 403, data: { code: 'SOME_NEW_CODE' } } },
        { skipLog: true }
      )
    ).toBe('common.errors.http403');
  });

  it('uses unknownError for unmatched errors', () => {
    expect(translateApiError(t, new Error('weird'), { skipLog: true })).toBe(
      'common.messages.unknownError'
    );
  });

  it('uses fallbackKey when provided and status unknown', () => {
    expect(
      translateApiError(t, new Error('weird'), {
        skipLog: true,
        fallbackKey: 'users.messages.errorGeneric',
      })
    ).toBe('users.messages.errorGeneric');
  });

  it('loginContext uses invalidCredentials when no safe raw message', () => {
    expect(
      translateApiError(t, { response: { status: 401 } }, { skipLog: true, loginContext: true })
    ).toBe('common.auth.invalidCredentials');
  });

  it('loginContext prefers registered code over raw text', () => {
    expect(
      translateApiError(
        t,
        {
          response: {
            status: 401,
            data: { code: 'INVALID_CREDENTIALS', message: 'Ungültiges Passwort' },
          },
        },
        { skipLog: true, loginContext: true }
      )
    ).toBe('common.apiErrors.INVALID_CREDENTIALS');
  });

  it('loginContext allows short localized raw 401 when no code', () => {
    expect(
      translateApiError(
        t,
        { response: { status: 401, data: { message: 'Ungültiges Passwort' } } },
        { skipLog: true, loginContext: true }
      )
    ).toBe('Ungültiges Passwort');
  });

  it('does not surface stack traces to the user', () => {
    const stack =
      'System.NullReferenceException: Object reference not set\n   at Foo.Bar() in C:\\src\\Foo.cs:line 12';
    expect(
      translateApiError(
        t,
        { response: { status: 500, data: { message: stack } } },
        { skipLog: true }
      )
    ).toBe('common.errors.http500');
  });

  it('respects extra registerApiErrorCodeTranslation overrides after ensure', () => {
    ensureDefaultApiErrorTranslations();
    registerApiErrorCodeTranslation('INVOICE_LOCKED', 'payments.errors.invoiceLocked');
    expect(
      translateApiError(
        t,
        { response: { status: 409, data: { code: 'INVOICE_LOCKED' } } },
        { skipLog: true }
      )
    ).toBe('payments.errors.invoiceLocked');
  });
});

describe('isUnsafeTechnicalErrorDetail', () => {
  it('flags empty, long, and exception-like strings', () => {
    expect(isUnsafeTechnicalErrorDetail(undefined)).toBe(true);
    expect(isUnsafeTechnicalErrorDetail('')).toBe(true);
    expect(isUnsafeTechnicalErrorDetail('x'.repeat(300))).toBe(true);
    expect(isUnsafeTechnicalErrorDetail('NullReferenceException: boom')).toBe(true);
    expect(isUnsafeTechnicalErrorDetail('Ungültiges Passwort')).toBe(false);
  });
});
