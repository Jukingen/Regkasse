/**
 * Single place to parse GET /api/pos/payment/methods (and similar) response bodies.
 * Backend SuccessResponse shape: { success, message, data: T, timestamp }.
 * Does not invent fields; drops rows without a usable id.
 */

export type NormalizedPosPaymentMethod = {
  id: string;
  name: string;
  type: 'cash' | 'card' | 'voucher' | 'transfer';
  icon: string;
};

export function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

/** Extract a methods array from envelope or raw array (defensive). */
export function extractPaymentMethodsArrayFromApiBody(body: unknown): unknown[] {
  if (body == null) return [];
  if (Array.isArray(body)) return body;
  if (!isRecord(body)) return [];
  if (Array.isArray(body.data)) return body.data;
  if (Array.isArray(body.methods)) return body.methods;
  if (Array.isArray(body.Data)) return body.Data;
  return [];
}

const ALLOWED_TYPES = new Set(['cash', 'card', 'voucher', 'transfer']);

function coerceType(raw: unknown): NormalizedPosPaymentMethod['type'] {
  const s = String(raw ?? 'cash').toLowerCase();
  return (ALLOWED_TYPES.has(s) ? s : 'cash') as NormalizedPosPaymentMethod['type'];
}

/** Normalize to POS payment method rows used by paymentService / UI. */
export function normalizeToPosPaymentMethods(body: unknown): NormalizedPosPaymentMethod[] {
  const rows = extractPaymentMethodsArrayFromApiBody(body);
  const out: NormalizedPosPaymentMethod[] = [];
  for (const row of rows) {
    if (!isRecord(row)) continue;
    const id = String(row.id ?? row.Id ?? '').trim();
    if (!id) continue;
    const name = String(row.name ?? row.Name ?? id);
    const type = coerceType(row.type ?? row.Type);
    const icon = String(row.icon ?? row.Icon ?? 'ellipse-outline');
    out.push({ id, name, type, icon });
  }
  return out;
}

/**
 * Strips at most one wrapper layer (`Value`, `value`, `data`) from API payloads.
 * Used in paymentService for receipt/statistics only — not for GET payment methods (use `normalizeToPosPaymentMethods`).
 */
export function unwrapApiResponseLayer(raw: unknown): unknown {
  if (raw == null || typeof raw !== 'object') return raw;
  const o = raw as Record<string, unknown>;
  if ('Value' in o && o.Value != null) return o.Value;
  if ('value' in o && o.value != null) return o.value;
  if ('data' in o && o.data != null) return o.data;
  return raw;
}
