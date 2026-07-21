/**
 * Single place to parse GET /api/pos/payment/methods (and similar) response bodies.
 * Backend SuccessResponse shape: { success, message, data: T, timestamp }.
 * Does not invent fields; drops rows without a usable id.
 * `type` is the stable code sent in POST payment.method (admin-configurable).
 */

const CODE_PATTERN = /^[a-z0-9_-]+$/;

export type NormalizedPosPaymentMethod = {
  id: string;
  name: string;
  /** Stable payment method code (POST payment.method). */
  type: string;
  icon: string;
  isDefault?: boolean;
  requiresReceivedAmount?: boolean;
  requiresTerminal?: boolean;
  terminalType?: string | null;
  allowRefund?: boolean;
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

function coerceCode(raw: unknown): string | null {
  const s = String(raw ?? '')
    .toLowerCase()
    .trim();
  if (!s || !CODE_PATTERN.test(s)) return null;
  return s;
}

function coerceBool(raw: unknown): boolean | undefined {
  if (typeof raw === 'boolean') return raw;
  return undefined;
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
    const type = coerceType(row.type ?? row.Type ?? row.code ?? row.Code);
    if (!type) {
      console.warn(
        '[normalizeToPosPaymentMethods] Unsupported payment method code dropped:',
        row.type ?? row.Type
      );
      continue;
    }
    const icon = String(row.icon ?? row.Icon ?? 'ellipse-outline');
    const isDefault = coerceBool(row.isDefault ?? row.IsDefault);
    const requiresReceivedAmount = coerceBool(
      row.requiresReceivedAmount ?? row.RequiresReceivedAmount
    );
    const requiresTerminal = coerceBool(row.requiresTerminal ?? row.RequiresTerminal);
    const terminalType =
      row.terminalType != null || row.TerminalType != null
        ? String(row.terminalType ?? row.TerminalType ?? '')
        : undefined;
    const allowRefund = coerceBool(row.allowRefund ?? row.AllowRefund);
    out.push({
      id,
      name,
      type,
      icon,
      ...(isDefault !== undefined ? { isDefault } : {}),
      ...(requiresReceivedAmount !== undefined ? { requiresReceivedAmount } : {}),
      ...(requiresTerminal !== undefined ? { requiresTerminal } : {}),
      ...(terminalType !== undefined ? { terminalType: terminalType || null } : {}),
      ...(allowRefund !== undefined ? { allowRefund } : {}),
    });
  }
  return out;
}

function coerceType(raw: unknown): string | null {
  return coerceCode(raw);
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
