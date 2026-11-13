import type { CashRegister } from '@/api/generated/model';
import type {
  EnhancedCashRegister,
  TseHealthStatus,
} from '@/features/cash-registers/types/enhancedCashRegister';
import {
  REGISTER_STATUS,
  isDecommissionedRegister,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { normalizeTseHealthStatus } from '@/features/cash-registers/utils/tseHealthStatus';

export type CashRegisterStatusFilter = number | undefined;
export type TseHealthStatusFilter = TseHealthStatus | undefined;

export type FilterCashRegistersOptions = {
  search?: string;
  status?: CashRegisterStatusFilter;
  tseHealth?: TseHealthStatusFilter;
  showDecommissioned: boolean;
};

function asEnhanced(record: CashRegister): EnhancedCashRegister {
  return record as EnhancedCashRegister;
}

export function filterCashRegisters(
  registers: CashRegister[],
  options: FilterCashRegistersOptions
): CashRegister[] {
  const normalizedSearch = options.search?.trim().toLowerCase() ?? '';

  return registers.filter((register) => {
    const status = rawRegisterStatus(register);
    const matchesStatus = options.status == null || status === options.status;
    const matchesTseHealth =
      options.tseHealth == null ||
      normalizeTseHealthStatus(asEnhanced(register).tseHealthStatus) === options.tseHealth;
    const decommissionedAllowed =
      options.showDecommissioned || options.status === REGISTER_STATUS.decommissioned;
    const matchesDecommissioned = decommissionedAllowed || !isDecommissionedRegister(status);

    if (!matchesStatus || !matchesTseHealth || !matchesDecommissioned) {
      return false;
    }

    if (normalizedSearch === '') {
      return true;
    }

    const haystack = [register.registerNumber, register.location]
      .filter((value): value is string => typeof value === 'string' && value.trim() !== '')
      .join(' ')
      .toLowerCase();

    return haystack.includes(normalizedSearch);
  });
}
