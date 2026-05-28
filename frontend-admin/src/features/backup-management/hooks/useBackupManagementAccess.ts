"use client";

import { useMemo } from "react";
import { useAuth } from "@/features/auth/hooks/useAuth";
import { isSuperAdmin } from "@/features/auth/constants/roles";
import { hasPermission, PERMISSIONS } from "@/shared/auth/permissions";

/** Backup panel capability matrix (permissions-first; SuperAdmin for platform scope). */
export function useBackupManagementAccess() {
  const { user } = useAuth();

  return useMemo(() => {
    const canView = hasPermission(user, PERMISSIONS.SETTINGS_VIEW);
    const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
    const canViewAudit = hasPermission(user, PERMISSIONS.AUDIT_VIEW);
    const superAdmin = isSuperAdmin(user?.role);

    return {
      canView,
      canManage,
      canViewAudit,
      isReadOnly: canView && !canManage,
      isSuperAdmin: superAdmin,
      /** Manual backup trigger + restore drill enqueue. */
      canTriggerBackup: canManage,
      /** Cron, retention, execution mode. */
      canEditConfiguration: canManage,
      /** Platform operator: same deployment scope until per-tenant backup runs exist. */
      canViewAllTenantsScope: superAdmin,
    };
  }, [user]);
}
