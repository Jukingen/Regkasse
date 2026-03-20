/** Cash register list normalizer for mixed backend payload shapes. */

export type CashRegisterRow = {
  id?: string;
  registerNumber?: string;
  location?: string;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

export function normalizeCashRegisterListBody(data: unknown): CashRegisterRow[] {
  if (!isRecord(data)) return [];
  const raw = data.registers;
  if (!Array.isArray(raw)) return [];
  return raw.filter(isRecord) as CashRegisterRow[];
}
