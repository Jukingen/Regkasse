import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { usePermissions } from '@/hooks/usePermissions';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

const mockUseAuth = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

describe('usePermissions', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
  });

  it('grants all checks for SuperAdmin regardless of permission list', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: [] },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasPermission(PERMISSIONS.USER_MANAGE)).toBe(true);
    expect(result.current.canViewMenu('/admin/users')).toBe(true);
    expect(result.current.canViewCashRegisters).toBe(true);
  });

  it('denies menu paths when user has no permission claims', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Cashier', permissions: [] },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.canViewMenu('/products')).toBe(false);
    expect(result.current.hasPermission(PERMISSIONS.PRODUCT_VIEW)).toBe(false);
  });

  it('canViewMenu respects route permission map', () => {
    mockUseAuth.mockReturnValue({
      user: {
        role: 'Manager',
        permissions: [PERMISSIONS.REPORT_VIEW, PERMISSIONS.USER_VIEW],
      },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.canViewMenu('/dashboard')).toBe(true);
    expect(result.current.canViewMenu('/admin/reports')).toBe(true);
    expect(result.current.canViewMenu('/admin/users')).toBe(true);
    expect(result.current.canViewMenu('/products')).toBe(false);
  });

  it('hasAnyPermission and hasAllPermissions work for partial grants', () => {
    mockUseAuth.mockReturnValue({
      user: {
        role: 'Cashier',
        permissions: [PERMISSIONS.REPORT_VIEW],
      },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.hasAnyPermission([PERMISSIONS.REPORT_VIEW, PERMISSIONS.USER_VIEW])).toBe(
      true,
    );
    expect(result.current.hasAllPermissions([PERMISSIONS.REPORT_VIEW, PERMISSIONS.USER_VIEW])).toBe(
      false,
    );
  });

  it('treats cash_register.manage as sufficient for canViewCashRegisters', () => {
    mockUseAuth.mockReturnValue({
      user: {
        role: 'Manager',
        permissions: [AppPermissions.CashRegisterManage],
      },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.canViewCashRegisters).toBe(true);
    expect(result.current.canManageCashRegisters).toBe(true);
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

  it('exposes permissions and userPermissions aliases', () => {
    const perms = [PERMISSIONS.PRODUCT_VIEW];
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: perms },
    });

    const { result } = renderHook(() => usePermissions());

    expect(result.current.permissions).toEqual(perms);
    expect(result.current.userPermissions).toEqual(perms);
  });
});
