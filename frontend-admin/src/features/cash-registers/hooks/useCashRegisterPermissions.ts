import { useMemo } from 'react';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { isDecommissionedRegister } from '@/features/cash-registers/utils/registerStatus';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

export type CashRegisterPermissionFlags = {
  canView: boolean;
  canEdit: boolean;
  /** Reserve a register for one cashier (`POST /api/admin/cash-registers/{id}/assign`). */
  canAssignUser: boolean;
  canOpen: boolean;
  canClose: boolean;
  canManageShifts: boolean;
  canViewReports: boolean;
  canExport: boolean;
  isDecommissioned: boolean;
};

export type ResolveCashRegisterPermissionsInput = {
  isSuperAdmin: boolean;
  userTenantId?: string | null;
  registerTenantId?: string | null;
  hasCashRegisterView: boolean;
  hasCashRegisterManage: boolean;
  hasReportView: boolean;
  hasReportExport: boolean;
  isDecommissioned: boolean;
  /** When true, tenant mismatch cannot be evaluated yet (no register loaded). */
  registerLoaded: boolean;
};

const DENIED: CashRegisterPermissionFlags = {
  canView: false,
  canEdit: false,
  canAssignUser: false,
  canOpen: false,
  canClose: false,
  canManageShifts: false,
  canViewReports: false,
  canExport: false,
  isDecommissioned: false,
};

function sameTenant(left?: string | null, right?: string | null): boolean {
  const a = left?.trim();
  const b = right?.trim();
  return Boolean(a && b && a.toLowerCase() === b.toLowerCase());
}

export function resolveCashRegisterPermissions(
  input: ResolveCashRegisterPermissionsInput
): CashRegisterPermissionFlags {
  const decommissioned = input.isDecommissioned;

  if (input.isSuperAdmin) {
    return {
      canView: true,
      canEdit: true,
      canAssignUser: true,
      canOpen: true,
      canClose: true,
      canManageShifts: true,
      canViewReports: true,
      canExport: true,
      isDecommissioned: decommissioned,
    };
  }

  if (input.registerLoaded && !sameTenant(input.registerTenantId, input.userTenantId)) {
    return { ...DENIED, isDecommissioned: decommissioned };
  }

  const canView = input.hasCashRegisterView;
  const canManage = input.hasCashRegisterManage && canView;

  return {
    canView,
    canEdit: canManage,
    canAssignUser: canManage,
    canOpen: canManage,
    canClose: canManage,
    canManageShifts: canManage,
    canViewReports: input.hasReportView,
    canExport: input.hasReportExport,
    isDecommissioned: decommissioned,
  };
}

export function useCashRegisterPermissions(
  register?: Pick<AdminCashRegisterListItem, 'tenantId' | 'status'> | null
): CashRegisterPermissionFlags {
  const {
    user,
    isSuperAdmin,
    hasPermission,
    canViewCashRegisters,
    canManageCashRegisters,
  } = usePermissions();

  return useMemo(
    () =>
      resolveCashRegisterPermissions({
        isSuperAdmin,
        userTenantId: user?.tenantId,
        registerTenantId: register?.tenantId,
        hasCashRegisterView: canViewCashRegisters,
        hasCashRegisterManage: canManageCashRegisters,
        hasReportView: hasPermission(PERMISSIONS.REPORT_VIEW),
        hasReportExport: hasPermission(PERMISSIONS.REPORT_EXPORT),
        isDecommissioned: isDecommissionedRegister(register?.status),
        registerLoaded: Boolean(register),
      }),
    [
      canManageCashRegisters,
      canViewCashRegisters,
      hasPermission,
      isSuperAdmin,
      register,
      user?.tenantId,
    ]
  );
}
