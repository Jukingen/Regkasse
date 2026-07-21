'use client';

import { Alert, Tag } from 'antd';
import { useSearchParams } from 'next/navigation';
import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { useGetApiAdminBackupStatusLatest } from '@/api/generated/admin-backup/admin-backup';
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupDrDashboard } from '@/features/backup-dr/components/BackupDrDashboard';
import { BackupActivityLogPanel } from '@/features/backup-management/components/BackupActivityLogPanel';
import { BackupConfigurationTab } from '@/features/backup-management/components/BackupConfigurationTab';
import { useBackupManagementAccess } from '@/features/backup-management/hooks/useBackupManagementAccess';
import { BackupDetailModal } from '@/features/backup/components/BackupDetailModal';
import { useI18n } from '@/i18n';
import { resolveBackupTabFromSearch } from '@/shared/backupAreaRoutes';

export type BackupManagementTabKey = 'operations' | 'monitoring' | 'configuration' | 'log';

export interface BackupManagementPanelProps {
  defaultTab?: BackupManagementTabKey;
}

export function BackupManagementPanel({ defaultTab = 'operations' }: BackupManagementPanelProps) {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const access = useBackupManagementAccess();
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);

  const activeTab = useMemo(
    () => resolveBackupTabFromSearch(searchParams?.toString()),
    [searchParams]
  );

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

  const roleTag = access.isSuperAdmin
    ? t('backupDr.management.role.superAdmin')
    : access.canManageBackup
      ? t('backupDr.management.role.tenantAdmin')
      : t('backupDr.management.role.readOnly');

  const tabContent = useMemo(() => {
    switch (activeTab) {
      case 'configuration':
        return <BackupConfigurationTab />;
      case 'log':
        return <BackupActivityLogPanel />;
      case 'monitoring':
      case 'operations':
      default:
        return (
          <BackupDrDashboard embedded hideScheduleSettings onSelectBackupRun={openRunDetail} />
        );
    }
  }, [activeTab, openRunDetail]);

  return (
    <>
      <Alert
        type={access.isReadOnly ? 'info' : 'success'}
        showIcon
        style={{ marginBottom: 8 }}
        title={
          <>
            {t('backupDr.management.accessBanner', { role: roleTag })} <Tag>{roleTag}</Tag>
          </>
        }
        description={
          access.isReadOnly
            ? t('backupDr.management.accessReadOnlyHint')
            : access.isSuperAdmin
              ? t('backupDr.management.accessSuperAdminHint')
              : t('backupDr.management.accessManageHint')
        }
      />

      {tabContent}

      <BackupDetailModal runId={selectedRunId} open={detailModalOpen} onClose={closeRunDetail} />
    </>
  );
}
