/**
 * Defensive filtering for the POS picker: keeps rows the cashier can actually work on.
 *
 * GET /api/pos/cash-register/selectable returns Open *and* Closed rows.
 * Open + shift.open → select to continue. Closed + shift.open → select auto-opens the shift.
 * Closed without shift.open → request Mandanten-Admin. Permanently unusable states are dropped.
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

export type PosPickerRowKind = 'available' | 'opensOnSelect' | 'requestOpen' | 'unavailable';

/** True when the till has no open shift (`RegisterStatus.Closed`). */
export function isClosedRegister(row: CashRegisterRowWithOptionalStatus): boolean {
  return row.status?.trim().toLowerCase() === 'closed';
}

/** Closed row that a user with `shift.open` opens by picking it. */
export function isOpenedOnSelect(row: CashRegisterRowWithOptionalStatus): boolean {
  return isClosedRegister(row);
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

/**
 * How the POS picker should treat a row. `canOpenShift` is JWT `shift.open` (not FA `isActive`).
 */
export function resolvePosPickerRowKind(
  row: CashRegisterRowWithOptionalStatus,
  canOpenShift: boolean
): PosPickerRowKind {
  if (!isPaymentUsableSelectableRow(row)) return 'unavailable';
  if (isClosedRegister(row)) return canOpenShift ? 'opensOnSelect' : 'requestOpen';
  return 'available';
}

function isOpenStatus(status: string | undefined): boolean {
  return status?.trim().toLowerCase() === 'open';
}

/**
 * Open rows first, then the rest. Each group is ordered by registerNumber.
 * Equal numbers keep input order.
 */
export function sortSelectableRegisters<T extends CashRegisterRowWithOptionalStatus>(
  registers: readonly T[]
): T[] {
  return registers
    .map((row, index) => ({ row, index }))
    .sort((a, b) => {
      const groupA = isOpenStatus(a.row.status) ? 0 : 1;
      const groupB = isOpenStatus(b.row.status) ? 0 : 1;
      if (groupA !== groupB) return groupA - groupB;
      const byNumber = a.row.registerNumber.localeCompare(b.row.registerNumber, 'de', {
        numeric: true,
        sensitivity: 'base',
      });
      if (byNumber !== 0) return byNumber;
      return a.index - b.index;
    })
    .map((entry) => entry.row);
}

/** Rows assigned to this user. Shared, blank, and other-user rows are dropped. */
export function filterAssignedToUser<T extends CashRegisterRowWithOptionalStatus>(
  registers: readonly T[],
  userId: string
): T[] {
  if (registers.length === 0 || userId.trim() === '') return [];
  return registers.filter((row) => row.assignedUserId === userId);
}
