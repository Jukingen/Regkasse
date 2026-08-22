/**
 * Defensive filtering for the POS picker: keeps rows the cashier can actually work on.
 *
 * GET /api/pos/cash-register/selectable returns Open *and* Closed rows — picking a closed one opens it via
 * POST /api/pos/shift/auto-open. Only permanently unusable states are dropped here, which also hardens the
 * picker if the admin inventory response (every status) ever leaks into this code path.
 */

/** States a POS user can never open a shift on, whatever the picker returned. */
const NON_SELECTABLE_STATUSES = new Set(['decommissioned', 'maintenance', 'disabled']);

export type CashRegisterRowWithOptionalStatus = {
  id: string;
  registerNumber: string;
  location?: string;
  /** Backend RegisterStatus name, e.g. "Open" / "Closed". Absent on older backends. */
  status?: string;
  assignedUserId?: string | null;
};

/** True when the row is closed and therefore gets opened on pick rather than reused. */
export function isOpenedOnSelect(row: CashRegisterRowWithOptionalStatus): boolean {
  return row.status?.trim().toLowerCase() === 'closed';
}

export function isPaymentUsableSelectableRow(row: CashRegisterRowWithOptionalStatus): boolean {
  const s = row.status?.trim().toLowerCase();
  if (!s) return true;
  return !NON_SELECTABLE_STATUSES.has(s);
}

export function filterPaymentUsableSelectableRows<T extends CashRegisterRowWithOptionalStatus>(
  rows: T[]
): T[] {
  return rows.filter(isPaymentUsableSelectableRow);
}
