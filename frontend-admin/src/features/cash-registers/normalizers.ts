import type { CashRegister } from '@/api/generated/model';

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

/** Normalizes `GET /api/CashRegister` list payloads. */
export function normalizeCashRegisterList(data: unknown): CashRegister[] {
  if (Array.isArray(data)) return data as CashRegister[];
  if (isRecord(data) && Array.isArray(data.registers)) {
    return data.registers as CashRegister[];
  }
  return [];
}
