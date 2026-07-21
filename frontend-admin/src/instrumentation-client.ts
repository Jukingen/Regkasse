/**
 * Browser Sentry SDK — loaded by Next.js via instrumentation-client.
 * Unhandled exceptions / rejections are captured automatically when enabled.
 */
import * as Sentry from '@sentry/nextjs';

import { registerSentryErrorReporter } from '@/lib/monitoring/reportToSentry';
import { buildSentryInitOptions } from '@/lib/monitoring/sentryInitOptions';

Sentry.init({
  ...buildSentryInitOptions(),
  // Browser tracing for slow navigations + fetch/XHR (axios) spans.
  integrations: [Sentry.browserTracingIntegration()],
});

registerSentryErrorReporter();

/** App Router navigation transitions → performance traces. */
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
