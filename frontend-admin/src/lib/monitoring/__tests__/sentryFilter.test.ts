import { describe, expect, it } from 'vitest';

import {
  isCanceledLikeError,
  isNetworkLikeError,
  isProductionSentryEnabled,
  shouldDropSentryEvent,
  shouldReportAxiosErrorToSentry,
} from '../sentryFilter';

describe('isProductionSentryEnabled', () => {
  it('requires production and a non-empty DSN', () => {
    expect(isProductionSentryEnabled('production', 'https://a@b/1')).toBe(true);
    expect(isProductionSentryEnabled('development', 'https://a@b/1')).toBe(false);
    expect(isProductionSentryEnabled('production', '')).toBe(false);
    expect(isProductionSentryEnabled('production', null)).toBe(false);
    expect(isProductionSentryEnabled('test', 'https://a@b/1')).toBe(false);
  });
});

describe('shouldReportAxiosErrorToSentry', () => {
  it('ignores 404 / 401 / network / cancel', () => {
    expect(shouldReportAxiosErrorToSentry({ response: { status: 404 }, isAxiosError: true })).toBe(
      false
    );
    expect(shouldReportAxiosErrorToSentry({ response: { status: 401 }, isAxiosError: true })).toBe(
      false
    );
    expect(
      shouldReportAxiosErrorToSentry({
        message: 'Network Error',
        code: 'ERR_NETWORK',
        isAxiosError: true,
      })
    ).toBe(false);
    expect(shouldReportAxiosErrorToSentry({ code: 'ERR_CANCELED', name: 'CanceledError' })).toBe(
      false
    );
  });

  it('reports HTTP 5xx', () => {
    expect(shouldReportAxiosErrorToSentry({ response: { status: 500 }, isAxiosError: true })).toBe(
      true
    );
    expect(shouldReportAxiosErrorToSentry({ response: { status: 503 }, isAxiosError: true })).toBe(
      true
    );
  });
});

describe('network / cancel helpers', () => {
  it('detects network-like errors', () => {
    expect(isNetworkLikeError({ message: 'Failed to fetch', isAxiosError: true })).toBe(true);
    expect(isNetworkLikeError({ response: { status: 500 }, isAxiosError: true })).toBe(false);
  });

  it('detects cancel-like errors', () => {
    expect(isCanceledLikeError({ name: 'AbortError', message: 'aborted' })).toBe(true);
    expect(isCanceledLikeError({ message: 'boom' })).toBe(false);
  });
});

describe('shouldDropSentryEvent', () => {
  it('drops tagged ignored HTTP statuses and network messages', () => {
    expect(shouldDropSentryEvent({ tags: { httpStatus: '404' } })).toBe(true);
    expect(
      shouldDropSentryEvent({
        exception: { values: [{ type: 'Error', value: 'Network Error' }] },
      })
    ).toBe(true);
    expect(
      shouldDropSentryEvent({
        tags: { httpStatus: '500' },
        exception: { values: [{ type: 'Error', value: 'Internal Server Error' }] },
      })
    ).toBe(false);
  });
});
