/**
 * Node.js server Sentry SDK for Next.js App Router / Route Handlers / SSR.
 */
import * as Sentry from '@sentry/nextjs';

import { registerSentryErrorReporter } from '@/lib/monitoring/reportToSentry';
import { buildSentryInitOptions } from '@/lib/monitoring/sentryInitOptions';

Sentry.init({
  ...buildSentryInitOptions(),
});

registerSentryErrorReporter();
