/**
 * Build + emit structured log records (browser + Node without importing pino).
 * Server API routes prefer `@/lib/logging/serverLogger` (pino) for stdout aggregation.
 */

import { compactLogContext, getLogContext } from '@/lib/logging/logContext';
import { redactTechnicalLogArg } from '@/lib/logging/redact';
import {
  LOG_LEVEL_NUM,
  type LogLevel,
  type StructuredLogRecord,
} from '@/lib/logging/types';

export const LOG_SERVICE = 'frontend-admin' as const;
export const LOG_CONSOLE_PREFIX = '[regkasse-admin]';

function resolveEnv(): string {
  return (
    process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT?.trim() ||
    process.env.NODE_ENV ||
    'development'
  );
}

function isDevRuntime(): boolean {
  return process.env.NODE_ENV !== 'production';
}

function extractMessage(args: unknown[]): { msg: string; rest: unknown[] } {
  if (args.length === 0) {
    return { msg: '', rest: [] };
  }
  const first = args[0];
  if (typeof first === 'string') {
    return { msg: first, rest: args.slice(1) };
  }
  if (first instanceof Error) {
    return { msg: first.message || first.name || 'Error', rest: args.slice(1) };
  }
  return { msg: 'log', rest: args };
}

function mergeFieldArgs(rest: unknown[]): Record<string, unknown> {
  const fields: Record<string, unknown> = {};
  rest.forEach((arg, index) => {
    const redacted = redactTechnicalLogArg(arg);
    if (redacted && typeof redacted === 'object' && !Array.isArray(redacted)) {
      Object.assign(fields, redacted as Record<string, unknown>);
    } else {
      fields[`arg${index}`] = redacted;
    }
  });
  return fields;
}

export function buildStructuredLogRecord(
  level: LogLevel,
  args: unknown[],
  bound?: Record<string, string>,
): StructuredLogRecord {
  const { msg, rest } = extractMessage(args);
  const ctx = compactLogContext({ ...getLogContext(), ...bound });
  const fields = mergeFieldArgs(rest);

  // Prefer explicit Error shape when first arg was an Error (already redacted in fields if duplicated).
  const first = args[0];
  if (first instanceof Error && fields.name === undefined) {
    const errShape = redactTechnicalLogArg(first) as { name?: string; message?: string };
    if (errShape.name) {
      fields.errorName = errShape.name;
    }
  }

  return {
    time: new Date().toISOString(),
    level,
    levelNum: LOG_LEVEL_NUM[level],
    msg,
    service: LOG_SERVICE,
    env: resolveEnv(),
    ...ctx,
    ...fields,
  };
}

/**
 * Whether this level should print to the console.
 * - development: all levels
 * - production: error only (warn/info/debug stay quiet in the browser)
 */
export function shouldEmitToConsole(level: LogLevel): boolean {
  if (isDevRuntime()) {
    return true;
  }
  return level === 'error';
}

export function writeStructuredToConsole(level: LogLevel, record: StructuredLogRecord): void {
  if (!shouldEmitToConsole(level)) {
    return;
  }
  const line = LOG_CONSOLE_PREFIX;
  switch (level) {
    case 'debug':
      console.debug(line, record);
      break;
    case 'info':
      console.info(line, record);
      break;
    case 'warn':
      console.warn(line, record);
      break;
    case 'error':
      console.error(line, record);
      break;
    default:
      console.log(line, record);
  }
}

/** Optional same-origin beacon for warn/error aggregation (Datadog/ELK via API stdout). */
export function maybeBeaconStructuredLog(record: StructuredLogRecord): void {
  if (typeof window === 'undefined') {
    return;
  }
  if (process.env.NEXT_PUBLIC_LOG_BEACON?.trim().toLowerCase() !== 'true') {
    return;
  }
  if (record.level !== 'warn' && record.level !== 'error') {
    return;
  }
  try {
    const body = JSON.stringify(record);
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function') {
      const blob = new Blob([body], { type: 'application/json' });
      navigator.sendBeacon('/api/monitoring/logs', blob);
      return;
    }
    void fetch('/api/monitoring/logs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
      keepalive: true,
    }).catch(() => {
      // Never let logging break the app.
    });
  } catch {
    // ignore
  }
}
