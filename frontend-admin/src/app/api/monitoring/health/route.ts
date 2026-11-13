import { NextResponse } from 'next/server';

import { serverLogger } from '@/lib/logging/serverLogger';
import { MONITORING_THRESHOLDS } from '@/lib/monitoring/thresholds';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const startedAt = Date.now();

/**
 * Detailed FA process health for ops dashboards / k8s readiness.
 * Public (see proxy allowlist) — no secrets.
 */
export async function GET() {
  const mem = process.memoryUsage();
  const payload = {
    status: 'ok' as const,
    service: 'frontend-admin',
    ts: new Date().toISOString(),
    uptimeSec: Math.round(process.uptime()),
    processUptimeSec: Math.round((Date.now() - startedAt) / 1000),
    node: process.version,
    env:
      process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT?.trim() ||
      process.env.NODE_ENV ||
      'development',
    memory: {
      rss: mem.rss,
      heapUsed: mem.heapUsed,
      heapTotal: mem.heapTotal,
    },
    thresholds: {
      apiErrorRate: MONITORING_THRESHOLDS.apiErrorRate,
      apiResponseTimeMs: MONITORING_THRESHOLDS.apiResponseTimeMs,
    },
    checks: {
      sentryConfigured: Boolean(
        process.env.NEXT_PUBLIC_SENTRY_DSN?.trim() || process.env.SENTRY_DSN?.trim(),
      ),
    },
  };

  serverLogger.debug({ type: 'health_check' }, 'health_ok');

  return NextResponse.json(payload, {
    status: 200,
    headers: { 'Cache-Control': 'no-store' },
  });
}
