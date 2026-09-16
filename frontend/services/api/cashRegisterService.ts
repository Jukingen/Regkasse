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
  /** RegisterStatus name, e.g. "Open" / "Closed". A closed row is opened when the user picks it (shift.open). */
  status?: string;
  /** Admin assignment. Null/omitted = shared. Not used for payment authorization. */
  assignedUserId?: string | null;
}

/**
 * @deprecated Use CashRegisterSelectableRow (name aligned with backend domain type).
 */
export type CashRegisterRow = CashRegisterSelectableRow;

/**
 * When `registers` is empty, server may explain why (GET /api/pos/cash-register/selectable).
 * `none_open` is no longer produced by the backend (closed registers are listed now) but stays
 * accepted so an older API keeps rendering a meaningful message.
 */
export type PosSelectableEmptyReason =
  'no_registers' | 'none_open' | 'none_assigned' | 'none_selectable_for_user' | null;

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
  if (
    s === 'no_registers' ||
    s === 'none_open' ||
    s === 'none_assigned' ||
    s === 'none_selectable_for_user'
  ) {
    return s;
  }
  return null;
}

function extractEmptyReasonFromBody(body: unknown): PosSelectableEmptyReason {
  if (!isRecord(body)) return null;
  return parseSelectableEmptyReason(body.emptyReason ?? body.EmptyReason);
}

/**
 * Fetches the user-selectable cash registers for POS assignment (ListSelectableForPosPickerAsync).
 * Rows may be Open or Closed — a closed one is opened by shift auto-open once the user picks it (requires shift.open).
 * Do not use GET /api/CashRegister — full inventory ignores assignment and shift occupancy.
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
    const assignedRaw = row.assignedUserId ?? row.AssignedUserId;
    const assignedUserId =
      assignedRaw != null && String(assignedRaw).trim() !== ''
        ? String(assignedRaw).trim()
        : null;
    parsed.push({
      id,
      registerNumber,
      location: location || undefined,
      status,
      assignedUserId,
    });
  }
  const beforeFilterCount = parsed.length;
  const usable = filterPaymentUsableSelectableRows(parsed);
  const out: CashRegisterSelectableRow[] = usable.map(
    ({ id, registerNumber, location, status, assignedUserId }) => ({
      id,
      registerNumber,
      location,
      status,
      assignedUserId,
    })
  );
  let effectiveEmptyReason: PosSelectableEmptyReason;
  if (out.length > 0) {
    effectiveEmptyReason = null;
  } else if (beforeFilterCount > 0) {
    // Server offered rows but every one is maintenance / disabled / decommissioned, so from the
    // cashier's point of view nothing is selectable.
    effectiveEmptyReason = 'none_selectable_for_user';
  } else {
    effectiveEmptyReason = emptyReason;
  }
  return { registers: out, emptyReason: effectiveEmptyReason };
}

export const POS_OPEN_REQUESTS_PATH = '/pos/cash-register/open-requests' as const;
export const POS_OPEN_REQUESTS_MINE_PATH = '/pos/cash-register/open-requests/mine' as const;

export type CashRegisterOpenRequestStatus = 'Pending' | 'Approved' | 'Denied' | 'Cancelled';

export type CashRegisterOpenRequestRow = {
  id: string;
  cashRegisterId: string;
  registerNumber?: string;
  status: CashRegisterOpenRequestStatus;
  requestedAt: string;
};

export type CashRegisterOpenRequestMutation = {
  succeeded: boolean;
  code?: string | null;
  error?: string | null;
  request?: CashRegisterOpenRequestRow | null;
};

function parseOpenRequestStatus(v: unknown): CashRegisterOpenRequestStatus {
  const s = typeof v === 'string' ? v.trim() : '';
  if (s === 'Approved' || s === 'Denied' || s === 'Cancelled' || s === 'Pending') return s;
  return 'Pending';
}

function mapOpenRequestRow(raw: unknown): CashRegisterOpenRequestRow | null {
  if (!isRecord(raw)) return null;
  const id = String(raw.id ?? raw.Id ?? '').trim();
  const cashRegisterId = String(raw.cashRegisterId ?? raw.CashRegisterId ?? '').trim();
  if (!id || !cashRegisterId) return null;
  return {
    id,
    cashRegisterId,
    registerNumber: String(raw.registerNumber ?? raw.RegisterNumber ?? '').trim() || undefined,
    status: parseOpenRequestStatus(raw.status ?? raw.Status),
    requestedAt: String(raw.requestedAt ?? raw.RequestedAt ?? ''),
  };
}

/** POST /api/pos/cash-register/open-requests — notify Mandanten-Admin to open a closed register. */
export async function requestPosCashRegisterOpen(
  registerId: string
): Promise<CashRegisterOpenRequestMutation> {
  const raw = await apiClient.post<unknown>(POS_OPEN_REQUESTS_PATH, { registerId });
  const body = unwrapApiResponseLayer(raw);
  if (!isRecord(body)) {
    return { succeeded: false, error: 'Request failed' };
  }
  const succeeded = Boolean(body.succeeded ?? body.Succeeded);
  const request = mapOpenRequestRow(body.request ?? body.Request);
  return {
    succeeded,
    code: body.code != null || body.Code != null ? String(body.code ?? body.Code) : null,
    error: body.error != null || body.Error != null ? String(body.error ?? body.Error) : null,
    request,
  };
}

/** GET /api/pos/cash-register/open-requests/mine */
export async function fetchMyPosCashRegisterOpenRequests(): Promise<CashRegisterOpenRequestRow[]> {
  const raw = await apiClient.get<unknown>(POS_OPEN_REQUESTS_MINE_PATH);
  const body = unwrapApiResponseLayer(raw);
  const list = Array.isArray(body)
    ? body
    : isRecord(body) && Array.isArray(body.requests ?? body.Requests)
      ? (body.requests ?? body.Requests)
      : [];
  if (!Array.isArray(list)) return [];
  return list.map(mapOpenRequestRow).filter((row): row is CashRegisterOpenRequestRow => row != null);
}

/** POST /api/pos/cash-register/default — persist UserSettings.CashRegisterId for later auto-open. */
export async function setDefaultPosCashRegister(registerId: string): Promise<string> {
  const raw = await apiClient.post<unknown>('/pos/cash-register/default', { registerId });
  const body = unwrapApiResponseLayer(raw);
  if (body != null && typeof body === 'object') {
    const row = body as Record<string, unknown>;
    const id = String(row.cashRegisterId ?? row.CashRegisterId ?? registerId).trim();
    if (id) return id;
  }
  return registerId;
}

export const POS_MONATSBELEG_NOTIFY_MANAGER_PATH =
  '/pos/cash-register/monatsbeleg/notify-manager' as const;

export type NotifyMonatsbelegManagerResult = {
  ok: boolean;
  code: string;
  message: string;
};

/** POST /api/pos/cash-register/monatsbeleg/notify-manager */
export async function notifyPosMonatsbelegManager(
  cashRegisterId: string
): Promise<NotifyMonatsbelegManagerResult> {
  const raw = await apiClient.post<unknown>(POS_MONATSBELEG_NOTIFY_MANAGER_PATH, {
    cashRegisterId,
  });
  const body = unwrapApiResponseLayer(raw);
  if (body != null && typeof body === 'object') {
    const row = body as Record<string, unknown>;
    return {
      ok: Boolean(row.ok ?? row.Ok),
      code: String(row.code ?? row.Code ?? ''),
      message: String(row.message ?? row.Message ?? ''),
    };
  }
  return { ok: false, code: 'UNKNOWN', message: '' };
}

/** GET /api/pos/cash-register/default — persisted default register id, or null. */
export async function getDefaultPosCashRegister(): Promise<string | null> {
  const raw = await apiClient.get<unknown>('/pos/cash-register/default');
  const body = unwrapApiResponseLayer(raw);
  if (body == null || typeof body !== 'object') return null;
  const row = body as Record<string, unknown>;
  const id = String(row.registerId ?? row.RegisterId ?? '').trim();
  return id || null;
}
