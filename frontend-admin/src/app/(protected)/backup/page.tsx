'use client';

/**
 * Role-aware Backup & DR overview hub.
 * Super Admin → system-wide view; Mandanten-Admin → tenant-scoped view.
 * Detailed operator monitoring remains at `/backup/dashboard`.
 */
import React from 'react';

import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { SystemBackupView } from '@/features/backup/components/SystemBackupView';
import { TenantBackupView } from '@/features/backup/components/TenantBackupView';
import { usePermissions } from '@/hooks/usePermissions';
import { BACKUP_HUB_LANDING_PATH } from '@/shared/backupAreaRoutes';

export default function BackupOverviewPage() {
  const { isSuperAdmin } = usePermissions();

  return (
    <BackupPageShell
      titleKey={
        isSuperAdmin
          ? 'backupDr.overview.systemView.pageTitle'
          : 'backupDr.overview.tenantView.pageTitle'
      }
      sectionLabelKey="nav.backupOverview"
      sectionHref={BACKUP_HUB_LANDING_PATH}
      subtitleKey={
        isSuperAdmin
          ? 'backupDr.overview.systemView.pageSubtitle'
          : 'backupDr.overview.tenantView.pageSubtitle'
      }
    >
      {isSuperAdmin ? <SystemBackupView /> : <TenantBackupView />}
    </BackupPageShell>
  );
}
