import { NextResponse } from 'next/server';

import {
  type WebVitalName,
  type WebVitalPayload,
  exceedsBudget,
  sanitizeRoutePath,
} from '@/lib/monitoring/webVitalsBudgets';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const ALLOWED_NAMES = new Set<WebVitalName>(['CLS', 'FCP', 'INP', 'LCP', 'TTFB']);

type BeaconBody = Partial<WebVitalPayload> & { name?: string; value?: unknown };

function parseBody(raw: unknown): WebVitalPayload | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }
  const body = raw as BeaconBody;
  const name = body.name;
  if (typeof name !== 'string' || !ALLOWED_NAMES.has(name as WebVitalName)) {
    return null;
  }
  const value = typeof body.value === 'number' && Number.isFinite(body.value) ? body.value : null;
  if (value === null) {
    return null;
  }
  const rating =
    body.rating === 'good' || body.rating === 'needs-improvement' || body.rating === 'poor'
      ? body.rating
      : 'needs-improvement';
  const id = typeof body.id === 'string' && body.id.length > 0 ? body.id.slice(0, 64) : 'unknown';

  return {
    name: name as WebVitalName,
    value,
    rating,
    id,
    route: sanitizeRoutePath(typeof body.route === 'string' ? body.route : '/'),
    navigationType: typeof body.navigationType === 'string' ? body.navigationType.slice(0, 32) : undefined,
    delta: typeof body.delta === 'number' && Number.isFinite(body.delta) ? body.delta : undefined,
  };
}

/**
 * Same-origin Web Vitals beacon for self-hosted FA.
 * Emits a single structured log line for log scrapers (Loki / Datadog / CloudWatch).
 * Enable client posts with NEXT_PUBLIC_WEB_VITALS_BEACON=true at build time.
 */
export async function POST(request: Request) {
  if (process.env.NEXT_PUBLIC_WEB_VITALS_BEACON?.trim().toLowerCase() !== 'true') {
    return NextResponse.json({ ok: false, reason: 'beacon_disabled' }, { status: 404 });
  }

  let json: unknown;
  try {
    json = await request.json();
  } catch {
    return NextResponse.json({ ok: false, reason: 'invalid_json' }, { status: 400 });
  }

  const metric = parseBody(json);
  if (!metric) {
    return NextResponse.json({ ok: false, reason: 'invalid_payload' }, { status: 400 });
  }

  // Structured stdout for Grafana Loki / Datadog / CloudWatch scrapers (no PII).
  // eslint-disable-next-line no-console -- intentional telemetry sink for ops pipelines
  console.info(
    JSON.stringify({
      type: 'web_vital',
      name: metric.name,
      value: metric.value,
      rating: metric.rating,
      route: metric.route,
      id: metric.id,
      navigationType: metric.navigationType,
      degraded: exceedsBudget(metric.name, metric.value),
      ts: new Date().toISOString(),
    }),
  );

  return NextResponse.json({ ok: true });
}
