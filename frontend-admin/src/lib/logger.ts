/**
 * Canonical FA app logger — English technical diagnostics only.
 *
 * Structured records include: timestamp (`time`), level, message (`msg`), and context
 * (`component`, `userId`, `sessionId`, …). Secrets are redacted before emit.
 *
 * - `log` / `info` / `debug` / `warn` → development only (`NODE_ENV === 'development'`)
 * - `error` → all environments (console); optional monitoring via `registerErrorReporter`
 *
 * Prefer `logger` over raw `console.*` in feature code.
 * Never log tokens, passwords, or full license/JWT payloads.
 *
 * @example
 * logger.info('Query ready', { component: 'ProductsPage' });
 * logger.child({ component: 'LoginForm' }).warn('Validation skipped');
 */
import { compactLogContext, type LogContext } from '@/lib/logging/logContext';
import {
  type TechnicalErrorReporter,
  bindLogContext,
  emitTechnicalLog,
  registerTechnicalErrorReporter,
  technicalConsole,
} from '@/shared/dev/technicalConsole';

export type Logger = {
  log: (...args: unknown[]) => void;
  info: (...args: unknown[]) => void;
  debug: (...args: unknown[]) => void;
  warn: (...args: unknown[]) => void;
  error: (...args: unknown[]) => void;
  child: (bound: LogContext) => Logger;
};

function createLogger(bound?: LogContext): Logger {
  const boundFields = bound ? compactLogContext(bound) : undefined;

  return {
    log: (...args: unknown[]) => {
      emitTechnicalLog('info', args, boundFields);
    },
    info: (...args: unknown[]) => {
      emitTechnicalLog('info', args, boundFields);
    },
    debug: (...args: unknown[]) => {
      emitTechnicalLog('debug', args, boundFields);
    },
    warn: (...args: unknown[]) => {
      emitTechnicalLog('warn', args, boundFields);
    },
    error: (...args: unknown[]) => {
      if (boundFields && Object.keys(boundFields).length > 0) {
        technicalConsole.error(...args, boundFields);
      } else {
        technicalConsole.error(...args);
      }
    },
    child: (next: LogContext) =>
      createLogger({
        ...bound,
        ...next,
      }),
  };
}

export const logger: Logger = createLogger();

/** Merge ambient context (userId / sessionId / component) for subsequent logs. */
export function setLogContext(partial: LogContext): void {
  bindLogContext(partial);
}

/** Wire Sentry (or similar): prefer `registerSentryErrorReporter()` from `@/lib/monitoring/reportToSentry`. */
export function registerErrorReporter(reporter: TechnicalErrorReporter | null): void {
  registerTechnicalErrorReporter(reporter);
}

export type { TechnicalErrorReporter as ErrorReporter, LogContext };
export { technicalConsole, bindLogContext };
