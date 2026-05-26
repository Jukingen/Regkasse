import { useMemo } from 'react';

import { useAuth } from '@/contexts/AuthContext';

export interface AdminPermission {
  canViewLicense: boolean;
  canManageCashRegisters: boolean;
  canManageUsers: boolean;
  canViewReports: boolean;
  canManageRksv: boolean;
  canManageTenants: boolean;
}

function hasPermission(granted: string[] | undefined, permission: string): boolean {
  return granted?.includes(permission) === true;
}

export function useAdminPermissions(): AdminPermission {
  const { user } = useAuth();

  return useMemo(() => {
    const role = user?.role;
    const roles = user?.roles ?? [];
    const permissions = user?.permissions ?? [];

    const isSuperAdmin = role === 'SuperAdmin' || roles.includes('SuperAdmin');
    const isManager = role === 'Manager' || roles.includes('Manager');
    const isCashier = role === 'Cashier' || roles.includes('Cashier');

    return {
      canViewLicense:
        isSuperAdmin ||
        isManager ||
        isCashier ||
        hasPermission(permissions, 'settings.view') ||
        hasPermission(permissions, 'settings.manage'),
      canManageCashRegisters:
        isSuperAdmin || isManager || hasPermission(permissions, 'settings.manage'),
      canManageUsers:
        isSuperAdmin ||
        isManager ||
        hasPermission(permissions, 'user.view') ||
        hasPermission(permissions, 'user.manage'),
      canViewReports:
        isSuperAdmin || isManager || hasPermission(permissions, 'report.view'),
      canManageRksv:
        isSuperAdmin ||
        isManager ||
        hasPermission(permissions, 'report.view') ||
        hasPermission(permissions, 'admin_rksv'),
      canManageTenants:
        isSuperAdmin ||
        hasPermission(permissions, 'system.critical') ||
        hasPermission(permissions, 'tenant.manage'),
    };
  }, [user?.permissions, user?.role, user?.roles]);
}
