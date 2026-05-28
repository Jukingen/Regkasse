"use client";

/**
 * Backup UI permission matrix — settings.view / settings.manage + SuperAdmin restore.
 */

import { useMemo } from "react";
import { useAuth } from "@/features/auth/hooks/useAuth";
import { isSuperAdmin } from "@/features/auth/constants/roles";
import { hasPermission, PERMISSIONS } from "@/shared/auth/permissions";

export function useBackupPermissions() {
  const { user } = useAuth();

  return useMemo(() => {
    const canView = hasPermission(user, PERMISSIONS.SETTINGS_VIEW);
    const canConfigure = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
    const canTrigger = canConfigure;
    const superAdmin = isSuperAdmin(user?.role);
    /** Restore drills / high-risk restore surfaces — platform operator only. */
    const canRestore = superAdmin;

    return {
      canView,
      canConfigure,
      canTrigger,
      canRestore,
      isReadOnly: canView && !canConfigure,
      isSuperAdmin: superAdmin,
      /** Super Admin may narrow list by tenant idempotency key prefix (deployment-wide DB). */
      canFilterRunsByTenant: superAdmin,
      /** Execution mode + deployment path hints. */
      canEditExecutionMode: superAdmin && canConfigure,
    };
  }, [user]);
}
