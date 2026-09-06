'use client';

import React from 'react';

import { PitrRestore } from '@/features/backup/PitrRestore';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_PITR_PATH } from '@/shared/backupAreaRoutes';

export default function BackupPitrPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.pitrPage.pageTitle"
      sectionLabelKey="nav.backupPitr"
      sectionHref={BACKUP_PITR_PATH}
      subtitleKey="backupDr.pitrPage.pageSubtitle"
    >
      <PitrRestore />
    </BackupPageShell>
  );
}
