import { describe, expect, it, vi, afterEach } from 'vitest';
import { clearApiErrorCodeRegistryForTests, registerApiErrorCodeTranslation } from '../apiErrorCodeRegistry';
import { getUserFacingApiErrorMessage } from '../userFacingApiError';

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn() },
}));

const t = (key: string) => key;

describe('getUserFacingApiErrorMessage', () => {
  afterEach(() => {
    clearApiErrorCodeRegistryForTests();
  });

  it('maps 401 in loginContext to invalidCredentials', () => {
    expect(
      getUserFacingApiErrorMessage(t, { response: { status: 401 } }, { logContext: 'x', loginContext: true }),
    ).toBe('common.auth.invalidCredentials');
  });

  it('prefers backend message on 401 in loginContext when present', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        { response: { status: 401, data: { code: 'INVALID_PASSWORD', message: 'Ungültiges Passwort' } } },
        { logContext: 'x', loginContext: true },
      ),
    ).toBe('Ungültiges Passwort');
  });

  it('maps 401 without loginContext to http401', () => {
    expect(getUserFacingApiErrorMessage(t, { response: { status: 401 } }, { logContext: 'x' })).toBe(
      'common.errors.http401',
    );
  });

  it('prefers server error text on HTTP 400 when present', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        { response: { status: 400, data: { error: 'Closing blocked: 2 payment(s) without a matching invoice.' } } },
        { logContext: 'x' },
      ),
    ).toBe('Closing blocked: 2 payment(s) without a matching invoice.');
  });

  it('uses fallbackKey when status unknown', () => {
    expect(
      getUserFacingApiErrorMessage(t, new Error('weird'), {
        logContext: 'x',
        fallbackKey: 'tagesabschluss.errors.unknown',
      }),
    ).toBe('tagesabschluss.errors.unknown');
  });

  it('prefers registered code mapping over HTTP status', () => {
    registerApiErrorCodeTranslation('INVOICE_LOCKED', 'payments.errors.invoiceLocked');
    expect(
      getUserFacingApiErrorMessage(
        t,
        { response: { status: 409, data: { code: 'INVOICE_LOCKED', message: 'x' } } },
        { logContext: 'x' },
      ),
    ).toBe('payments.errors.invoiceLocked');
  });
});
