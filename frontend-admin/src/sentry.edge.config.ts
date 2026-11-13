/**
 * Edge runtime Sentry SDK (middleware / edge route handlers).
 */
import * as Sentry from '@sentry/nextjs';

import { buildSentryInitOptions } from '@/lib/monitoring/sentryInitOptions';

Sentry.init({
  ...buildSentryInitOptions(),
});
