import { scrubPosSentryEvent } from './sentryScrub';

/**
 * Starts the POS Sentry SDK only when EXPO_PUBLIC_SENTRY_DSN is set.
 * No DSN means no native module load and no events.
 */
export function initPosSentry(): void {
  const dsn = process.env.EXPO_PUBLIC_SENTRY_DSN?.trim();
  if (!dsn) return;

  void import('@sentry/react-native').then((Sentry) => {
    Sentry.init({
      dsn,
      environment: process.env.EXPO_PUBLIC_SENTRY_ENVIRONMENT?.trim() || undefined,
      sendDefaultPii: false,
      tracesSampleRate: 0.1,
      beforeSend(event) {
        return scrubPosSentryEvent(event as unknown as Record<string, unknown>) as unknown as typeof event;
      },
    });
  });
}
