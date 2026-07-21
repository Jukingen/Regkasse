import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

export type CashRegisterPickCandidate = Pick<
  AdminCashRegisterListItem,
  'id' | 'tenantId' | 'isDefaultForTenant'
>;

export type OperationalCashRegisterPickCandidate = CashRegisterPickCandidate &
  Pick<AdminCashRegisterListItem, 'status'>;

function scopeRegistersForTenant<T extends CashRegisterPickCandidate>(
  registers: T[],
  tenantId: string | null | undefined
): T[] {
  if (!tenantId) {
    return registers;
  }
  return registers.filter((row) => row.tenantId === tenantId);
}

/**
 * Resolves the register id FA should preselect for the current mandant.
 * Keeps an existing selection when it still belongs to the scoped list; otherwise auto-selects
 * only when {@link pickCashRegisterOnTenantSwitch} finds a sole register or a flagged default.
 */
export function pickPreferredCashRegisterId(
  registers: CashRegisterPickCandidate[],
  currentId: string | null | undefined,
  tenantId: string | null | undefined
): string | null {
  if (registers.length === 0) {
    return null;
  }

  const scoped = scopeRegistersForTenant(registers, tenantId);
  if (scoped.length === 0) {
    return null;
  }

  const normalizedCurrent = currentId?.trim();
  if (normalizedCurrent && scoped.some((row) => row.id === normalizedCurrent)) {
    return normalizedCurrent;
  }

  const effectiveTenantId = tenantId ?? scoped[0]?.tenantId;
  if (!effectiveTenantId) {
    return null;
  }

  return pickCashRegisterOnTenantSwitch(scoped, effectiveTenantId);
}

/**
 * Used when the mandant changes: auto-select only when a default is flagged or exactly one register exists.
 */
export function pickCashRegisterOnTenantSwitch(
  registers: CashRegisterPickCandidate[],
  tenantId: string
): string | null {
  const scoped = registers.filter((row) => row.tenantId === tenantId);
  if (scoped.length === 0) {
    return null;
  }
  if (scoped.length === 1) {
    return scoped[0]?.id ?? null;
  }
  return scoped.find((row) => row.isDefaultForTenant === true)?.id ?? null;
}

/**
 * Operational auto-select for Manager dashboard / quick-switch (aligned with POS):
 * valid stored/current (kept when Open, or when nothing is Open) → sole → Open (+ default among Open) →
 * tenant default → first in list.
 */
export function pickOperationalCashRegisterId(
  registers: OperationalCashRegisterPickCandidate[],
  preferredId: string | null | undefined,
  tenantId: string | null | undefined
): string | null {
  const scoped = scopeRegistersForTenant(registers, tenantId);
  if (scoped.length === 0) {
    return null;
  }

  const open = scoped.filter((row) => row.status === REGISTER_STATUS.open);
  const normalizedPreferred = preferredId?.trim();
  if (normalizedPreferred) {
    const preferred = scoped.find((row) => row.id === normalizedPreferred);
    if (preferred) {
      const preferredIsOpen = preferred.status === REGISTER_STATUS.open;
      if (preferredIsOpen || open.length === 0) {
        return preferred.id;
      }
      // Stale closed/decommissioned preference while POS works on an Open register — follow Open.
    }
  }

  if (scoped.length === 1) {
    return scoped[0]?.id ?? null;
  }

  if (open.length === 1) {
    return open[0]?.id ?? null;
  }

  if (open.length > 1) {
    return open.find((row) => row.isDefaultForTenant === true)?.id ?? open[0]?.id ?? null;
  }

  const defaultRegister = scoped.find((row) => row.isDefaultForTenant === true);
  if (defaultRegister?.id) {
    return defaultRegister.id;
  }

  return scoped[0]?.id ?? null;
}
