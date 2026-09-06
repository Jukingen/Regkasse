'use client';

import React from 'react';

import { BackupRetentionSettings } from '@/features/backup/BackupRetentionSettings';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_RETENTION_PATH } from '@/shared/backupAreaRoutes';

export default function BackupRetentionPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.retention.pageTitle"
      sectionLabelKey="nav.backupRetention"
      sectionHref={BACKUP_RETENTION_PATH}
      subtitleKey="backupDr.retention.pageSubtitle"
    >
      <BackupRetentionSettings />
    </BackupPageShell>
  );
}
