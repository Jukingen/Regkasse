import { isAxiosStatusMessage, userFacingPosHttpMessage } from './userFacingHttpError';

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

/** Axios body, whether still on `response.data` or already unwrapped by the POS client. */
export function readPosApiErrorBody(error: unknown): Record<string, unknown> | null {
  if (!error || typeof error !== 'object') return null;
  const e = error as { response?: { data?: unknown }; data?: unknown };
  return asRecord(e.response?.data) ?? asRecord(e.data);
}

function readText(record: Record<string, unknown>, ...keys: string[]): string {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'string' && value.trim()) return value.trim();
  }
  return '';
}

/** Past-month Monatsbeleg 400 (`RequiresForce`) before a generic status text. */
export function isMonatsbelegForceRequired(error: unknown): boolean {
  const record = readPosApiErrorBody(error);
  if (!record) return false;
  return record.requiresForce === true || record.RequiresForce === true;
}

/** Operator warning from `MonatsbelegWarningResponse`, or empty when the body has none. */
export function readPosApiWarningMessage(error: unknown): string {
  const record = readPosApiErrorBody(error);
  if (!record) return '';
  return readText(record, 'warningMessage', 'WarningMessage');
}

/**
 * Reads backend error text from POS axios failures, including nested Fiskaly envelopes.
 */
export function readPosApiErrorMessage(error: unknown, fallback: string): string {
  const record = readPosApiErrorBody(error);
  if (record) {
    const nested = asRecord(record.error);
    if (nested) {
      const message = readText(nested, 'message');
      const details = readText(nested, 'details');
      const code = readText(nested, 'code');
      if (message && details) return `${message} (${code || details})`;
      if (message) return code ? `${message} [${code}]` : message;
      if (details) return details;
    }
    const warning = readText(record, 'warningMessage', 'WarningMessage');
    if (warning) return warning;
    const message = readText(record, 'message');
    if (message) return message;
    const errorText = readText(record, 'error');
    if (errorText) return errorText;
  }
  const e = error as { message?: string; response?: { status?: number }; status?: number } | null;
  if (typeof e?.message === 'string' && e.message.trim()) {
    const trimmed = e.message.trim();
    if (isAxiosStatusMessage(trimmed)) {
      const status = e.response?.status ?? e.status;
      const friendly = typeof status === 'number' ? userFacingPosHttpMessage(status) : undefined;
      if (friendly) return friendly;
    }
    return trimmed;
  }
  return fallback;
}
