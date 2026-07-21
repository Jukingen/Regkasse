import { describe, expect, it } from '@jest/globals';

import { isPaymentTransportFailure } from '@/utils/isPaymentTransportFailure';

describe('isPaymentTransportFailure (mid-payment disconnect)', () => {
  it('queues when axios reports ERR_NETWORK (connection lost during POST)', () => {
    expect(isPaymentTransportFailure({ code: 'ERR_NETWORK', message: 'Network Error' })).toBe(true);
  });

  it('queues on timeout / abort without HTTP response', () => {
    expect(
      isPaymentTransportFailure({
        code: 'ECONNABORTED',
        message: 'timeout of 30000ms exceeded',
      })
    ).toBe(true);
    expect(isPaymentTransportFailure({ message: 'Request aborted' })).toBe(true);
  });

  it('does not queue when the server returned an HTTP error body', () => {
    expect(
      isPaymentTransportFailure({
        response: { status: 500, data: { message: 'Internal' } },
        message: 'Request failed with status code 500',
      })
    ).toBe(false);
  });

  it('does not treat auth expiry as a transport failure', () => {
    expect(isPaymentTransportFailure({ message: 'Token expired' })).toBe(false);
  });
});
