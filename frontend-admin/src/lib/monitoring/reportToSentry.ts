/**
 * Thin Sentry reporting facade for FA.
 * Safe to import from client/server; no-ops when Sentry is disabled.
 */
import * as Sentry from '@sentry/nextjs';
import type { AxiosError } from 'axios';

import { registerErrorReporter } from '@/lib/logger';
import {
  type AxiosLikeError,
  isProductionSentryEnabled,
  shouldReportAxiosErrorToSentry,
} from '@/lib/monitoring/sentryFilter';

function resolveDsn(): string | undefined {
  const publicDsn = process.env.NEXT_PUBLIC_SENTRY_DSN?.trim();
  if (publicDsn) {
    return publicDsn;
  }
  // Server-only fallback (never expose secret DSNs; prefer the public project DSN).
  return process.env.SENTRY_DSN?.trim() || undefined;
}

export function isSentryActive(): boolean {
  return isProductionSentryEnabled(process.env.NODE_ENV, resolveDsn());
}

export type CaptureContext = {
  tags?: Record<string, string>;
  extra?: Record<string, unknown>;
  level?: 'fatal' | 'error' | 'warning' | 'log' | 'info' | 'debug';
};

/** Custom exception capture — preferred over raw `Sentry.captureException` in feature code. */
export function captureException(error: unknown, context?: CaptureContext): string | undefined {
  if (!isSentryActive()) {
    return undefined;
  }
  return Sentry.captureException(error, {
    level: context?.level ?? 'error',
    tags: context?.tags,
    extra: context?.extra,
  });
}

/** Custom message capture for non-exception operational signals. */
export function captureMessage(message: string, context?: CaptureContext): string | undefined {
  if (!isSentryActive()) {
    return undefined;
  }
  return Sentry.captureMessage(message, {
    level: context?.level ?? 'info',
    tags: context?.tags,
    extra: context?.extra,
  });
}

/**
 * Report an axios failure when it passes the noise filter (typically HTTP 5xx).
 * Attaches method/url/status tags without request bodies or auth headers.
 */
export function reportAxiosErrorToSentry(error: unknown): void {
  if (!isSentryActive() || !shouldReportAxiosErrorToSentry(error)) {
    return;
  }

  const axiosError = error as AxiosError & AxiosLikeError;
  const status = axiosError.response?.status;
  const method = axiosError.config?.method?.toUpperCase();
  const url = axiosError.config?.url;

  captureException(error, {
    tags: {
      source: 'axios',
      ...(typeof status === 'number' ? { httpStatus: String(status) } : {}),
      ...(method ? { httpMethod: method } : {}),
    },
    extra: {
      url: typeof url === 'string' ? url : undefined,
      code: axiosError.code,
    },
  });
}

/**
 * Wire `logger.error` / `technicalConsole.error` → Sentry.
 * Call once after client (or server) `Sentry.init`.
 */
export function registerSentryErrorReporter(): void {
  if (!isSentryActive()) {
    registerErrorReporter(null);
    return;
  }

  registerErrorReporter((error, meta) => {
    const extraArgs = meta?.args;
    const record = meta?.record;
    const extra =
      extraArgs && extraArgs.length > 0
        ? { args: extraArgs, ...(record ? { log: record } : {}) }
        : record
          ? { log: record }
          : undefined;

    if (error instanceof Error) {
      captureException(error, {
        tags: { source: 'logger' },
        extra,
      });
      return;
    }
    if (typeof error === 'string') {
      captureMessage(error, {
        level: 'error',
        tags: { source: 'logger' },
        extra,
      });
      return;
    }
    captureException(error ?? new Error('Unknown logger error'), {
      tags: { source: 'logger' },
      extra,
    });
  });
}

/** Dev / ops helper: send a deliberate test event (no-op when Sentry is inactive). */
export function sendSentryTestEvent(reason = 'manual-test'): string | undefined {
  return captureException(new Error(`Sentry test event (${reason})`), {
    tags: { source: 'sentry-test', reason },
    level: 'warning',
  });
}
