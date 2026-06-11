import { beforeEach, describe, expect, it, vi } from 'vitest';

import { FA_QUICK_CASH_REGISTER_STORAGE_KEY } from '@/features/cash-registers/constants/quickSwitch';
import { persistCashRegisterOnTenantSwitch } from '@/features/tenancy/services/persistCashRegisterOnTenantSwitch';

vi.mock('@/features/cash-registers/api/cashRegisters', () => ({
    listAdminCashRegisters: vi.fn(),
}));

import { listAdminCashRegisters } from '@/features/cash-registers/api/cashRegisters';

const mockListAdminCashRegisters = vi.mocked(listAdminCashRegisters);

describe('persistCashRegisterOnTenantSwitch', () => {
    beforeEach(() => {
        sessionStorage.clear();
        mockListAdminCashRegisters.mockReset();
    });

    it('persists the default register before mandant switch', async () => {
        mockListAdminCashRegisters.mockResolvedValue({
            items: [
                {
                    id: 'reg-default',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-001',
                    location: 'Theke',
                    status: 2,
                    isDefaultForTenant: true,
                    isActive: true,
                },
                {
                    id: 'reg-other',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-002',
                    location: 'Bar',
                    status: 1,
                    isDefaultForTenant: false,
                    isActive: true,
                },
            ],
            totalCount: 2,
            page: 1,
            pageSize: 100,
            totalPages: 1,
        });

        await expect(persistCashRegisterOnTenantSwitch('tenant-a')).resolves.toBe('reg-default');
        expect(sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)).toBe('reg-default');
    });

    it('clears selection when multiple registers have no default', async () => {
        sessionStorage.setItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY, 'stale-register');
        mockListAdminCashRegisters.mockResolvedValue({
            items: [
                {
                    id: 'reg-1',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-001',
                    location: 'Theke',
                    status: 2,
                    isActive: true,
                },
                {
                    id: 'reg-2',
                    tenantId: 'tenant-a',
                    registerNumber: 'KASSE-002',
                    location: 'Bar',
                    status: 1,
                    isActive: true,
                },
            ],
            totalCount: 2,
            page: 1,
            pageSize: 100,
            totalPages: 1,
        });

        await expect(persistCashRegisterOnTenantSwitch('tenant-a')).resolves.toBeNull();
        expect(sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)).toBeNull();
    });
});
