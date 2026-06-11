import { describe, expect, it } from 'vitest';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { cashRegisterByTenantQueryKey } from '@/features/cash-registers/api/cashRegisters';

describe('useCashRegisters helpers', () => {
    it('uses a tenant-scoped query key', () => {
        expect(cashRegisterByTenantQueryKey('tenant-a')).toEqual(['cash-registers', 'by-tenant', 'tenant-a']);
    });

    it('derives default register from API rows', () => {
        const registers: AdminCashRegisterListItem[] = [
            {
                id: 'reg-other',
                tenantId: 'tenant-a',
                registerNumber: 'KASSE-002',
                location: 'Bar',
                status: 1,
                isDefaultForTenant: false,
            },
            {
                id: 'reg-default',
                tenantId: 'tenant-a',
                registerNumber: 'KASSE-001',
                location: 'Theke',
                status: 2,
                isDefaultForTenant: true,
            },
        ];

        const defaultRegister =
            registers.find((row) => row.isDefaultForTenant === true) ?? registers[0] ?? null;

        expect(defaultRegister?.id).toBe('reg-default');
    });
});
