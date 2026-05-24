import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { usePermissions } from '@/shared/auth/usePermissions';
import { AppPermissions } from '@/shared/auth/permissions';

const mockUseAuth = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
    useAuth: () => mockUseAuth(),
}));

describe('usePermissions cash register helpers', () => {
    beforeEach(() => {
        mockUseAuth.mockReset();
    });

    it('grants view/manage/decommission for Manager matrix', () => {
        mockUseAuth.mockReturnValue({
            user: {
                role: 'Manager',
                permissions: [
                    AppPermissions.CashRegisterView,
                    AppPermissions.CashRegisterManage,
                    AppPermissions.CashRegisterDecommission,
                ],
            },
        });

        const { result } = renderHook(() => usePermissions());

        expect(result.current.canViewCashRegisters).toBe(true);
        expect(result.current.canManageCashRegisters).toBe(true);
        expect(result.current.canDecommissionCashRegisters).toBe(true);
    });

    it('grants view only for Cashier and Accountant', () => {
        mockUseAuth.mockReturnValue({
            user: {
                role: 'Cashier',
                permissions: [AppPermissions.CashRegisterView],
            },
        });

        const { result } = renderHook(() => usePermissions());

        expect(result.current.canViewCashRegisters).toBe(true);
        expect(result.current.canManageCashRegisters).toBe(false);
        expect(result.current.canDecommissionCashRegisters).toBe(false);
    });
});
