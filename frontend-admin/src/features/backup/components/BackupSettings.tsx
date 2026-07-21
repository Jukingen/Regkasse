'use client';

/**
 * Backup schedule + optional manual trigger — permission-gated for Manager (backup.manage)
 * and platform operator (settings.manage / system.critical).
 * Backend has no separate backup.execute; trigger and schedule share backup.manage.
 */
import { Card, Space } from 'antd';
import React from 'react';

import { BackupScheduleSettings } from '@/features/backup-dr/components/BackupScheduleSettings';
import { BackupConfigurationForm } from '@/features/backup/components/BackupConfigurationForm';
import { ConfigurationHealthCard } from '@/features/backup/components/ConfigurationHealthCard';
import { TriggerBackupButton } from '@/features/backup/components/TriggerBackupButton';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';

export interface BackupSettingsProps {
  /** When false, manual trigger is omitted (e.g. page header already exposes it). */
  showManualTrigger?: boolean;
  /** Optional Card title; omit when a parent shell (e.g. Collapse) already labels the section. */
  title?: string | null;
}

export function BackupSettings({ showManualTrigger = false, title = null }: BackupSettingsProps) {
  const { canManageBackup, canConfigure, canTrigger } = useBackupPermissions();

  const content = (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <ConfigurationHealthCard canManage={canManageBackup} />
      <BackupScheduleSettings canManage={canManageBackup} />
      {canConfigure ? <BackupConfigurationForm /> : null}
      {showManualTrigger && canTrigger ? <TriggerBackupButton canManage={canTrigger} /> : null}
    </Space>
  );

  if (title) {
    return (
      <Card size="small" title={title}>
        {content}
      </Card>
    );
  }

  return content;
}
