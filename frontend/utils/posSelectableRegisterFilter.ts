/**
 * Defensive filtering when a response shape looks like inventory (status field) but was used for the POS picker.
 * Canonical GET /api/pos/cash-register/selectable omits status — all rows pass.
 */

export type CashRegisterRowWithOptionalStatus = {
  id: string;
  registerNumber: string;
  location?: string;
  /** Backend RegisterStatus name when inventory API leaked into the picker response. */
  status?: string;
};

export function isPaymentUsableSelectableRow(row: CashRegisterRowWithOptionalStatus): boolean {
  const s = row.status?.trim().toLowerCase();
  if (!s) return true;
  return s !== 'closed';
}

export function filterPaymentUsableSelectableRows<T extends CashRegisterRowWithOptionalStatus>(
  rows: T[]
): T[] {
  return rows.filter(isPaymentUsableSelectableRow);
}
