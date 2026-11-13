'use client';

import React from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { PitrRestore } from '@/features/backup/PitrRestore';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { WalStatusPanel } from '@/features/backup/components/WalStatusPanel';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { BACKUP_PITR_PATH } from '@/shared/backupAreaRoutes';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export default function BackupPitrPage() {
  const { user } = useAuth();
  const { isSuperAdmin } = useBackupPermissions();
  const showWalStatus = isSuperAdmin && hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

  return (
    <BackupPageShell
      titleKey="backupDr.pitrPage.pageTitle"
      sectionLabelKey="nav.backupPitr"
      sectionHref={BACKUP_PITR_PATH}
      subtitleKey="backupDr.pitrPage.pageSubtitle"
    >
      {showWalStatus ? <WalStatusPanel /> : null}
      <PitrRestore />
    </BackupPageShell>
  );
}
