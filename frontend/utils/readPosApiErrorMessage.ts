/**
 * Reads backend error text from POS axios failures, including nested Fiskaly envelopes.
 */
export function readPosApiErrorMessage(error: unknown, fallback: string): string {
  const e = error as { response?: { data?: unknown }; message?: string } | null;
  const data = e?.response?.data;
  if (data && typeof data === 'object' && data !== null) {
    const record = data as Record<string, unknown>;
    const nested = record.error;
    if (nested && typeof nested === 'object' && nested !== null) {
      const n = nested as Record<string, unknown>;
      const message = typeof n.message === 'string' ? n.message.trim() : '';
      const details = typeof n.details === 'string' ? n.details.trim() : '';
      const code = typeof n.code === 'string' ? n.code.trim() : '';
      if (message && details) return `${message} (${code || details})`;
      if (message) return code ? `${message} [${code}]` : message;
      if (details) return details;
    }
    if (typeof record.message === 'string' && record.message.trim()) return record.message.trim();
    if (typeof record.error === 'string' && record.error.trim()) return record.error.trim();
  }
  if (typeof e?.message === 'string' && e.message.trim()) return e.message.trim();
  return fallback;
}
