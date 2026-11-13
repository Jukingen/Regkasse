import { CanceledError } from 'axios';
import { describe, expect, it } from 'vitest';

import { isRequestCanceled, shouldSuppressCanceledRequestToast } from '@/lib/httpCancellation';

describe('httpCancellation', () => {
  it('detects CanceledError and ERR_CANCELED', () => {
    expect(isRequestCanceled(new CanceledError('canceled'))).toBe(true);
    expect(isRequestCanceled({ code: 'ERR_CANCELED' })).toBe(true);
    expect(isRequestCanceled({ name: 'AbortError' })).toBe(true);
    expect(isRequestCanceled({ message: 'Query was cancelled' })).toBe(true);
  });

  it('treats non-timeout ECONNABORTED as canceled', () => {
    expect(isRequestCanceled({ code: 'ECONNABORTED', message: 'aborted' })).toBe(true);
    expect(isRequestCanceled({ code: 'ECONNABORTED', message: 'timeout of 10000ms exceeded' })).toBe(
      false,
    );
  });

  it('detects aborted AbortSignal on config', () => {
    const controller = new AbortController();
    controller.abort();
    expect(isRequestCanceled({ config: { signal: controller.signal } })).toBe(true);
  });

  it('returns false for normal errors', () => {
    expect(isRequestCanceled(new Error('boom'))).toBe(false);
    expect(isRequestCanceled({ response: { status: 500 } })).toBe(false);
  });

  it('aliases toast suppression', () => {
    expect(shouldSuppressCanceledRequestToast({ code: 'ERR_CANCELED' })).toBe(true);
    expect(shouldSuppressCanceledRequestToast(new Error('x'))).toBe(false);
  });
});
