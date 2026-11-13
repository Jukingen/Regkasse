import axios, { AxiosError } from 'axios';
import { describe, expect, it } from 'vitest';

import {
  AXIOS_MAX_RETRIES,
  AXIOS_RETRY_DELAYS_MS,
  getRetryDelayMs,
  isAbortCancellation,
  isIdempotentMethod,
  isMutationMethod,
  isRetryableAxiosError,
  shouldRetryAxiosRequest,
} from '../axiosRetry';

function networkError(overrides?: Partial<AxiosError>): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: 'Network Error',
    toJSON: () => ({}),
    ...overrides,
  } as AxiosError;
}

function httpError(status: number, method = 'get'): AxiosError {
  return {
    isAxiosError: true,
    name: 'AxiosError',
    message: `Request failed with status code ${status}`,
    response: { status, data: {}, statusText: '', headers: {}, config: {} as never },
    config: { method } as never,
    toJSON: () => ({}),
  } as AxiosError;
}

describe('axiosRetry helpers', () => {
  it('classifies idempotent vs mutation methods', () => {
    expect(isIdempotentMethod('get')).toBe(true);
    expect(isIdempotentMethod('HEAD')).toBe(true);
    expect(isIdempotentMethod('options')).toBe(true);
    expect(isIdempotentMethod(undefined)).toBe(true);
    expect(isMutationMethod('post')).toBe(true);
    expect(isMutationMethod('PUT')).toBe(true);
    expect(isMutationMethod('patch')).toBe(true);
    expect(isMutationMethod('delete')).toBe(true);
    expect(isMutationMethod('get')).toBe(false);
  });

  it('uses exponential backoff 1s / 2s / 4s', () => {
    expect(getRetryDelayMs(1)).toBe(1000);
    expect(getRetryDelayMs(2)).toBe(2000);
    expect(getRetryDelayMs(3)).toBe(4000);
    expect(AXIOS_RETRY_DELAYS_MS).toEqual([1000, 2000, 4000]);
  });

  it('retries network errors, 429, and 5xx', () => {
    expect(isRetryableAxiosError(networkError())).toBe(true);
    expect(isRetryableAxiosError(httpError(429))).toBe(true);
    expect(isRetryableAxiosError(httpError(500))).toBe(true);
    expect(isRetryableAxiosError(httpError(503))).toBe(true);
    expect(isRetryableAxiosError(httpError(400))).toBe(false);
    expect(isRetryableAxiosError(httpError(401))).toBe(false);
    expect(isRetryableAxiosError(httpError(403))).toBe(false);
    expect(isRetryableAxiosError(httpError(404))).toBe(false);
  });

  it('retries GET on 429 with backoff but never retries mutations on 429', () => {
    const rateLimited = httpError(429);
    expect(shouldRetryAxiosRequest({ method: 'get', _retryCount: 0 }, rateLimited)).toBe(true);
    expect(shouldRetryAxiosRequest({ method: 'get', _retryCount: 2 }, rateLimited)).toBe(true);
    expect(
      shouldRetryAxiosRequest({ method: 'get', _retryCount: AXIOS_MAX_RETRIES }, rateLimited)
    ).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'post' }, rateLimited)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'put' }, rateLimited)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'patch' }, rateLimited)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'delete' }, rateLimited)).toBe(false);
  });

  it('does not treat timeouts as abort cancellation (timeouts stay retryable)', () => {
    const timeout = networkError({ code: 'ECONNABORTED', message: 'timeout of 10000ms exceeded' });
    expect(isAbortCancellation(timeout)).toBe(false);
    expect(isRetryableAxiosError(timeout)).toBe(true);
  });

  it('skips aborted / cancelled requests', () => {
    const canceled = new axios.CanceledError('Query was cancelled');
    expect(isAbortCancellation(canceled)).toBe(true);
    expect(isRetryableAxiosError(canceled)).toBe(false);
    expect(
      isAbortCancellation({
        code: 'ERR_CANCELED',
        message: 'canceled',
        name: 'CanceledError',
      })
    ).toBe(true);
  });

  it('retries GET up to 3 times and never retries mutations', () => {
    const err = networkError();
    expect(shouldRetryAxiosRequest({ method: 'get', _retryCount: 0 }, err)).toBe(true);
    expect(shouldRetryAxiosRequest({ method: 'get', _retryCount: 2 }, err)).toBe(true);
    expect(shouldRetryAxiosRequest({ method: 'get', _retryCount: AXIOS_MAX_RETRIES }, err)).toBe(
      false
    );

    expect(shouldRetryAxiosRequest({ method: 'post' }, err)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'put' }, err)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'delete' }, err)).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'patch' }, httpError(503))).toBe(false);
  });

  it('respects retry: false opt-out', () => {
    expect(shouldRetryAxiosRequest({ method: 'get', retry: false }, networkError())).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'get', retry: true }, networkError())).toBe(true);
  });

  it('does not retry non-retryable 4xx even when retry is explicitly true', () => {
    expect(shouldRetryAxiosRequest({ method: 'get', retry: true }, httpError(404))).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'get', retry: true }, httpError(401))).toBe(false);
    expect(shouldRetryAxiosRequest({ method: 'get', retry: true }, httpError(429))).toBe(true);
  });
});
