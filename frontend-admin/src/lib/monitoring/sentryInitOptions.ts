/**
 * Shared Sentry.init options for client / server / edge.
 * Initialization is production-only and requires a DSN.
 */
import type { ErrorEvent, EventHint } from '@sentry/nextjs';

import {
  SENTRY_IGNORE_ERRORS,
  isProductionSentryEnabled,
  shouldDropSentryEvent,
} from '@/lib/monitoring/sentryFilter';

export function resolveSentryDsn(): string | undefined {
  const publicDsn = process.env.NEXT_PUBLIC_SENTRY_DSN?.trim();
  if (publicDsn) {
    return publicDsn;
  }
  return process.env.SENTRY_DSN?.trim() || undefined;
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
    // Performance: sample a slice of page loads + API spans in production.
    tracesSampleRate: enabled ? 0.1 : 0,
    // Prefer privacy: do not send default PII (cookies / IP enrichment).
    sendDefaultPii: false,
    ignoreErrors: SENTRY_IGNORE_ERRORS,
    beforeSend(event: ErrorEvent, _hint: EventHint): ErrorEvent | null {
      if (shouldDropSentryEvent(event)) {
        return null;
      }
      return event;
    },
  };
}
