'use client';

import React from 'react';

import { BackupActivityLogPanel } from '@/features/backup-management/components/BackupActivityLogPanel';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_AUDIT_PATH } from '@/shared/backupAreaRoutes';

export default function BackupAuditPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.management.tabs.log"
      sectionLabelKey="nav.backupAuditLog"
      sectionHref={BACKUP_AUDIT_PATH}
      subtitleKey="backupDr.management.log.subtitle"
    >
      <BackupActivityLogPanel />
    </BackupPageShell>
  );
}
