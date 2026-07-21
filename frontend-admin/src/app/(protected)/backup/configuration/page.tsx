'use client';

import React from 'react';

import { BackupConfigurationTab } from '@/features/backup-management/components/BackupConfigurationTab';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_CONFIGURATION_PATH } from '@/shared/backupAreaRoutes';

export default function BackupConfigurationPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.management.tabs.configuration"
      sectionLabelKey="nav.backupConfiguration"
      sectionHref={BACKUP_CONFIGURATION_PATH}
      subtitleKey="backupDr.management.configurationSubtitle"
    >
      <BackupConfigurationTab />
    </BackupPageShell>
  );
}
