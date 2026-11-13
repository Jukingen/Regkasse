import type { RksvComplianceSpecialReceipt } from '@/features/rksv/compliance/types';
import type { CashRegisterRow } from '@/features/tagesabschluss/normalizers';

export const RKSV_KIND_STARTBELEG = 'Startbeleg';

export type StartbelegMissingRegister = {
  cashRegisterId: string;
  registerNumber?: string;
};

/**
 * Registers in scope that have no Startbeleg in the compliance report snapshot.
 * Uses the admin cash-register list (not only registers with receipts in the period).
 */
export function findRegistersMissingStartbeleg(
  registers: CashRegisterRow[],
  specialReceipts: RksvComplianceSpecialReceipt[],
  scopedCashRegisterId?: string
): StartbelegMissingRegister[] {
  const inScope = scopedCashRegisterId
    ? registers.filter((r) => r.id === scopedCashRegisterId)
    : registers;

  const withStartbeleg = new Set(
    specialReceipts
      .filter((s) => s.kind === RKSV_KIND_STARTBELEG && s.cashRegisterId)
      .map((s) => s.cashRegisterId as string)
  );

  return inScope
    .filter((r): r is CashRegisterRow & { id: string } => {
      const id = r.id?.trim();
      if (!id) return false;
      return !withStartbeleg.has(id);
    })
    .map((r) => ({
      cashRegisterId: r.id,
      registerNumber: r.registerNumber,
    }));
}
