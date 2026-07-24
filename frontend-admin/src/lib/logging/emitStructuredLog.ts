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

/** Matches `DEV_TENANT_LOCAL_STORAGE_KEY` — keep string local to avoid auth ↔ logging cycles. */
const DEV_TENANT_STORAGE_KEY = 'dev_tenant_id';

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

/**
 * Safe browser extras for diagnostics (no tokens/passwords).
 * Prefer ambient `tenantId` from LogContextBinder; `devTenant` is the local override slug only.
 */
export function getBrowserLogExtras(): Record<string, string> {
  if (typeof window === 'undefined') {
    return {};
  }

  const out: Record<string, string> = {};
  try {
    const href = window.location?.href?.trim();
    if (href) {
      out.pageUrl = href.slice(0, 256);
    }
  } catch {
    // ignore
  }
  try {
    const ua = window.navigator?.userAgent?.trim();
    if (ua) {
      out.userAgent = ua.slice(0, 160);
    }
  } catch {
    // ignore
  }
  try {
    const tenant = window.localStorage?.getItem(DEV_TENANT_STORAGE_KEY)?.trim();
    if (tenant && tenant.length > 0 && tenant.length <= 64) {
      out.devTenant = tenant;
    }
  } catch {
    // ignore
  }
  return out;
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

/** Merge Error / AxiosError fields into the record without overwriting explicit caller fields. */
function mergeErrorFields(fields: Record<string, unknown>, error: Error): void {
  const errShape = redactTechnicalLogArg(error);
  if (!errShape || typeof errShape !== 'object' || Array.isArray(errShape)) {
    return;
  }
  for (const [key, value] of Object.entries(errShape as Record<string, unknown>)) {
    if (key === 'message') {
      // Already captured as `msg`.
      continue;
    }
    if (fields[key] === undefined) {
      fields[key] = value;
    }
  }
  if (fields.errorName === undefined && error.name) {
    fields.errorName = error.name;
  }
}

export function buildStructuredLogRecord(
  level: LogLevel,
  args: unknown[],
  bound?: Record<string, string>
): StructuredLogRecord {
  const { msg, rest } = extractMessage(args);
  const ctx = compactLogContext({ ...getLogContext(), ...bound });
  const browser = getBrowserLogExtras();
  const fields = mergeFieldArgs(rest);

  const first = args[0];
  if (first instanceof Error) {
    mergeErrorFields(fields, first);
  }

  return {
    time: new Date().toISOString(),
    level,
    levelNum: LOG_LEVEL_NUM[level],
    msg,
    service: LOG_SERVICE,
    env: resolveEnv(),
    ...browser,
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

function fallbackRecord(level: LogLevel): StructuredLogRecord {
  return {
    time: new Date().toISOString(),
    level,
    levelNum: LOG_LEVEL_NUM[level],
    msg: '(missing log record)',
    service: LOG_SERVICE,
    env: resolveEnv(),
  };
}

export function writeStructuredToConsole(
  level: LogLevel,
  record: StructuredLogRecord | null | undefined
): void {
  if (!shouldEmitToConsole(level)) {
    return;
  }
  // Never pass undefined as the second console arg — DevTools shows a useless "undefined".
  const safeRecord = record ?? fallbackRecord(level);
  const line = LOG_CONSOLE_PREFIX;
  switch (level) {
    case 'debug':
      // Intentional: structured console sink for local/dev diagnostics.
      // eslint-disable-next-line no-console -- structured console transport
      console.debug(line, safeRecord);
      break;
    case 'info':
      // eslint-disable-next-line no-console -- structured console transport
      console.info(line, safeRecord);
      break;
    case 'warn':
      // eslint-disable-next-line no-console -- structured console transport
      console.warn(line, safeRecord);
      break;
    case 'error':
      // eslint-disable-next-line no-console -- structured console transport
      console.error(line, safeRecord);
      break;
    default:
      // eslint-disable-next-line no-console -- structured console transport
      console.log(line, safeRecord);
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
