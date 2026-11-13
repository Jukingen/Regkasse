/**
 * Shared Sentry.init options for client / server / edge.
 * Initialization is production-only and requires a DSN.
 */
import type { ErrorEvent, EventHint } from '@sentry/nextjs';

import {
  SENTRY_IGNORE_ERRORS,
  isProductionSentryEnabled,
  scrubSentryPii,
  shouldDropSentryEvent,
} from '@/lib/monitoring/sentryFilter';

export function resolveSentryDsn(): string | undefined {
  const publicDsn = process.env.NEXT_PUBLIC_SENTRY_DSN?.trim();
  if (publicDsn) {
    return publicDsn;
  }
  return process.env.SENTRY_DSN?.trim() || undefined;
}

/** Parses a 0..1 sample rate. Invalid values fall back. */
export function readSentrySampleRate(raw: string | undefined, fallback: number): number {
  if (!raw?.trim()) {
    return fallback;
  }
  const parsed = Number(raw.trim());
  if (!Number.isFinite(parsed) || parsed < 0 || parsed > 1) {
    return fallback;
  }
  return parsed;
}

function firstEnv(...names: string[]): string | undefined {
  for (const name of names) {
    const value = process.env[name]?.trim();
    if (value) {
      return value;
    }
  }
  return undefined;
}

/** Options spread into `Sentry.init` on every runtime. */
export function buildSentryInitOptions() {
  const dsn = resolveSentryDsn();
  const enabled = isProductionSentryEnabled(process.env.NODE_ENV, dsn);

  return {
    dsn,
    enabled,
    environment:
      process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT?.trim() ||
      process.env.SENTRY_ENVIRONMENT?.trim() ||
      process.env.NODE_ENV ||
      'development',
    release: process.env.NEXT_PUBLIC_SENTRY_RELEASE?.trim() || process.env.SENTRY_RELEASE?.trim(),
    // Performance: 10% of transactions unless SENTRY_TRACES_SAMPLE_RATE overrides it.
    tracesSampleRate: readSentrySampleRate(
      firstEnv('SENTRY_TRACES_SAMPLE_RATE', 'NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE'),
      enabled ? 0.1 : 0
    ),
    // Errors: 50% by default. Set SENTRY_SAMPLE_RATE=1 when error volume is low.
    sampleRate: readSentrySampleRate(
      firstEnv('SENTRY_SAMPLE_RATE', 'NEXT_PUBLIC_SENTRY_SAMPLE_RATE'),
      enabled ? 0.5 : 1
    ),
    // Prefer privacy: do not send default PII (cookies / IP enrichment).
    sendDefaultPii: false,
    ignoreErrors: SENTRY_IGNORE_ERRORS,
    beforeSend(event: ErrorEvent, _hint: EventHint): ErrorEvent | null {
      if (shouldDropSentryEvent(event)) {
        return null;
      }
      return scrubSentryPii(event);
    },
  };
}
