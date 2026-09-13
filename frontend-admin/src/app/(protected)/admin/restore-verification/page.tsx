'use client';

import React from 'react';

import { RestoreVerificationList } from '@/features/backup/RestoreVerificationList';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { ADMIN_RESTORE_VERIFICATION_PATH } from '@/shared/backupAreaRoutes';

export default function AdminRestoreVerificationPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.restoreVerificationPage.pageTitle"
      sectionLabelKey="nav.backupRestoreVerification"
      sectionHref={ADMIN_RESTORE_VERIFICATION_PATH}
      subtitleKey="backupDr.restoreVerificationPage.pageSubtitle"
    >
      <RestoreVerificationList />
    </BackupPageShell>
  );
}
