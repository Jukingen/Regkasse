import { afterEach, describe, expect, it, vi } from 'vitest';

import deCommon from '@/i18n/locales/de/common.json';

import {
  clearApiErrorCodeRegistryForTests,
  registerApiErrorCodeTranslation,
} from '../apiErrorCodeRegistry';
import { getUserFacingApiErrorMessage } from '../userFacingApiError';

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn() },
}));

const t = (key: string) => key;

function tDe(key: string): string {
  const path = key.replace(/^common\./, '').split('.');
  let current: unknown = deCommon;
  for (const segment of path) {
    if (!current || typeof current !== 'object') return key;
    current = (current as Record<string, unknown>)[segment];
  }
  return typeof current === 'string' ? current : key;
}

describe('getUserFacingApiErrorMessage', () => {
  afterEach(() => {
    clearApiErrorCodeRegistryForTests();
  });

  it('maps 401 in loginContext to invalidCredentials', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        { response: { status: 401 } },
        { logContext: 'x', loginContext: true }
      )
    ).toBe('common.auth.invalidCredentials');
  });

  it('prefers backend message on 401 in loginContext when present', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        {
          response: {
            status: 401,
            data: { code: 'INVALID_PASSWORD', message: 'Ungültiges Passwort' },
          },
        },
        { logContext: 'x', loginContext: true }
      )
    ).toBe('Ungültiges Passwort');
  });

  it('maps 401 without loginContext to http401', () => {
    expect(
      getUserFacingApiErrorMessage(t, { response: { status: 401 } }, { logContext: 'x' })
    ).toBe('common.errors.http401');
  });

  it('prefers server error text on HTTP 400 when present', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        {
          response: {
            status: 400,
            data: { error: 'Closing blocked: 2 payment(s) without a matching invoice.' },
          },
        },
        { logContext: 'x' }
      )
    ).toBe('Closing blocked: 2 payment(s) without a matching invoice.');
  });

  it('uses fallbackKey when status unknown', () => {
    expect(
      getUserFacingApiErrorMessage(t, new Error('weird'), {
        logContext: 'x',
        fallbackKey: 'tagesabschluss.errors.unknown',
      })
    ).toBe('tagesabschluss.errors.unknown');
  });

  it('prefers registered code mapping over HTTP status', () => {
    registerApiErrorCodeTranslation('INVOICE_LOCKED', 'payments.errors.invoiceLocked');
    expect(
      getUserFacingApiErrorMessage(
        t,
        { response: { status: 409, data: { code: 'INVOICE_LOCKED', message: 'x' } } },
        { logContext: 'x' }
      )
    ).toBe('payments.errors.invoiceLocked');
  });

  it('replaces axios status 400 with the input-check message', () => {
    const expected = 'Die Anfrage konnte nicht verarbeitet werden. Bitte prüfen Sie Ihre Eingaben.';
    expect(deCommon.errors.http400).toBe(expected);
    expect(
      getUserFacingApiErrorMessage(
        tDe,
        {
          message: 'Request failed with status code 400',
          response: { status: 400 },
        },
        { logContext: 'x' }
      )
    ).toBe(expected);
  });

  it('replaces generic 403 with the permission message', () => {
    const expected = 'Sie haben keine Berechtigung für diese Aktion.';
    expect(deCommon.errors.http403).toBe(expected);
    expect(
      getUserFacingApiErrorMessage(tDe, { response: { status: 403 } }, { logContext: 'x' })
    ).toBe(expected);
  });

  it('replaces generic 404 with the not-found message', () => {
    const expected = 'Der gesuchte Eintrag wurde nicht gefunden.';
    expect(deCommon.errors.http404).toBe(expected);
    expect(
      getUserFacingApiErrorMessage(tDe, { response: { status: 404 } }, { logContext: 'x' })
    ).toBe(expected);
  });

  it('replaces Konflikt (Datenstand) with the open-table message', () => {
    const expected =
      'Dieser Tisch ist von einem anderen Kassierer geöffnet. Bitte schließen Sie ihn zuerst.';
    expect(deCommon.errors.http409).toBe(expected);
    expect(
      getUserFacingApiErrorMessage(
        tDe,
        {
          message: 'Konflikt (Datenstand).',
          response: { status: 409, data: { message: 'Konflikt (Datenstand).' } },
        },
        { logContext: 'x' }
      )
    ).toBe(expected);
  });

  it('maps LIMIT_EXCEEDED to the limit-key i18n instead of http409', () => {
    expect(
      getUserFacingApiErrorMessage(
        t,
        {
          response: {
            status: 409,
            data: {
              code: 'LIMIT_EXCEEDED',
              limitKey: 'maxActiveRegistersPerUser',
              limit: 5,
              current: 5,
              message: 'Max 5 active registers per user reached',
            },
          },
        },
        { logContext: 'x' }
      )
    ).toBe('tenants.limits.errors.maxActiveRegistersPerUser');
  });
});
