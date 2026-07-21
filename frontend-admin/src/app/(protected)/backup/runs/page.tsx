'use client';

import React from 'react';

import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import {
  AdminBackupPage,
  AdminBackupPageHeaderActions,
} from '@/features/backup/pages/AdminBackupPage';
import { BACKUP_RUNS_PATH } from '@/shared/backupAreaRoutes';

export default function BackupRunsPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.adminBackup.pageTitle"
      sectionLabelKey="nav.backupRuns"
      sectionHref={BACKUP_RUNS_PATH}
      subtitleKey="backupDr.adminBackup.pageSubtitle"
      actions={<AdminBackupPageHeaderActions />}
    >
      <AdminBackupPage />
    </BackupPageShell>
  );
}
