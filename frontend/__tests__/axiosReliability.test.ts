import { describe, expect, it } from '@jest/globals';

import {
  getNetworkRetryCount,
  isAxiosNetworkError,
  isIdempotentHttpMethod,
  isNonRetryableApiPath,
  networkRetryDelayMs,
  shouldRetryAxiosNetworkError,
  withIncrementedRetryCount,
  NETWORK_RETRY_HEADER,
} from '../services/api/axiosReliability';

describe('axiosReliability', () => {
  it('detects transport network errors without response', () => {
    expect(isAxiosNetworkError({ code: 'ERR_NETWORK', message: 'Network Error' })).toBe(true);
    expect(
      isAxiosNetworkError({
        response: { status: 500 },
        code: 'ERR_BAD_RESPONSE',
        message: 'Request failed',
      })
    ).toBe(false);
    expect(isAxiosNetworkError({ code: 'ERR_CANCELED', name: 'CanceledError' })).toBe(false);
  });

  it('only treats GET/HEAD/OPTIONS as idempotent for auto-retry', () => {
    expect(isIdempotentHttpMethod('get')).toBe(true);
    expect(isIdempotentHttpMethod('POST')).toBe(false);
    expect(isIdempotentHttpMethod('put')).toBe(false);
  });

  it('blocks retry on payment and auth paths', () => {
    expect(isNonRetryableApiPath('/api/pos/payment')).toBe(true);
    expect(isNonRetryableApiPath('/Auth/login')).toBe(true);
    expect(isNonRetryableApiPath('/pos/products/active')).toBe(false);
  });

  it('applies exponential backoff delays', () => {
    expect(networkRetryDelayMs(1)).toBe(300);
    expect(networkRetryDelayMs(2)).toBe(600);
    expect(networkRetryDelayMs(3)).toBe(1200);
  });

  it('increments retry header and gates max attempts', () => {
    const cfg = withIncrementedRetryCount({ method: 'get', url: '/pos/products', headers: {} });
    expect(getNetworkRetryCount(cfg)).toBe(1);

    const error = {
      code: 'ERR_NETWORK',
      message: 'Network Error',
      config: {
        method: 'get',
        url: '/pos/products',
        headers: { [NETWORK_RETRY_HEADER]: '2' },
      },
    };
    expect(shouldRetryAxiosNetworkError(error)).toBe(false);

    const retryable = {
      code: 'ERR_NETWORK',
      message: 'Network Error',
      config: { method: 'get', url: '/pos/products', headers: {} },
    };
    expect(shouldRetryAxiosNetworkError(retryable)).toBe(true);

    const paymentPost = {
      code: 'ERR_NETWORK',
      message: 'Network Error',
      config: { method: 'post', url: '/pos/payment', headers: {} },
    };
    expect(shouldRetryAxiosNetworkError(paymentPost)).toBe(false);
  });
});
