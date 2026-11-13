/**
 * POS payment / shift auto-open require a non-empty cash register GUID
 * (backend PaymentService.CreatePaymentAsync, AutoOpenShiftRequest optional Guid).
 * Empty Guid or a non-GUID string fails ASP.NET model binding with HTTP 400.
 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Authenticated POS screen: pick a register before auto-open / cash-register tabs. */
export const POS_CASH_REGISTER_SELECT_HREF = '/cash-register-select' as const;

export function isValidPosCashRegisterId(id: string | null | undefined): boolean {
  const t = String(id ?? '').trim();
  if (!t || t.toLowerCase() === EMPTY_GUID) return false;
  return GUID_RE.test(t);
}

/** Trimmed GUID when valid; otherwise null (do not POST auto-open). */
export function readValidPosCashRegisterId(id: string | null | undefined): string | null {
  const t = String(id ?? '').trim();
  return isValidPosCashRegisterId(t) ? t : null;
}

/** True when POS must send the operator to the register picker before auto-open. */
export function needsPosCashRegisterSelection(
  currentCashRegisterId: string | null | undefined
): boolean {
  return readValidPosCashRegisterId(currentCashRegisterId) == null;
}

/**
 * Register id that is safe to POST to /api/pos/shift/auto-open.
 * Null means omit cashRegisterId — the API resolves the persisted default or returns NEED_REGISTER_SELECTION.
 */
export function resolveAutoOpenShiftRegisterId(
  cashRegisterId: string | null | undefined
): string | null {
  return readValidPosCashRegisterId(cashRegisterId);
}
