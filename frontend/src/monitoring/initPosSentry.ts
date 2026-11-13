import { scrubPosSentryEvent } from './sentryScrub';

type SentryEnv = Record<string, string | undefined>;

/** Parses a 0..1 sample rate. Invalid values fall back. */
export function readPosSentrySampleRate(raw: string | undefined, fallback: number): number {
  if (!raw?.trim()) {
    return fallback;
  }
  const parsed = Number(raw.trim());
  if (!Number.isFinite(parsed) || parsed < 0 || parsed > 1) {
    return fallback;
  }
  return parsed;
}

function firstEnv(env: SentryEnv, ...names: string[]): string | undefined {
  for (const name of names) {
    const value = env[name]?.trim();
    if (value) {
      return value;
    }
  }
  return undefined;
}

/** Transaction sample 0.1 and error sample 0.5, overridable via SENTRY_* env vars. */
export function resolvePosSentrySampleRates(env: SentryEnv = process.env): {
  tracesSampleRate: number;
  sampleRate: number;
} {
  return {
    tracesSampleRate: readPosSentrySampleRate(
      firstEnv(env, 'SENTRY_TRACES_SAMPLE_RATE', 'EXPO_PUBLIC_SENTRY_TRACES_SAMPLE_RATE'),
      0.1
    ),
    sampleRate: readPosSentrySampleRate(
      firstEnv(env, 'SENTRY_SAMPLE_RATE', 'EXPO_PUBLIC_SENTRY_SAMPLE_RATE'),
      0.5
    ),
  };
}

/**
 * Starts the POS Sentry SDK only when EXPO_PUBLIC_SENTRY_DSN is set.
 * No DSN means no native module load and no events.
 */
export function initPosSentry(): void {
  const dsn = process.env.EXPO_PUBLIC_SENTRY_DSN?.trim();
  if (!dsn) return;

  const rates = resolvePosSentrySampleRates();

  void import('@sentry/react-native').then((Sentry) => {
    Sentry.init({
      dsn,
      environment: process.env.EXPO_PUBLIC_SENTRY_ENVIRONMENT?.trim() || undefined,
      sendDefaultPii: false,
      tracesSampleRate: rates.tracesSampleRate,
      sampleRate: rates.sampleRate,
      beforeSend(event) {
        return scrubPosSentryEvent(event as unknown as Record<string, unknown>) as unknown as typeof event;
      },
    });
  });
}
