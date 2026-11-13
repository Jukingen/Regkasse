'use client';

import { useSearchParams } from 'next/navigation';
import React, { useCallback, useEffect, useState } from 'react';

import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupDrDashboard } from '@/features/backup-dr/components/BackupDrDashboard';
import { BackupDetailModal } from '@/features/backup/components/BackupDetailModal';
import { BackupPageShell } from '@/features/backup/components/BackupPageShell';
import { BACKUP_DASHBOARD_PATH } from '@/shared/backupAreaRoutes';

export default function BackupDashboardPage() {
  const searchParams = useSearchParams();
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);

  useEffect(() => {
    const runId = searchParams.get('runId')?.trim();
    if (!runId) return;
    setSelectedRunId(runId);
    setDetailModalOpen(true);
  }, [searchParams]);

  const openRunDetail = useCallback((run: BackupRunResponseDto) => {
    if (!run.id) return;
    setSelectedRunId(run.id);
    setDetailModalOpen(true);
  }, []);

  const closeRunDetail = useCallback(() => {
    setDetailModalOpen(false);
  }, []);

  return (
    <BackupPageShell
      titleKey="backupDr.page.title"
      sectionLabelKey="nav.backupOverview"
      sectionHref={BACKUP_DASHBOARD_PATH}
      subtitleKey="backupDr.management.pageSubtitle"
    >
      <BackupDrDashboard embedded hideScheduleSettings onSelectBackupRun={openRunDetail} />
      <BackupDetailModal runId={selectedRunId} open={detailModalOpen} onClose={closeRunDetail} />
    </BackupPageShell>
  );
}
