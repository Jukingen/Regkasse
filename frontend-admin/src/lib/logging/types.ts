export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

/** Pino-compatible numeric levels (for scrapers that expect them). */
export const LOG_LEVEL_NUM: Record<LogLevel, number> = {
  debug: 20,
  info: 30,
  warn: 40,
  error: 50,
};

export type StructuredLogRecord = {
  /** ISO-8601 timestamp */
  time: string;
  /** Human-readable level */
  level: LogLevel;
  /** Pino-style numeric level */
  levelNum: number;
  msg: string;
  service: 'frontend-admin';
  env: string;
  component?: string;
  userId?: string;
  sessionId?: string;
  tenantId?: string;
  route?: string;
  /** Additional redacted fields */
  [key: string]: unknown;
};
