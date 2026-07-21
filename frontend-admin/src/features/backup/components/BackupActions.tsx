'use client';

/**
 * Primary actions for the `/backup` overview hub.
 */
import {
  AuditOutlined,
  DashboardOutlined,
  DatabaseOutlined,
  HistoryOutlined,
  LineChartOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import { Button, Card, Space } from 'antd';
import Link from 'next/link';
import React from 'react';

import { TriggerBackupButton } from '@/features/backup/components/TriggerBackupButton';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { useI18n } from '@/i18n';
import {
  BACKUP_AUDIT_PATH,
  BACKUP_CONFIGURATION_PATH,
  BACKUP_DASHBOARD_PATH,
  BACKUP_PERFORMANCE_PATH,
  BACKUP_RESTORE_HISTORY_PATH,
  BACKUP_RUNS_PATH,
} from '@/shared/backupAreaRoutes';

export function BackupActions() {
  const { t } = useI18n();
  const { canManageBackup, canRestore } = useBackupPermissions();

  return (
    <Card size="small" title={t('backupDr.overview.actions.title')}>
      <Space wrap size="middle">
        <TriggerBackupButton canManage={canManageBackup} />
        <Link href={BACKUP_RUNS_PATH} prefetch={false}>
          <Button icon={<DatabaseOutlined />}>{t('nav.backupRuns')}</Button>
        </Link>
        <Link href={BACKUP_PERFORMANCE_PATH} prefetch={false}>
          <Button icon={<LineChartOutlined />}>{t('nav.backupPerformance')}</Button>
        </Link>
        {canRestore ? (
          <Link href={BACKUP_RESTORE_HISTORY_PATH} prefetch={false}>
            <Button icon={<HistoryOutlined />}>{t('nav.backupRestoreHistory')}</Button>
          </Link>
        ) : null}
        <Link href={BACKUP_DASHBOARD_PATH} prefetch={false}>
          <Button icon={<DashboardOutlined />}>
            {t('backupDr.overview.actions.detailedMonitoring')}
          </Button>
        </Link>
        <Link href={BACKUP_CONFIGURATION_PATH} prefetch={false}>
          <Button icon={<SettingOutlined />}>{t('nav.backupConfiguration')}</Button>
        </Link>
        <Link href={BACKUP_AUDIT_PATH} prefetch={false}>
          <Button icon={<AuditOutlined />}>{t('nav.backupAuditLog')}</Button>
        </Link>
      </Space>
    </Card>
  );
}
