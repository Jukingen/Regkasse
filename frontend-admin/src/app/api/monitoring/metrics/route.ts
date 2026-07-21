import { NextResponse } from 'next/server';

import { serverLogger } from '@/lib/logging/serverLogger';
import { sanitizeApiPath } from '@/lib/monitoring/sanitizeApiPath';
import { API_RESPONSE_TIME_ALERT_MS } from '@/lib/monitoring/thresholds';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

type MetricBody = {
  type?: unknown;
  method?: unknown;
  path?: unknown;
  status?: unknown;
  durationMs?: unknown;
  ok?: unknown;
  ts?: unknown;
};

/**
 * Optional client API metric beacon → pino stdout (Datadog / Loki / ELK).
 * Enable with NEXT_PUBLIC_METRICS_BEACON=true at build time.
 */
export async function POST(request: Request) {
  if (process.env.NEXT_PUBLIC_METRICS_BEACON?.trim().toLowerCase() !== 'true') {
    return NextResponse.json({ ok: false, reason: 'beacon_disabled' }, { status: 404 });
  }

  let json: unknown;
  try {
    json = await request.json();
  } catch {
    return NextResponse.json({ ok: false, reason: 'invalid_json' }, { status: 400 });
  }

  if (!json || typeof json !== 'object') {
    return NextResponse.json({ ok: false, reason: 'invalid_payload' }, { status: 400 });
  }

  const body = json as MetricBody;
  const durationMs =
    typeof body.durationMs === 'number' && Number.isFinite(body.durationMs)
      ? Math.max(0, Math.round(body.durationMs))
      : null;
  if (durationMs == null) {
    return NextResponse.json({ ok: false, reason: 'invalid_duration' }, { status: 400 });
  }

  const method =
    typeof body.method === 'string' ? body.method.toUpperCase().slice(0, 16) : 'GET';
  const path = sanitizeApiPath(typeof body.path === 'string' ? body.path : 'unknown');
  const status =
    typeof body.status === 'number' && Number.isFinite(body.status) ? Math.round(body.status) : 0;
  const ok = body.ok === true;

  serverLogger.info(
    {
      type: 'api_metric',
      method,
      path,
      status,
      durationMs,
      ok,
      slow: durationMs > API_RESPONSE_TIME_ALERT_MS,
      clientTs: typeof body.ts === 'string' ? body.ts.slice(0, 40) : undefined,
    },
    'api_metric',
  );

  return NextResponse.json({ ok: true });
}
