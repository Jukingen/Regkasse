/**
 * Server-only structured logger (pino).
 * Use from Route Handlers / instrumentation — do not import from Client Components.
 *
 * stdout JSON is scrape-friendly for Datadog / Grafana Loki / ELK / CloudWatch.
 */

import pino from 'pino';

import { LOG_SERVICE } from '@/lib/logging/emitStructuredLog';

const level =
  process.env.LOG_LEVEL?.trim() ||
  (process.env.NODE_ENV === 'production' ? 'info' : 'debug');

/**
 * Sensitive paths censored by pino before write.
 * Keep in sync with `src/lib/logging/redact.ts` intent.
 */
const REDACT_PATHS = [
  'password',
  'passwd',
  'pwd',
  'secret',
  'token',
  'accessToken',
  'refreshToken',
  'idToken',
  'authorization',
  'cookie',
  'licenseKey',
  'apiKey',
  'clientSecret',
  '*.password',
  '*.token',
  '*.accessToken',
  '*.refreshToken',
  '*.authorization',
  '*.secret',
  'req.headers.authorization',
  'req.headers.cookie',
];

export const serverLogger = pino({
  level,
  base: {
    service: LOG_SERVICE,
  },
  timestamp: pino.stdTimeFunctions.isoTime,
  formatters: {
    level(label) {
      return { level: label };
    },
  },
  redact: {
    paths: REDACT_PATHS,
    censor: '[REDACTED]',
  },
});

export type ServerLogger = typeof serverLogger;
