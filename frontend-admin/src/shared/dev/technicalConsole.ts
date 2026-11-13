/**
 * Technical and developer diagnostics for the admin app.
 *
 * Policy: all human-readable log lines written here MUST be English. Never pass i18n `t(...)`
 * strings or operator-facing copy into these methods — use Ant Design notifications for UI.
 *
 * Emits structured records: `{ time, level, msg, service, component?, userId?, sessionId?, ... }`.
 * Secrets are redacted before emit. Prefer `logger` from `@/lib/logger` in feature code.
 */

import {
  buildStructuredLogRecord,
  maybeBeaconStructuredLog,
  writeStructuredToConsole,
} from '@/lib/logging/emitStructuredLog';
import { type LogContext, bindLogContext, getLogContext } from '@/lib/logging/logContext';
import { redactTechnicalLogArg } from '@/lib/logging/redact';
import type { LogLevel, StructuredLogRecord } from '@/lib/logging/types';

export type TechnicalErrorReporter = (
  error: unknown,
  meta?: { args?: unknown[]; record?: StructuredLogRecord },
) => void;

let errorReporter: TechnicalErrorReporter | null = null;

/** Optional monitoring hook (Sentry, etc.). Pass `null` to clear. */
export function registerTechnicalErrorReporter(reporter: TechnicalErrorReporter | null): void {
  errorReporter = reporter;
}

export { redactTechnicalLogArg, bindLogContext, getLogContext };
export type { LogContext, StructuredLogRecord, LogLevel };

function emit(level: LogLevel, args: unknown[], bound?: Record<string, string>): StructuredLogRecord {
  const record = buildStructuredLogRecord(level, args, bound);
  writeStructuredToConsole(level, record);
  maybeBeaconStructuredLog(record);
  return record;
}

/** @internal used by child loggers */
export function emitTechnicalLog(
  level: LogLevel,
  args: unknown[],
  bound?: Record<string, string>,
): StructuredLogRecord {
  return emit(level, args, bound);
}

export const technicalConsole = {
  /** Development-only informational trace (auth flow, query init, etc.). */
  devLog(...args: unknown[]) {
    emit('info', args);
  },

  /** Development-only verbose trace (HTTP debug, token attach, etc.). */
  devDebug(...args: unknown[]) {
    emit('debug', args);
  },

  /** Warnings (missing keys, legacy routes, parse issues). English-only message strings. */
  warn(...args: unknown[]) {
    emit('warn', args);
  },

  /** Errors (logout failure, export failure, API error summary). English-only message strings. */
  error(...args: unknown[]) {
    const record = emit('error', args);
    try {
      const redactedRest = args.slice(1).map((arg) => redactTechnicalLogArg(arg));
      errorReporter?.(args[0], { args: redactedRest, record });
    } catch {
      // Never let monitoring break the app.
    }
  },
};
