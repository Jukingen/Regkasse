'use client';

import React from 'react';

import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BackupStorageCostsDashboard } from '@/features/backup/components/BackupStorageCostsDashboard';
import { BACKUP_COSTS_PATH } from '@/shared/backupAreaRoutes';

export default function BackupCostsPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.costs.pageTitle"
      sectionLabelKey="nav.backupCosts"
      sectionHref={BACKUP_COSTS_PATH}
      subtitleKey="backupDr.costs.pageSubtitle"
    >
      <BackupStorageCostsDashboard />
    </BackupPageShell>
  );
}
