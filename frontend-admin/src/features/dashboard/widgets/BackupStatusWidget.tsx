'use client';

import { CloudServerOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Badge, Button, Space, Statistic, Typography } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';

import { BackupStatusBadge } from '@/features/backup/components/BackupStatusBadge';
import {
  getBackupDashboardHealth,
  getBackupDashboardHealthQueryKey,
} from '@/features/backup/logic/backupDashboardHealthApi';
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import { mapBackupDashboardHealth } from '@/features/backup/logic/backupDashboardHealthPresentation';
import type { WidgetShellProps } from '@/features/dashboard/components/WidgetShell';
import { WidgetShell } from '@/features/dashboard/components/WidgetShell';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n/I18nProvider';
import dayjs from '@/lib/dayjs';
import { PERMISSIONS } from '@/shared/auth/permissions';

type Props = Pick<WidgetShellProps, 'title' | 'dragHandleProps' | 'onRefresh'>;

/** Dashboard backup status widget (success rate, last run, staging storage, health). */
export function BackupStatusWidget({ title, dragHandleProps, onRefresh }: Props) {
  const { t } = useI18n();
  const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.SETTINGS_VIEW });

  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const healthQuery = useQuery({
    queryKey: getBackupDashboardHealthQueryKey(),
    queryFn: getBackupDashboardHealth,
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const stats = statsQuery.data;
  const health = healthQuery.data;

  const handleRefresh = () => {
    void statsQuery.refetch();
    void healthQuery.refetch();
    onRefresh?.();
  };

  const vm = useMemo(
    () =>
      mapBackupDashboardHealth({
        healthScore: health?.healthScore ?? stats?.healthScore,
        healthLevel: health?.healthLevel ?? stats?.healthLevel,
        verificationStatus: health?.verificationStatus,
        lastVerificationStatus: stats?.lastVerificationStatus,
        contentValidationStatus: health?.contentValidationStatus,
        contentValidationSummaryStatus: stats?.contentValidationSummary?.status,
        rpoStatus: health?.rpoStatus ?? stats?.rpoStatus,
        rpoHours: health?.rpoHours ?? stats?.rpoHours,
      }),
    [health, stats]
  );

  const healthBadge = useMemo(() => {
    const label =
      vm.healthLevel === 'healthy'
        ? t('backup.dashboard.healthy')
        : vm.healthLevel === 'critical'
          ? t('backup.dashboard.critical')
          : t('backup.dashboard.warning');
    const status =
      vm.healthLevel === 'healthy' ? 'success' : vm.healthLevel === 'critical' ? 'error' : 'warning';
    return <Badge status={status} text={`${vm.healthEmoji} ${label}`} />;
  }, [t, vm.healthEmoji, vm.healthLevel]);

  const rpoLabel = useMemo(() => {
    if (vm.rpoStatus === 'Healthy') return t('backup.dashboard.rpoHealthy');
    if (vm.rpoStatus === 'AtRisk') return t('backup.dashboard.rpoAtRisk');
    if (vm.rpoStatus === 'Critical') return t('backup.dashboard.rpoCritical');
    return t('backup.dashboard.rpoUnknown');
  }, [t, vm.rpoStatus]);

  const verificationLabel = useMemo(() => {
    if (vm.verificationStatus === 'Passed') return t('backup.dashboard.verificationPassed');
    if (vm.verificationStatus === 'Failed') return t('backup.dashboard.verificationFailed');
    return t('backup.dashboard.verificationNone');
  }, [t, vm.verificationStatus]);

  const contentLabel = useMemo(() => {
    if (vm.contentValidationStatus === 'passed') return t('backup.dashboard.contentPassed');
    if (vm.contentValidationStatus === 'failed') return t('backup.dashboard.contentFailed');
    if (vm.contentValidationStatus === 'partial') return t('backup.dashboard.contentPartial');
    if (vm.contentValidationStatus === 'unavailable') return t('backup.dashboard.contentUnavailable');
    return t('backup.dashboard.contentUnknown');
  }, [t, vm.contentValidationStatus]);

  if ((statsQuery.isLoading || healthQuery.isLoading) && !stats && !health) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <Statistic loading value={0} />
      </WidgetShell>
    );
  }

  if (!stats && !health) {
    return (
      <WidgetShell title={title} dragHandleProps={dragHandleProps} onRefresh={handleRefresh}>
        <span>{t('dashboard.backupStatusWidget.load_failed')}</span>
      </WidgetShell>
    );
  }

  const lastBackupAt = stats?.lastBackupAtUtc
    ? dayjs(stats.lastBackupAtUtc).fromNow()
    : t('dashboard.backupStatusWidget.no_backup');

  const storagePercent = stats?.stagingDiskUsedPercent ?? null;
  const storageAlert =
    stats?.stagingDiskAlert === true || (storagePercent != null && storagePercent >= 80);

  return (
    <WidgetShell
      title={title}
      dragHandleProps={dragHandleProps}
      onRefresh={handleRefresh}
      refreshing={statsQuery.isFetching || healthQuery.isFetching}
      extra={<BackupStatusBadge status={stats?.lastBackupStatus} />}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          {healthBadge}
          <Typography.Text strong>
            {t('backup.dashboard.overallHealth')}:{' '}
            {t('backup.dashboard.healthScorePercent', { score: vm.healthScore })}
          </Typography.Text>
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
            gap: 16,
          }}
        >
          <Statistic
            title={t('dashboard.backupStatusWidget.success_rate_30d')}
            value={stats?.successRate30DaysPercent ?? 0}
            suffix="%"
            precision={0}
            loading={statsQuery.isLoading}
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
            loading={statsQuery.isLoading}
          />
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
            gap: 12,
            fontSize: 12,
            color: '#64748b',
          }}
        >
          <div>
            <div>{t('backup.dashboard.rpoStatus')}</div>
            <Typography.Text strong>
              {rpoLabel}
              {vm.rpoHours != null ? ` (${Math.round(vm.rpoHours)}h)` : ''}
            </Typography.Text>
          </div>
          <div>
            <div>{t('backup.dashboard.verificationStatus')}</div>
            <Typography.Text strong>{verificationLabel}</Typography.Text>
          </div>
          <div>
            <div>{t('backup.dashboard.contentValidation')}</div>
            <Typography.Text strong>{contentLabel}</Typography.Text>
          </div>
        </div>

        {storageAlert ? (
          <Alert
            type="warning"
            showIcon
            title={t('dashboard.backupStatusWidget.storage_alert')}
          />
        ) : null}

        {stats?.configurationHealth?.level ? (
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
