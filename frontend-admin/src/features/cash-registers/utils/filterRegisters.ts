import type { CashRegister } from '@/api/generated/model';
import { isDecommissionedRegister, rawRegisterStatus, REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

export type CashRegisterStatusFilter = number | undefined;

export type FilterCashRegistersOptions = {
    search?: string;
    status?: CashRegisterStatusFilter;
    showDecommissioned: boolean;
};

export function filterCashRegisters(
    registers: CashRegister[],
    options: FilterCashRegistersOptions,
): CashRegister[] {
    const normalizedSearch = options.search?.trim().toLowerCase() ?? '';

    return registers.filter((register) => {
        const status = rawRegisterStatus(register);
        const matchesStatus = options.status == null || status === options.status;
        const decommissionedAllowed =
            options.showDecommissioned || options.status === REGISTER_STATUS.decommissioned;
        const matchesDecommissioned = decommissionedAllowed || !isDecommissionedRegister(status);

        if (!matchesStatus || !matchesDecommissioned) {
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
