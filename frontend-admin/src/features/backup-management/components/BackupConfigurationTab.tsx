'use client';

import { Alert, Space } from 'antd';
import React from 'react';

import { BackupScheduleSettings } from '@/features/backup-dr/components/BackupScheduleSettings';
import { useBackupManagementAccess } from '@/features/backup-management/hooks/useBackupManagementAccess';
import { BackupConfigurationForm } from '@/features/backup/components/BackupConfigurationForm';
import { ConfigurationHealthCard } from '@/features/backup/components/ConfigurationHealthCard';
import { useI18n } from '@/i18n';

export function BackupConfigurationTab() {
  const { t } = useI18n();
  const access = useBackupManagementAccess();

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      {access.isReadOnly ? (
        <Alert type="info" showIcon title={t('backupDr.permission.noManage')} />
      ) : null}
      {access.isSuperAdmin ? (
        <Alert type="info" showIcon title={t('backupDr.management.scope.deploymentWide')} />
      ) : null}
      {access.canEditConfiguration ? (
        <BackupScheduleSettings canManage={access.canManageBackup} />
      ) : null}
      {access.canEditExecutionMode ? <BackupConfigurationForm /> : null}
      <ConfigurationHealthCard canManage={access.canManageBackup} poll={false} />
    </Space>
  );
}
