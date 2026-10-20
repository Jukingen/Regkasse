'use client';

import React from 'react';

import { RestoreDrillBoard } from '@/features/backup/components/RestoreDrillBoard';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_DRILLS_PATH } from '@/shared/backupAreaRoutes';

export default function BackupDrillsPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.drills.pageTitle"
      sectionLabelKey="nav.backupDrills"
      sectionHref={BACKUP_DRILLS_PATH}
      subtitleKey="backupDr.drills.pageSubtitle"
    >
      <RestoreDrillBoard />
    </BackupPageShell>
  );
}
