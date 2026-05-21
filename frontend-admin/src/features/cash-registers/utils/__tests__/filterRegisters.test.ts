import { describe, expect, it } from 'vitest';
import type { CashRegister } from '@/api/generated/model';
import { isDecommissionedRegister, rawRegisterStatus } from '@/features/cash-registers/utils/registerStatus';

function filterVisible(registers: CashRegister[], showDecommissioned: boolean): CashRegister[] {
    if (showDecommissioned) return registers;
    return registers.filter((r) => !isDecommissionedRegister(rawRegisterStatus(r)));
}

describe('filterVisible registers', () => {
    it('hides decommissioned by default', () => {
        const rows = [
            { id: '1', status: 2 } as CashRegister,
            { id: '2', status: 5 } as CashRegister,
        ];
        expect(filterVisible(rows, false).map((r) => r.id)).toEqual(['1']);
    });

    it('shows all when checkbox enabled', () => {
        const rows = [
            { id: '1', status: 1 } as CashRegister,
            { id: '2', status: 5 } as CashRegister,
        ];
        expect(filterVisible(rows, true)).toHaveLength(2);
    });
});
