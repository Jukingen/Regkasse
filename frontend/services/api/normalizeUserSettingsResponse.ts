import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

/**
 * Peel SuccessResponse / Value / data wrappers so POS can read cashRegisterId even when the API
 * envelopes the payload (some gateways or legacy clients).
 */
export function flattenUserSettingsPayload(raw: unknown): Record<string, unknown> {
  let cur: unknown = raw;
  for (let i = 0; i < 5; i++) {
    if (!isRecord(cur)) return {};
    const next = unwrapApiResponseLayer(cur);
    if (next === cur) break;
    cur = next;
  }
  return isRecord(cur) ? cur : {};
}

/** Source object for settings fields: prefer unwrapped body, fall back to top-level response. */
export function resolveUserSettingsRecord(raw: unknown): Record<string, unknown> {
  const inner = flattenUserSettingsPayload(raw);
  if (Object.keys(inner).length > 0) return inner;
  return isRecord(raw) ? raw : {};
}

export function readCashRegisterIdFromSettingsPayload(
  o: Record<string, unknown>
): string | undefined {
  const v = o.cashRegisterId ?? o.CashRegisterId;
  if (typeof v === 'string' && v.trim() !== '') return v.trim();
  return undefined;
}
