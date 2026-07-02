"use client";

import { useMemo } from "react";
import { useAuth } from "@/features/auth/hooks/useAuth";
import { isSuperAdmin } from "@/features/auth/constants/roles";
import { hasAnyPermission, hasPermission, PERMISSIONS } from "@/shared/auth/permissions";

/** Backup panel capability matrix (permissions-first; SuperAdmin for platform scope). */
export function useBackupManagementAccess() {
  const { user } = useAuth();

  return useMemo(() => {
    const canView = hasPermission(user, PERMISSIONS.SETTINGS_VIEW);
    /** Tenant-scoped backup ops (trigger + schedule); Manager via backup.manage. */
    const canManageBackup = hasAnyPermission(user, [
      PERMISSIONS.SETTINGS_MANAGE,
      PERMISSIONS.BACKUP_MANAGE,
    ]);
    /** Instance-wide settings (execution mode) — settings.manage only. */
    const canManage = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
    const canViewAudit = hasPermission(user, PERMISSIONS.AUDIT_VIEW);
    const superAdmin = isSuperAdmin(user?.role);

    return {
      canView,
      canManage,
      canManageBackup,
      canViewAudit,
      isReadOnly: canView && !canManageBackup,
      isSuperAdmin: superAdmin,
      /** Manual backup trigger + schedule. */
      canTriggerBackup: canManageBackup,
      /** Cron, retention schedule (tenant-scoped). */
      canEditConfiguration: canManageBackup,
      /** Execution mode / deployment paths — instance-wide, settings.manage only. */
      canEditExecutionMode: canManage,
      /** Platform operator: same deployment scope until per-tenant backup runs exist. */
      canViewAllTenantsScope: superAdmin,
    };
  }, [user]);
}
