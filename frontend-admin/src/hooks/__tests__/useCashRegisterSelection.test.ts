import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';

const mockUseAdminCashRegisterList = vi.fn();
const mockUseCurrentTenant = vi.fn();

vi.mock('@/features/cash-registers/hooks/useAdminCashRegisterList', () => ({
    useAdminCashRegisterList: (opts: unknown) => mockUseAdminCashRegisterList(opts),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
    useCurrentTenant: () => mockUseCurrentTenant(),
}));

const sampleRegisters: AdminCashRegisterListItem[] = [
    {
        id: 'reg-1',
        tenantId: 'tenant-a',
        registerNumber: 'K1',
        location: 'Haupt',
        status: 1,
        isDefaultForTenant: false,
    },
    {
        id: 'reg-2',
        tenantId: 'tenant-a',
        registerNumber: 'K2',
        location: 'Bar',
        status: 1,
        isDefaultForTenant: true,
    },
];

beforeEach(() => {
    sessionStorage.clear();
    mockUseCurrentTenant.mockReturnValue({
        tenantId: 'tenant-a',
        isSuperAdminUser: false,
    });
    mockUseAdminCashRegisterList.mockReturnValue({
        registers: sampleRegisters,
        isLoading: false,
        isFetching: false,
        error: null,
        refetch: vi.fn(),
    });
});

describe('useCashRegisterSelection', () => {
    it('loads manager registers via allowTenantScopedDefault admin list', () => {
        renderHook(() => useCashRegisterSelection());

        expect(mockUseAdminCashRegisterList).toHaveBeenCalledWith(
            expect.objectContaining({
                allowTenantScopedDefault: true,
                allowAllTenants: false,
                enabled: true,
            }),
        );
    });

    it('auto-selects default register when autoSelect is enabled', async () => {
        const onChange = vi.fn();

        const { result } = renderHook(() =>
            useCashRegisterSelection({ autoSelect: true, onChange }),
        );

        await waitFor(() => {
            expect(result.current.selectedRegisterId).toBe('reg-2');
        });
        expect(onChange).toHaveBeenCalledWith('reg-2', expect.objectContaining({ id: 'reg-2' }));
    });

    it('auto-selects first register when autoSelect is enabled and no default is flagged', async () => {
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [
                { ...sampleRegisters[0], isDefaultForTenant: false },
                { ...sampleRegisters[1], isDefaultForTenant: false },
            ],
            isLoading: false,
            isFetching: false,
            error: null,
            refetch: vi.fn(),
        });

        const onChange = vi.fn();
        const { result } = renderHook(() =>
            useCashRegisterSelection({ autoSelect: true, onChange }),
        );

        await waitFor(() => {
            expect(result.current.selectedRegisterId).toBe('reg-1');
        });
        expect(onChange).toHaveBeenCalledWith('reg-1', expect.objectContaining({ id: 'reg-1' }));
    });

    it('exposes hasRegisters when tenant inventory is non-empty', () => {
        const { result } = renderHook(() => useCashRegisterSelection());

        expect(result.current.hasRegisters).toBe(true);
        expect(result.current.hasMultipleRegisters).toBe(true);
        expect(result.current.isSingleRegister).toBe(false);
    });

    it('notifies controlled parent when value starts undefined', async () => {
        const onChange = vi.fn();

        renderHook(() =>
            useCashRegisterSelection({
                value: undefined,
                onChange,
                controlled: true,
                autoSelect: true,
            }),
        );

        await waitFor(() => {
            expect(onChange).toHaveBeenCalledWith('reg-2', expect.objectContaining({ id: 'reg-2' }));
        });
    });

    it('auto-selects sole register when autoSelect is enabled', async () => {
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [sampleRegisters[0]],
            isLoading: false,
            isFetching: false,
            error: null,
            refetch: vi.fn(),
        });

        const { result } = renderHook(() => useCashRegisterSelection({ autoSelect: true }));

        await waitFor(() => {
            expect(result.current.selectedRegisterId).toBe('reg-1');
        });
        expect(result.current.isSingleRegister).toBe(true);
    });

    it('does not auto-select when autoSelect is false', () => {
        const { result } = renderHook(() => useCashRegisterSelection({ autoSelect: false }));

        expect(result.current.selectedRegisterId).toBeUndefined();
        expect(result.current.registerOptions).toHaveLength(2);
    });

    it('auto-selects sole register when autoSelect is false but autoSelectSingle is enabled', async () => {
        mockUseAdminCashRegisterList.mockReturnValue({
            registers: [sampleRegisters[0]],
            isLoading: false,
            isFetching: false,
            error: null,
            refetch: vi.fn(),
        });

        const onChange = vi.fn();
        const { result } = renderHook(() =>
            useCashRegisterSelection({ autoSelect: false, onChange }),
        );

        await waitFor(() => {
            expect(result.current.selectedRegisterId).toBe('reg-1');
        });
        expect(onChange).toHaveBeenCalledWith('reg-1', expect.objectContaining({ id: 'reg-1' }));
    });

    it('restores persisted selection on optional filter pages', async () => {
        sessionStorage.setItem('fa_quick_cash_register_id:tenant-a', 'reg-1');

        const onChange = vi.fn();
        const { result } = renderHook(() =>
            useCashRegisterSelection({
                autoSelect: false,
                persistSelection: true,
                onChange,
            }),
        );

        await waitFor(() => {
            expect(result.current.selectedRegisterId).toBe('reg-1');
        });
        expect(onChange).toHaveBeenCalledWith('reg-1', expect.objectContaining({ id: 'reg-1' }));
    });
});
