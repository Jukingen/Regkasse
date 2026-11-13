import { afterEach, describe, expect, it } from 'vitest';

import { buildSentryInitOptions, readSentrySampleRate } from '@/lib/monitoring/sentryInitOptions';

const keys = [
  'NODE_ENV',
  'SENTRY_DSN',
  'NEXT_PUBLIC_SENTRY_DSN',
  'SENTRY_TRACES_SAMPLE_RATE',
  'SENTRY_SAMPLE_RATE',
  'NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE',
  'NEXT_PUBLIC_SENTRY_SAMPLE_RATE',
] as const;

describe('sentry sample rates', () => {
  const previous = new Map<string, string | undefined>();

  afterEach(() => {
    for (const key of keys) {
      const value = previous.get(key);
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
    previous.clear();
  });

  function setEnv(name: (typeof keys)[number], value: string | undefined) {
    if (!previous.has(name)) previous.set(name, process.env[name]);
    if (value === undefined) delete process.env[name];
    else process.env[name] = value;
  }

  it('uses 10% traces and 50% errors in production', () => {
    setEnv('NODE_ENV', 'production');
    setEnv('NEXT_PUBLIC_SENTRY_DSN', 'https://public@o0.ingest.sentry.io/1');
    setEnv('SENTRY_TRACES_SAMPLE_RATE', undefined);
    setEnv('SENTRY_SAMPLE_RATE', undefined);

    const options = buildSentryInitOptions();
    expect(options.tracesSampleRate).toBe(0.1);
    expect(options.sampleRate).toBe(0.5);
  });

  it('reads SENTRY_TRACES_SAMPLE_RATE and SENTRY_SAMPLE_RATE', () => {
    setEnv('NODE_ENV', 'production');
    setEnv('NEXT_PUBLIC_SENTRY_DSN', 'https://public@o0.ingest.sentry.io/1');
    setEnv('SENTRY_TRACES_SAMPLE_RATE', '0.2');
    setEnv('SENTRY_SAMPLE_RATE', '1');

    const options = buildSentryInitOptions();
    expect(options.tracesSampleRate).toBe(0.2);
    expect(options.sampleRate).toBe(1);
  });

  it('rejects rates outside 0..1', () => {
    expect(readSentrySampleRate('1.5', 0.1)).toBe(0.1);
    expect(readSentrySampleRate('0.4', 0.1)).toBe(0.4);
  });
});
