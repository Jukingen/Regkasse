import { apiClient } from './config';
import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';
import {
  filterPaymentUsableSelectableRows,
  type CashRegisterRowWithOptionalStatus,
} from '../../utils/posSelectableRegisterFilter';

/**
 * Relative URL for POS selectable registers (backend: ICashRegisterResolutionService.ListSelectableForPosPickerAsync).
 * Admin inventory remains GET /api/CashRegister — do not use that for POS pickers.
 */
export const POS_SELECTABLE_REGISTERS_PATH = '/pos/cash-register/selectable' as const;

/** JSON row from GET /api/pos/cash-register/selectable (backend CashRegisterSelectableRow, camelCase). */
export interface CashRegisterSelectableRow {
  id: string;
  registerNumber: string;
  location?: string;
}

/**
 * @deprecated Use CashRegisterSelectableRow (name aligned with backend domain type).
 */
export type CashRegisterRow = CashRegisterSelectableRow;

/** When `registers` is empty, server may explain why (GET /api/pos/cash-register/selectable). */
export type PosSelectableEmptyReason =
  'no_registers' | 'none_open' | 'none_selectable_for_user' | null;

export type PosSelectableListPayload = {
  registers: CashRegisterSelectableRow[];
  emptyReason: PosSelectableEmptyReason;
};

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

/** Extract registers array from Ok({ registers }) or wrapped / alternate shapes. */
function extractRegistersArrayFromCashRegisterBody(body: unknown): unknown[] {
  if (Array.isArray(body)) return body;
  if (!isRecord(body)) return [];
  const direct =
    body.registers ?? body.Registers ?? body.items ?? body.Items ?? body.data ?? body.Data;
  if (Array.isArray(direct)) return direct;
  const once = unwrapApiResponseLayer(body);
  if (once !== body && once != null) return extractRegistersArrayFromCashRegisterBody(once);
  return [];
}

function parseSelectableEmptyReason(v: unknown): PosSelectableEmptyReason {
  const s = typeof v === 'string' ? v.trim() : '';
  if (s === 'no_registers' || s === 'none_open' || s === 'none_selectable_for_user') return s;
  return null;
}

function extractEmptyReasonFromBody(body: unknown): PosSelectableEmptyReason {
  if (!isRecord(body)) return null;
  return parseSelectableEmptyReason(body.emptyReason ?? body.EmptyReason);
}

/**
 * Fetches open, user-selectable cash registers for POS assignment (ListSelectableForPosPickerAsync).
 * Do not use GET /api/CashRegister — full inventory includes closed rows and breaks picker semantics.
 */
export async function fetchPosSelectableRegisters(): Promise<PosSelectableListPayload> {
  const raw = await apiClient.get<unknown>(POS_SELECTABLE_REGISTERS_PATH);
  let body: unknown = unwrapApiResponseLayer(raw);
  if (body !== raw) {
    body = unwrapApiResponseLayer(body);
  }
  const regs = extractRegistersArrayFromCashRegisterBody(body);
  const emptyReason = extractEmptyReasonFromBody(body);
  const parsed: CashRegisterRowWithOptionalStatus[] = [];
  for (const r of regs) {
    if (r == null || typeof r !== 'object') continue;
    const row = r as Record<string, unknown>;
    const id = String(row.id ?? row.Id ?? '').trim();
    if (!id) continue;
    const registerNumber = String(row.registerNumber ?? row.RegisterNumber ?? id).trim();
    const location =
      row.location != null || row.Location != null
        ? String(row.location ?? row.Location ?? '').trim()
        : undefined;
    const statusRaw = row.status ?? row.Status;
    const status =
      statusRaw != null && String(statusRaw).trim() !== '' ? String(statusRaw).trim() : undefined;
    parsed.push({ id, registerNumber, location: location || undefined, status });
  }
  const beforeFilterCount = parsed.length;
  const usable = filterPaymentUsableSelectableRows(parsed);
  const out: CashRegisterSelectableRow[] = usable.map(({ id, registerNumber, location }) => ({
    id,
    registerNumber,
    location,
  }));
  let effectiveEmptyReason: PosSelectableEmptyReason;
  if (out.length > 0) {
    effectiveEmptyReason = null;
  } else if (beforeFilterCount > 0) {
    effectiveEmptyReason = 'none_open';
  } else {
    effectiveEmptyReason = emptyReason;
  }
  return { registers: out, emptyReason: effectiveEmptyReason };
}
