import type { CashRegister } from '@/api/generated/model';
import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { REGISTER_STATUS, rawRegisterStatus, readStartbelegCreatedAt } from '@/features/cash-registers/utils/registerStatus';

/** Inventory / RKSV lifecycle — not the open till. */
export type RegisterLifecycleKind =
  | 'active'
  | 'inactive'
  | 'maintenance'
  | 'disabled'
  | 'decommissioned';

/** Open till vs no till. Distinct from `isActive`. */
export type RegisterShiftOccupancy = 'open' | 'closed' | 'none' | 'unavailable';

export function resolveRegisterLifecycleKind(register: CashRegister): RegisterLifecycleKind {
  const status = rawRegisterStatus(register);
  if (status === REGISTER_STATUS.decommissioned) return 'decommissioned';
  if (status === REGISTER_STATUS.maintenance) return 'maintenance';
  if (status === REGISTER_STATUS.disabled) return 'disabled';
  if (register.isActive === false) return 'inactive';
  return 'active';
}

export function readLastShiftAt(register: CashRegister): string | null {
  const raw = (register as EnhancedCashRegister).lastShiftAtUtc;
  if (typeof raw !== 'string') return null;
  const trimmed = raw.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function resolveRegisterShiftOccupancy(register: CashRegister): RegisterShiftOccupancy {
  const lifecycle = resolveRegisterLifecycleKind(register);
  if (lifecycle !== 'active') return 'unavailable';

  const status = rawRegisterStatus(register);
  if (status === REGISTER_STATUS.open) return 'open';
  if (status === REGISTER_STATUS.closed) {
    if (readLastShiftAt(register) || readStartbelegCreatedAt(register)) return 'closed';
    return 'none';
  }
  return 'unavailable';
}

export function registerLifecycleTagColor(
  kind: RegisterLifecycleKind
): 'success' | 'default' | 'warning' | 'error' {
  switch (kind) {
    case 'active':
      return 'success';
    case 'maintenance':
      return 'warning';
    case 'decommissioned':
      return 'error';
    default:
      return 'default';
  }
}

export function registerShiftOccupancyTagColor(
  occupancy: RegisterShiftOccupancy
): 'success' | 'error' | 'warning' | 'default' {
  switch (occupancy) {
    case 'open':
      return 'success';
    case 'closed':
      return 'error';
    case 'none':
      return 'warning';
    default:
      return 'default';
  }
}
