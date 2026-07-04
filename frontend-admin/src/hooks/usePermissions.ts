'use client';

import { useMemo } from 'react';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isManager, isSuperAdmin } from '@/features/auth/constants/roles';
import { isMenuItemAllowed } from '@/shared/auth/menuPermissions';
import {
  AppPermissions,
  hasPermission as checkPermission,
  hasAnyPermission as checkAnyPermission,
  hasAllPermissions as checkAllPermissions,
  type UserWithPermissions,
} from '@/shared/auth/permissions';

export function usePermissions() {
  const { user } = useAuth();
  const userPermissions = useMemo(
    () => user?.permissions ?? [],
    [user?.permissions],
  );

  const userWithPerms: UserWithPermissions | null = useMemo(
    () => (user ? { permissions: userPermissions } : null),
    [user, userPermissions],
  );

  return useMemo(() => {
    const superAdmin = isSuperAdmin(user?.role);
    const manager = isManager(user?.role);

    const hasPermission = (permission: string): boolean => {
      if (superAdmin) return true;
      return checkPermission(userWithPerms, permission);
    };

    const hasAnyPermission = (permissions: string[]): boolean => {
      if (superAdmin) return true;
      return checkAnyPermission(userWithPerms, permissions);
    };

    const hasAllPermissions = (permissions: string[]): boolean => {
      if (superAdmin) return true;
      return checkAllPermissions(userWithPerms, permissions);
    };

    /** Sidebar / menu path visibility — uses full `ROUTE_PERMISSIONS` via `isMenuItemAllowed`. */
    const canViewMenu = (path: string): boolean => {
      if (superAdmin) return true;
      if (userPermissions.length === 0) return false;
      return isMenuItemAllowed(path, userPermissions);
    };

    return {
      user,
      permissions: userPermissions,
      userPermissions,
      hasPermission,
      hasAnyPermission,
      hasAllPermissions,
      canViewMenu,
      isSuperAdmin: superAdmin,
      isManager: manager,
      canViewCashRegisters:
        hasPermission(AppPermissions.CashRegisterView) ||
        hasPermission(AppPermissions.CashRegisterManage),
      canManageCashRegisters: hasPermission(AppPermissions.CashRegisterManage),
      canDecommissionCashRegisters: hasPermission(AppPermissions.CashRegisterDecommission),
    };
  }, [user, userPermissions, userWithPerms]);
}
