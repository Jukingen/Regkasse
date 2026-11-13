import { describe, expect, it } from '@jest/globals';

import { readPosSentrySampleRate, resolvePosSentrySampleRates } from '../src/monitoring/initPosSentry';

describe('POS Sentry sample rates', () => {
  it('defaults to 10% traces and 50% errors', () => {
    expect(resolvePosSentrySampleRates({})).toEqual({
      tracesSampleRate: 0.1,
      sampleRate: 0.5,
    });
  });

  it('reads SENTRY_TRACES_SAMPLE_RATE and SENTRY_SAMPLE_RATE', () => {
    expect(
      resolvePosSentrySampleRates({
        SENTRY_TRACES_SAMPLE_RATE: '0.2',
        SENTRY_SAMPLE_RATE: '1',
      })
    ).toEqual({
      tracesSampleRate: 0.2,
      sampleRate: 1,
    });
  });

  it('rejects rates outside 0..1', () => {
    expect(readPosSentrySampleRate('2', 0.1)).toBe(0.1);
    expect(readPosSentrySampleRate('0.3', 0.1)).toBe(0.3);
  });
});
