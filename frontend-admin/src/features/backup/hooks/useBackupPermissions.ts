'use client';

/**
 * Backup UI permission matrix — settings.view / settings.manage + SuperAdmin restore.
 */
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { PERMISSIONS, hasAnyPermission, hasPermission } from '@/shared/auth/permissions';

export function useBackupPermissions() {
  const { user } = useAuth();

  return useMemo(() => {
    const canView = hasPermission(user, PERMISSIONS.SETTINGS_VIEW);
    /**
     * Tenant-scoped backup ops (manual trigger + schedule/automation settings).
     * Manager holds backup.manage; settings.manage (SuperAdmin) also satisfies it.
     */
    const canManageBackup = hasAnyPermission(user, [
      PERMISSIONS.SETTINGS_MANAGE,
      PERMISSIONS.BACKUP_MANAGE,
    ]);
    /** Instance-wide settings (execution mode, deployment paths) — settings.manage only. */
    const canConfigure = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
    const canTrigger = canManageBackup;
    /** Download own-tenant backup artifacts (dump + manifest). */
    const canDownloadBackup = canManageBackup;
    const superAdmin = isSuperAdmin(user?.role);
    /** Restore drills / high-risk restore surfaces — platform operator only. */
    const canRestore = superAdmin;

    return {
      canView,
      /** Trigger backups + edit backup schedule (own tenant for Manager). */
      canManageBackup,
      /** Platform-wide backup settings (execution mode, deployment paths). */
      canConfigure,
      /** Alias for {@link canConfigure} — Super Admin / platform operator surfaces. */
      isPlatformAdmin: canConfigure,
      canTrigger,
      canDownloadBackup,
      canRestore,
      isReadOnly: canView && !canManageBackup,
      isSuperAdmin: superAdmin,
      /** Super Admin may narrow list by tenant idempotency key prefix (deployment-wide DB). */
      canFilterRunsByTenant: superAdmin,
      /** Execution mode + deployment path hints. */
      canEditExecutionMode: superAdmin && canConfigure,
    };
  }, [user]);
}
