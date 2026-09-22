import { NextResponse } from 'next/server';

import { serverLogger } from '@/lib/logging/serverLogger';
import type { LogLevel } from '@/lib/logging/types';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const ALLOWED_LEVELS = new Set<LogLevel>(['debug', 'info', 'warn', 'error']);

type IngestBody = {
  time?: unknown;
  level?: unknown;
  msg?: unknown;
  service?: unknown;
  component?: unknown;
  userId?: unknown;
  sessionId?: unknown;
  tenantId?: unknown;
  route?: unknown;
  [key: string]: unknown;
};

function asShortString(value: unknown, max = 256): string | undefined {
  if (typeof value !== 'string') {
    return undefined;
  }
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }
  return trimmed.slice(0, max);
}

/**
 * Same-origin structured log ingest for browser beacons.
 * Writes one pino line to stdout for Datadog / Loki / ELK / CloudWatch agents.
 * Enable client posts with NEXT_PUBLIC_LOG_BEACON=true at build time.
 */
export async function POST(request: Request) {
  if (process.env.NEXT_PUBLIC_LOG_BEACON?.trim().toLowerCase() !== 'true') {
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

  const body = json as IngestBody;
  const levelRaw = asShortString(body.level, 16)?.toLowerCase();
  const level = (levelRaw && ALLOWED_LEVELS.has(levelRaw as LogLevel) ? levelRaw : 'info') as LogLevel;
  const msg = asShortString(body.msg, 512) ?? 'client_log';

  // Drop obvious secret-shaped keys if a buggy client sends them.
  const safe: Record<string, unknown> = {
    source: 'log-beacon',
    time: asShortString(body.time, 40),
    component: asShortString(body.component, 128),
    userId: asShortString(body.userId, 80),
    sessionId: asShortString(body.sessionId, 80),
    tenantId: asShortString(body.tenantId, 80),
    route: asShortString(body.route, 256),
  };

  for (const [key, value] of Object.entries(body)) {
    if (
      key === 'password' ||
      key === 'token' ||
      key === 'accessToken' ||
      key === 'refreshToken' ||
      key === 'authorization' ||
      key === 'msg' ||
      key === 'level' ||
      key === 'service'
    ) {
      continue;
    }
    if (safe[key] !== undefined) {
      continue;
    }
    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
      safe[key] = typeof value === 'string' ? value.slice(0, 256) : value;
    }
  }

  serverLogger[level](safe, msg);

  return NextResponse.json({ ok: true });
}
