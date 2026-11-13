import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearApiErrorCodeRegistryForTests } from '@/shared/errors/apiErrorCodeRegistry';
import { normalizeApiError } from '@/shared/errors/normalizedApiError';
import { resetDefaultApiErrorTranslationsFlagForTests } from '@/lib/api/errorTranslator';

import {
  getTagesabschlussUserFacingError,
  inferTagesabschlussErrorCode,
  resetTagesabschlussApiErrorTranslationsFlagForTests,
} from '../tagesabschlussApiErrors';

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn() },
}));

const t = (key: string) => key;

describe('tagesabschlussApiErrors', () => {
  afterEach(() => {
    clearApiErrorCodeRegistryForTests();
    resetDefaultApiErrorTranslationsFlagForTests();
    resetTagesabschlussApiErrorTranslationsFlagForTests();
  });

  it('infers codes from English backend messages', () => {
    expect(inferTagesabschlussErrorCode('A reason is required for backdated (nachträglich) daily closings.')).toBe(
      'BACKDATED_REASON_REQUIRED'
    );
    expect(inferTagesabschlussErrorCode('Daily closing already performed for today')).toBe('ALREADY_CLOSED_TODAY');
    expect(
      inferTagesabschlussErrorCode(
        'Closing blocked: 2 payment(s) without a matching invoice. Resolve gaps (e.g. run backfill) and try again.'
      )
    ).toBe('PAYMENTS_WITHOUT_INVOICE');
  });

  it('maps response code to i18n key', () => {
    expect(
      getTagesabschlussUserFacingError(
        t,
        {
          response: {
            status: 400,
            data: {
              code: 'BACKDATED_REASON_REQUIRED',
              error: 'A reason is required for backdated (nachträglich) daily closings.',
            },
          },
        },
        { logContext: 'test' }
      )
    ).toBe('tagesabschluss.errors.backdatedReasonRequired');
  });

  it('maps SCREAMING_SNAKE details via normalizeApiError', () => {
    const normalized = normalizeApiError({
      response: {
        status: 400,
        data: { error: 'Tenant context required', details: 'TENANT_CONTEXT_REQUIRED' },
      },
    });
    expect(normalized.code).toBe('TENANT_CONTEXT_REQUIRED');
    expect(normalized.rawMessage).toBe('Tenant context required');
  });
});
