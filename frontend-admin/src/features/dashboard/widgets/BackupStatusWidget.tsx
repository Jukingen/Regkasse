'use client';

import { CloudServerOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Space, Statistic, Typography } from 'antd';
import dayjs from 'dayjs';
import 'dayjs/locale/de';
import 'dayjs/locale/en';
import 'dayjs/locale/tr';
import relativeTime from 'dayjs/plugin/relativeTime';
import Link from 'next/link';
import React from 'react';

import { BackupStatusBadge } from '@/features/backup/components/BackupStatusBadge';
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import { PERMISSIONS } from '@/shared/auth/permissions';

dayjs.extend(relativeTime);

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

function dayjsLocale(code: string): string {
  if (code.startsWith('tr')) return 'tr';
  if (code.startsWith('en')) return 'en';
  return 'de';
}

/** Dashboard backup status widget (success rate, last run, staging storage). */
export function BackupStatusWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t, textLocale } = useI18n();
  const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.SETTINGS_VIEW });

  const query = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const stats = query.data;
  const relativeLocale = dayjsLocale(textLocale);

  const handleRefresh = () => {
    void query.refetch();
    onRefresh?.();
  };

  if (query.isLoading && !stats) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <Statistic loading value={0} />
      </WidgetShell>
    );
  }

  if (!stats) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <span>{t('dashboard.backupStatusWidget.load_failed')}</span>
      </WidgetShell>
    );
  }

  const lastBackupAt = stats.lastBackupAtUtc
    ? dayjs(stats.lastBackupAtUtc).locale(relativeLocale).fromNow()
    : t('dashboard.backupStatusWidget.no_backup');

  const storagePercent = stats.stagingDiskUsedPercent ?? null;
  const storageAlert =
    stats.stagingDiskAlert === true || (storagePercent != null && storagePercent >= 80);

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={query.isFetching}
      extra={<BackupStatusBadge status={stats.lastBackupStatus} />}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
            gap: 16,
          }}
        >
          <Statistic
            title={t('dashboard.backupStatusWidget.success_rate_30d')}
            value={stats.successRate30DaysPercent ?? 0}
            suffix="%"
            precision={0}
            loading={query.isLoading}
          />
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 14 }}>
              {t('dashboard.backupStatusWidget.last_backup')}
            </Typography.Text>
            <div style={{ fontSize: 20, fontWeight: 600, marginTop: 4 }}>{lastBackupAt}</div>
          </div>
          <Statistic
            title={t('dashboard.backupStatusWidget.storage')}
            value={storagePercent ?? 0}
            suffix="%"
            precision={0}
            loading={query.isLoading}
          />
        </div>

        {storageAlert ? (
          <Alert
            type="warning"
            showIcon
            message={t('dashboard.backupStatusWidget.storage_alert')}
          />
        ) : null}

        {stats.configurationHealth?.level ? (
          <div style={{ fontSize: 12, color: '#64748b' }}>
            {t('dashboard.backupStatusWidget.config_health', {
              level: stats.configurationHealth.level,
            })}
          </div>
        ) : null}

        <Link href="/backup" style={{ display: 'block' }}>
          <Button type="primary" icon={<CloudServerOutlined />} block>
            {t('dashboard.backupStatusWidget.view_details')}
          </Button>
        </Link>
      </Space>
    </WidgetShell>
  );
}

/** Alias matching Phase 1 plan name (`BackupWidget`). */
export { BackupStatusWidget as BackupWidget };
