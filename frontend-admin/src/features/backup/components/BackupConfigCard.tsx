'use client';

/**
 * Schedule / retention / health snapshot for the `/backup` overview.
 */
import { SettingOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Skeleton, Space, Typography } from 'antd';
import Link from 'next/link';
import React from 'react';

import {
  getBackupScheduleSettings,
  getBackupScheduleSettingsQueryKey,
  getBackupScheduleStatus,
  getBackupScheduleStatusQueryKey,
} from '@/features/backup-dr/logic/backupScheduleSettingsApi';
import { ConfigurationHealthCard } from '@/features/backup/components/ConfigurationHealthCard';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import {
  BACKUP_CONFIGURATION_PATH,
  BACKUP_SCHEDULE_SETTINGS_HREF,
} from '@/shared/backupAreaRoutes';

export function BackupConfigCard() {
  const { t, formatLocale } = useI18n();
  const { canManageBackup, canConfigure } = useBackupPermissions();

  const settingsQuery = useQuery({
    queryKey: getBackupScheduleSettingsQueryKey(),
    queryFn: getBackupScheduleSettings,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
    enabled: canManageBackup || canConfigure,
  });

  const statusQuery = useQuery({
    queryKey: getBackupScheduleStatusQueryKey(),
    queryFn: getBackupScheduleStatus,
    staleTime: 20_000,
    refetchOnWindowFocus: true,
    enabled: canManageBackup || canConfigure,
  });

  const nextAt =
    statusQuery.data?.computedNextRunAtUtc ?? statusQuery.data?.storedNextRunAtUtc ?? null;

  return (
    <Space orientation="vertical" size={12} style={{ width: '100%' }}>
      <Card
        size="small"
        title={t('backupDr.overview.configCard.title')}
        extra={
          <Link href={BACKUP_SCHEDULE_SETTINGS_HREF} prefetch={false}>
            <Button type="link" size="small" icon={<SettingOutlined />}>
              {t('backupDr.overview.configCard.openSchedule')}
            </Button>
          </Link>
        }
      >
        {!canManageBackup && !canConfigure ? (
          <Alert type="info" showIcon title={t('backupDr.overview.configCard.readOnlyHint')} />
        ) : settingsQuery.isLoading && !settingsQuery.data ? (
          <Skeleton active paragraph={{ rows: 3 }} />
        ) : settingsQuery.isError ? (
          <Alert type="error" showIcon title={t('backupDr.errors.loadFailed')} />
        ) : (
          <Descriptions size="small" column={{ xs: 1, sm: 2 }} bordered>
            <Descriptions.Item label={t('backupDr.overview.configCard.enabled')}>
              {settingsQuery.data?.enabled
                ? t('backupDr.overview.configCard.enabledYes')
                : t('backupDr.overview.configCard.enabledNo')}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.overview.configCard.cron')}>
              <Typography.Text code>
                {settingsQuery.data?.scheduleCron ?? '0 2 * * *'}
              </Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.overview.configCard.retention')}>
              {t('backupDr.overview.configCard.retentionDays', {
                days: settingsQuery.data?.retentionDays ?? 30,
              })}
            </Descriptions.Item>
            <Descriptions.Item label={t('backupDr.monitoring.metrics.nextBackup')}>
              {nextAt ? formatDateTime(nextAt, formatLocale) : '—'}
            </Descriptions.Item>
          </Descriptions>
        )}
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          {t('backupDr.overview.configCard.hint')}{' '}
          <Link href={BACKUP_CONFIGURATION_PATH} prefetch={false}>
            {t('backupDr.overview.configCard.openConfiguration')}
          </Link>
        </Typography.Paragraph>
      </Card>

      <ConfigurationHealthCard canManage={canManageBackup || canConfigure} />
    </Space>
  );
}
