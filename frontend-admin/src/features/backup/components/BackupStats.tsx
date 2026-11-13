'use client';

/**
 * Compact backup statistics for the `/backup` overview hub.
 */
import { useQuery } from '@tanstack/react-query';
import { Alert, Col, Row } from 'antd';
import React from 'react';

import { SkeletonWrapper } from '@/components/Skeleton';
import { formatBackupBytes } from '@/features/backup-dr/logic/backupFormat';
import { MetricCard } from '@/features/backup/components/MetricCard';
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import { metricStatusFromStats } from '@/features/backup/logic/backupDashboardStatsMapper';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

export function BackupStats() {
  const { t, formatLocale } = useI18n();
  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  if (statsQuery.isLoading && !statsQuery.data) {
    return (
      <SkeletonWrapper type="widget" loading count={4}>
        {null}
      </SkeletonWrapper>
    );
  }

  if (statsQuery.isError) {
    return (
      <Alert
        type="error"
        showIcon
        title={t('backupDr.errors.loadFailed')}
        description={t('backupDr.monitoring.dashboardStatsLoadFailed')}
      />
    );
  }

  const stats = statsQuery.data!;
  const metrics = metricStatusFromStats(stats);
  const lastLabel = stats.lastBackupAtUtc
    ? formatDateTime(stats.lastBackupAtUtc, formatLocale)
    : '—';
  const nextLabel = stats.nextScheduledBackupAtUtc
    ? formatDateTime(stats.nextScheduledBackupAtUtc, formatLocale)
    : '—';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {stats.stagingDiskAlert ? (
        <Alert
          type="warning"
          showIcon
          title={t('backupDr.monitoring.diskAlert.title')}
          description={t('backupDr.monitoring.diskAlert.description', {
            percent: stats.stagingDiskUsedPercent ?? '—',
          })}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.totalRuns')}
            value={String(stats.totalRuns30Days ?? 0)}
            status="info"
            loading={statsQuery.isFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.succeededRuns')}
            value={String(stats.succeededRuns30Days ?? 0)}
            status="success"
            loading={statsQuery.isFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.failedRuns')}
            value={String(stats.failedRuns30Days ?? 0)}
            status={(stats.failedRuns30Days ?? 0) > 0 ? 'error' : 'success'}
            loading={statsQuery.isFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.pendingRuns')}
            value={String(stats.pendingRunsCount ?? 0)}
            status={(stats.pendingRunsCount ?? 0) > 0 ? 'warning' : 'info'}
            loading={statsQuery.isFetching}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.lastBackup')}
            value={lastLabel}
            status={metrics.lastBackupStatus}
            loading={statsQuery.isFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.nextBackup')}
            value={nextLabel}
            status={stats.nextScheduledBackupAtUtc ? 'info' : undefined}
            loading={statsQuery.isFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.backupSize')}
            value={formatBackupBytes(stats.backupSizeBytes ?? undefined, t)}
            status={stats.backupSizeBytes ? 'info' : undefined}
            loading={statsQuery.isFetching}
          />
        </Col>
      </Row>
    </div>
  );
}
