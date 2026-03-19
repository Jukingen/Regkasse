/**
 * Defensive parsing for Orval-generated clients that type some Tagesabschluss responses as void.
 * Aligns with backend JSON (camelCase). No invented fields.
 */

export type CashRegisterRow = {
  id?: string;
  registerNumber?: string;
  location?: string;
};

export type ClosingHistoryRow = {
  closingId?: string;
  closingDate?: string;
  closingType?: string;
  totalAmount?: number;
  totalTaxAmount?: number;
  transactionCount?: number;
  status?: string;
  finanzOnlineStatus?: string;
};

export type ClosingStatistics = {
  totalClosings?: number;
  totalAmount?: number;
  totalTaxAmount?: number;
  totalTransactions?: number;
  averageDailyAmount?: number;
  lastClosingDate?: string;
};

export type CanClosePayload = {
  canClose?: boolean;
  lastClosingDate?: string;
  paymentsWithoutInvoiceCount?: number;
  message?: string;
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

export function normalizeTagesabschlussHistory(data: unknown): ClosingHistoryRow[] {
  if (!Array.isArray(data)) return [];
  return data.filter(isRecord) as ClosingHistoryRow[];
}

export function normalizeTagesabschlussStatistics(data: unknown): ClosingStatistics | null {
  if (!isRecord(data)) return null;
  return data as ClosingStatistics;
}

export function normalizeCanClosePayload(data: unknown): CanClosePayload | undefined {
  if (!isRecord(data)) return undefined;
  return data as CanClosePayload;
}
