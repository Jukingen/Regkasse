import { describe, expect, it } from 'vitest';
import { collectFieldErrors, normalizeApiError } from '../normalizedApiError';

describe('normalizeApiError', () => {
  it('reads axios response.data.message and http status', () => {
    const n = normalizeApiError({
      response: { status: 409, data: { message: '  Conflict  ' } },
    });
    expect(n.httpStatus).toBe(409);
    expect(n.rawMessage).toBe('Conflict');
    expect(n.code).toBeUndefined();
  });

  it('reads code from response.data.code', () => {
    const n = normalizeApiError({
      response: {
        status: 400,
        data: { code: 'PAYMENT_ALREADY_SETTLED', message: 'Cannot refund' },
      },
    });
    expect(n.code).toBe('PAYMENT_ALREADY_SETTLED');
    expect(n.rawMessage).toBe('Cannot refund');
  });

  it('reads errorCode and error_code', () => {
    expect(normalizeApiError({ response: { data: { errorCode: 'E_1' } } }).code).toBe('E_1');
    expect(normalizeApiError({ response: { data: { error_code: 'E_2' } } }).code).toBe('E_2');
  });

  it('ignores RFC7807 type URL as code', () => {
    const n = normalizeApiError({
      response: {
        status: 400,
        data: {
          type: 'https://example.com/probs/out-of-stock',
          title: 'Out of stock',
        },
      },
    });
    expect(n.code).toBeUndefined();
    expect(n.rawMessage).toBe('Out of stock');
  });

  it('collects field errors and uses first for rawMessage priority after data.message', () => {
    const n = normalizeApiError({
      response: {
        status: 422,
        data: {
          errors: { Email: ['Invalid format', 'Required'], Name: 'Too short' },
        },
      },
    });
    expect(n.fieldErrors).toEqual({
      Email: ['Invalid format', 'Required'],
      Name: ['Too short'],
    });
    expect(n.rawMessage).toBe('Invalid format');
  });

  it('reads optional severity, retryable, remediation, traceId', () => {
    const n = normalizeApiError({
      response: {
        status: 503,
        data: {
          message: 'Down',
          severity: 'warning',
          retryable: true,
          remediationHint: 'Retry later',
          traceId: 'abc-123',
        },
      },
    });
    expect(n.severity).toBe('warning');
    expect(n.retryable).toBe(true);
    expect(n.remediationHint).toBe('Retry later');
    expect(n.traceId).toBe('abc-123');
  });

  it('reads normalized.message on client wrapper', () => {
    const n = normalizeApiError({
      normalized: { message: 'Wrapped' },
    } as { normalized: { message: string } });
    expect(n.rawMessage).toBe('Wrapped');
  });
});

describe('collectFieldErrors', () => {
  it('returns undefined for empty', () => {
    expect(collectFieldErrors({})).toBeUndefined();
  });
});
