import { describe, expect, it } from '@jest/globals';

import {
  classifyDailyClosingError,
  extractDailyClosingErrorDetails,
  getDailyClosingErrorMessage,
  resolveDailyClosingFailureMessage,
} from '../utils/errorMessages';

describe('getDailyClosingErrorMessage', () => {
  it('returns a clear actionable German message for known codes', () => {
    expect(getDailyClosingErrorMessage('TSE_UNAVAILABLE')).toMatch(/TSE/i);
    expect(getDailyClosingErrorMessage('TSE_UNAVAILABLE')).toMatch(/prüfen|erneut/i);
  });

  it('includes payment count when provided', () => {
    const msg = getDailyClosingErrorMessage('PAYMENTS_WITHOUT_INVOICE', { count: 3 });
    expect(msg.startsWith('3 Zahlung')).toBe(true);
    expect(msg).toMatch(/beheben/i);
  });

  it('falls back for unknown codes', () => {
    expect(getDailyClosingErrorMessage('SOMETHING_WEIRD')).toMatch(/unbekannt/i);
  });
});

describe('classifyDailyClosingError', () => {
  it('maps block reasons from status API', () => {
    expect(classifyDailyClosingError({ blockReason: 'already_closed_today' })).toBe(
      'ALREADY_CLOSED'
    );
    expect(classifyDailyClosingError({ blockReason: 'no_active_shift' })).toBe('NO_ACTIVE_SHIFT');
    expect(classifyDailyClosingError({ blockReason: 'payments_without_invoice' })).toBe(
      'PAYMENTS_WITHOUT_INVOICE'
    );
  });

  it('maps English backend messages', () => {
    expect(
      classifyDailyClosingError({
        message: 'TSE device is not connected. Daily closing cannot be performed.',
      })
    ).toBe('TSE_UNAVAILABLE');
    expect(
      classifyDailyClosingError({
        message: 'No transactions found for today. Cannot perform daily closing.',
      })
    ).toBe('NO_SALES_TODAY');
    expect(
      classifyDailyClosingError({
        message: 'Closing blocked: 2 payment(s) without a matching invoice.',
        paymentsWithoutInvoiceCount: 2,
      })
    ).toBe('PAYMENTS_WITHOUT_INVOICE');
  });

  it('maps transport failures', () => {
    expect(classifyDailyClosingError({ axiosCode: 'ERR_NETWORK' })).toBe('NETWORK_ERROR');
    expect(classifyDailyClosingError({ axiosCode: 'ECONNABORTED' })).toBe('TIMEOUT');
    expect(classifyDailyClosingError({ httpStatus: 403 })).toBe('PERMISSION_DENIED');
    expect(classifyDailyClosingError({ httpStatus: 500 })).toBe('BACKEND_ERROR');
  });
});

describe('extractDailyClosingErrorDetails / resolveDailyClosingFailureMessage', () => {
  it('reads code and count from API-shaped errors', () => {
    const err = {
      name: 'DailyClosingApiError',
      message: 'Closing blocked: 2 payment(s)',
      code: 'PAYMENTS_WITHOUT_INVOICE',
      paymentsWithoutInvoiceCount: 2,
      httpStatus: 400,
    };
    const details = extractDailyClosingErrorDetails(err);
    expect(details.code).toBe('PAYMENTS_WITHOUT_INVOICE');
    expect(details.count).toBe(2);
    expect(resolveDailyClosingFailureMessage(err)).toMatch(/^2 Zahlung/);
  });

  it('classifies interceptor-shaped network errors', () => {
    const err = {
      status: undefined,
      data: undefined,
      message: 'Network Error',
      code: 'ERR_NETWORK',
    };
    expect(extractDailyClosingErrorDetails(err).code).toBe('NETWORK_ERROR');
  });
});
