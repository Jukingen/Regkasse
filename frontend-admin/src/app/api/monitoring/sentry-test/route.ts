/**
 * Optional production verification endpoint for Sentry.
 * Only active when ALL of:
 * - NODE_ENV === production
 * - NEXT_PUBLIC_SENTRY_DSN is set
 * - SENTRY_ENABLE_TEST_ENDPOINT === 'true'
 *
 * POST /api/monitoring/sentry-test → captures a test exception.
 * Remove or keep disabled in steady-state production.
 */
import { NextResponse } from 'next/server';

import { isSentryActive, sendSentryTestEvent } from '@/lib/monitoring/reportToSentry';

export const dynamic = 'force-dynamic';

export async function POST(): Promise<NextResponse> {
  const testEnabled = process.env.SENTRY_ENABLE_TEST_ENDPOINT === 'true';
  if (!testEnabled || !isSentryActive()) {
    return NextResponse.json({ ok: false, reason: 'disabled' }, { status: 404 });
  }

  const eventId = sendSentryTestEvent('api-route');
  return NextResponse.json({
    ok: true,
    eventId: eventId ?? null,
    hint: 'Check the Sentry Issues dashboard for "Sentry test event (api-route)".',
  });
}
