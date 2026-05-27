import { describe, expect, it } from 'vitest';
import type { CashRegister } from '@/api/generated/model';
import { filterCashRegisters } from '@/features/cash-registers/utils/filterRegisters';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

describe('filterVisible registers', () => {
    it('hides decommissioned by default', () => {
        const rows = [
            { id: '1', status: 2 } as CashRegister,
            { id: '2', status: 5 } as CashRegister,
        ];
        expect(filterCashRegisters(rows, { showDecommissioned: false }).map((r) => r.id)).toEqual(['1']);
    });

    it('shows all when checkbox enabled', () => {
        const rows = [
            { id: '1', status: 1 } as CashRegister,
            { id: '2', status: 5 } as CashRegister,
        ];
        expect(filterCashRegisters(rows, { showDecommissioned: true })).toHaveLength(2);
    });

    it('filters by search across register number and location', () => {
        const rows = [
            { id: '1', registerNumber: 'KASSE-001', location: 'Hauptkasse', status: 1 } as CashRegister,
            { id: '2', registerNumber: 'KASSE-002', location: 'Bar', status: 1 } as CashRegister,
        ];

        expect(
            filterCashRegisters(rows, { search: 'haupt', showDecommissioned: true }).map((r) => r.id),
        ).toEqual(['1']);
        expect(
            filterCashRegisters(rows, { search: '002', showDecommissioned: true }).map((r) => r.id),
        ).toEqual(['2']);
    });

    it('filters by TSE health status', () => {
        const rows = [
            { id: '1', status: 1, tseHealthStatus: 'healthy' } as CashRegister,
            { id: '2', status: 1, tseHealthStatus: 'offline' } as CashRegister,
        ];

        expect(
            filterCashRegisters(rows, {
                tseHealth: 'healthy',
                showDecommissioned: true,
            }).map((r) => r.id),
        ).toEqual(['1']);
    });

    it('filters by status and allows explicit decommissioned selection', () => {
        const rows = [
            { id: '1', status: REGISTER_STATUS.open } as CashRegister,
            { id: '2', status: REGISTER_STATUS.decommissioned } as CashRegister,
        ];

        expect(
            filterCashRegisters(rows, {
                status: REGISTER_STATUS.decommissioned,
                showDecommissioned: false,
            }).map((r) => r.id),
        ).toEqual(['2']);
    });
});
