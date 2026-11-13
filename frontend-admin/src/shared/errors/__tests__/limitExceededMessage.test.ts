import { describe, expect, it } from 'vitest';

import {
  isLimitExceededError,
  limitExceededI18nKey,
  readLimitExceededPayload,
  translateLimitExceededError,
  translateLimitExceededOr,
} from '../limitExceededMessage';

const t = (key: string, options?: Record<string, string | number>) =>
  `${key}|${options?.limit ?? ''}|${options?.current ?? ''}`;

describe('limitExceededMessage', () => {
  it('reads classic 409 LimitErrorDto', () => {
    const error = {
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          limitKey: 'maxProductsPerTenant',
          limit: 10000,
          current: 10000,
          message: 'Maximum 10000 products per tenant reached',
          canForce: false,
        },
      },
    };

    expect(isLimitExceededError(error)).toBe(true);
    expect(readLimitExceededPayload(error)).toEqual({
      limitKey: 'maxProductsPerTenant',
      limit: 10000,
      current: 10000,
    });
    expect(translateLimitExceededError(t, error)).toBe(
      'tenants.limits.errors.maxProductsPerTenant|10000|10000'
    );
  });

  it('reads nested payment-style limitError', () => {
    const error = {
      response: {
        status: 409,
        data: {
          code: 'LIMIT_EXCEEDED',
          message: 'Offline queue limit of 50 reached',
          limitError: {
            code: 'LIMIT_EXCEEDED',
            limitKey: 'maxOfflineTransactions',
            limit: 50,
            current: 50,
          },
        },
      },
    };

    expect(readLimitExceededPayload(error)?.limitKey).toBe('maxOfflineTransactions');
    expect(limitExceededI18nKey('maxOfflineTransactions')).toBe(
      'tenants.limits.errors.maxOfflineTransactions'
    );
  });

  it('falls back to generic key and fallback copy when not a limit error', () => {
    expect(limitExceededI18nKey('unknown')).toBe('tenants.limits.errors.generic');
    expect(
      translateLimitExceededError(t, {
        response: { status: 409, data: { code: 'SLUG_CONFLICT' } },
      })
    ).toBeUndefined();
    expect(translateLimitExceededOr(t, new Error('nope'), 'fallback')).toBe('fallback');
  });
});
