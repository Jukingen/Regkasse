import { describe, expect, it } from 'vitest';

import {
  extractHttpStatusFromUnknownError,
  extractRawApiErrorMessage,
} from '../extractRawApiErrorMessage';

describe('extractRawApiErrorMessage', () => {
  it('reads axios response.data.message', () => {
    expect(
      extractRawApiErrorMessage({
        response: { data: { message: '  Server says  ' } },
      })
    ).toBe('Server says');
  });

  it('prefers first validation string', () => {
    expect(
      extractRawApiErrorMessage({
        response: { data: { errors: { Password: ['Too short'] } } },
      })
    ).toBe('Too short');
  });

  it('returns undefined for empty', () => {
    expect(extractRawApiErrorMessage({ response: { data: {} } })).toBeUndefined();
  });

  it('reads normalized.message when present on error object', () => {
    expect(
      extractRawApiErrorMessage({
        normalized: { message: 'Wrapped client message' },
      })
    ).toBe('Wrapped client message');
  });
});

describe('extractHttpStatusFromUnknownError', () => {
  it('returns status when present', () => {
    expect(extractHttpStatusFromUnknownError({ response: { status: 403 } })).toBe(403);
  });
});
