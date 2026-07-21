'use client';

import React from 'react';

import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BackupPerformanceDashboard } from '@/features/backup/components/BackupPerformanceDashboard';
import { BACKUP_PERFORMANCE_PATH } from '@/shared/backupAreaRoutes';

export default function BackupPerformancePage() {
  return (
    <BackupPageShell
      titleKey="backupDr.performance.pageTitle"
      sectionLabelKey="nav.backupPerformance"
      sectionHref={BACKUP_PERFORMANCE_PATH}
      subtitleKey="backupDr.performance.pageSubtitle"
    >
      <BackupPerformanceDashboard />
    </BackupPageShell>
  );
}
