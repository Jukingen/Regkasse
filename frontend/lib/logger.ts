/**
 * Structured POS logger. Never log tokens, PII, voucher codes, or payment secrets.
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

export type LogMeta = Record<string, unknown>;

const SENSITIVE_KEY =
  /^(.*(_|-|.)?)?(token|password|secret|authorization|cookie|voucher|voucherCode|pan|cardNumber|card_number|cvc|cvv|iban|email|phone|ssn|pin|clientSecret|client_secret|refreshToken|accessToken)(.*)?$/i;

const SENSITIVE_QUERY = /(?:token|secret|password|authorization|code|session|client_secret)=([^&#]*)/gi;

function redactUrl(value: string): string {
  try {
    const u = new URL(value);
    u.search = u.search ? '?redacted' : '';
    u.hash = '';
    return u.toString();
  } catch {
    return value.replace(SENSITIVE_QUERY, '$1=redacted');
  }
}

function redactValue(key: string, value: unknown, depth: number): unknown {
  if (depth > 4) return '[truncated]';
  if (SENSITIVE_KEY.test(key)) return '[redacted]';
  if (typeof value === 'string') {
    if (/^https?:\/\//i.test(value)) return redactUrl(value);
    if (value.length > 240) return `${value.slice(0, 80)}…[truncated]`;
    return value;
  }
  if (Array.isArray(value)) {
    return value.slice(0, 20).map((item, i) => redactValue(String(i), item, depth + 1));
  }
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      out[k] = redactValue(k, v, depth + 1);
    }
    return out;
  }
  return value;
}

export function redactLogMeta(meta?: LogMeta): LogMeta | undefined {
  if (!meta) return undefined;
  const out: LogMeta = {};
  for (const [key, value] of Object.entries(meta)) {
    out[key] = redactValue(key, value, 0);
  }
  return out;
}

function emit(level: LogLevel, event: string, meta?: LogMeta): void {
  const payload = {
    ts: new Date().toISOString(),
    level,
    event,
    ...(redactLogMeta(meta) ?? {}),
  };
  const line = `[pos] ${event}`;
  if (level === 'error') {
    console.error(line, payload);
    return;
  }
  if (level === 'warn') {
    console.warn(line, payload);
    return;
  }
  if (level === 'debug' && !__DEV__) {
    return;
  }
  if (__DEV__) {
    console.log(line, payload);
  }
}

export const logger = {
  debug(event: string, meta?: LogMeta): void {
    emit('debug', event, meta);
  },
  info(event: string, meta?: LogMeta): void {
    emit('info', event, meta);
  },
  warn(event: string, meta?: LogMeta): void {
    emit('warn', event, meta);
  },
  error(event: string, meta?: LogMeta): void {
    emit('error', event, meta);
  },
};
