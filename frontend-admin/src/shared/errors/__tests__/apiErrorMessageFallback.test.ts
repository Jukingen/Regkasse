import { describe, expect, it } from 'vitest';
import { extractApiErrorMessage } from '../apiErrorMessageFallback';

describe('extractApiErrorMessage', () => {
  it('returns raw message when present', () => {
    expect(
      extractApiErrorMessage({ response: { data: { message: 'API detail' } } }, 'fallback'),
    ).toBe('API detail');
  });

  it('returns fallback when nothing extractable', () => {
    expect(extractApiErrorMessage({}, 'fb')).toBe('fb');
  });
});
