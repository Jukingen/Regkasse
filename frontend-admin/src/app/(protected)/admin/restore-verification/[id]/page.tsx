'use client';

import { useParams } from 'next/navigation';
import React from 'react';

import { RestoreVerificationDetail } from '@/features/backup/RestoreVerificationDetail';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { ADMIN_RESTORE_VERIFICATION_PATH } from '@/shared/backupAreaRoutes';

export default function AdminRestoreVerificationDetailPage() {
  const params = useParams();
  const id = typeof params?.id === 'string' ? params.id : '';

  return (
    <BackupPageShell
      titleKey="backupDr.restoreVerificationPage.detailTitle"
      sectionLabelKey="nav.backupRestoreVerification"
      sectionHref={ADMIN_RESTORE_VERIFICATION_PATH}
      subtitleKey="backupDr.restoreVerificationPage.pageSubtitle"
    >
      {id ? <RestoreVerificationDetail runId={id} /> : null}
    </BackupPageShell>
  );
}
