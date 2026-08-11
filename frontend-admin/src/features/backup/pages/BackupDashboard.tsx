'use client';

/**
 * Backup monitoring dashboard — GET /api/admin/backup/dashboard/stats (30s refresh).
 */
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Col, Row, Space, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import React, { useCallback, useMemo, useState } from 'react';

import { useGetApiAdminBackupStatusLatest } from '@/api/generated/admin-backup/admin-backup';
import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { PageSkeleton } from '@/components/Skeleton';
import { buildBackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';
import { formatBackupBytes, formatBackupDurationMs } from '@/features/backup-dr/logic/backupFormat';
import { BackupHistoryChart } from '@/features/backup/components/BackupHistoryChart';
import { ConfigurationHealthCard } from '@/features/backup/components/ConfigurationHealthCard';
import { MetricCard } from '@/features/backup/components/MetricCard';
import { RecentBackupsTable } from '@/features/backup/components/RecentBackupsTable';
import { RestoreReadinessCard } from '@/features/backup/components/RestoreReadinessCard';
import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { getBackupDashboardHealthQueryKey } from '@/features/backup/logic/backupDashboardHealthApi';
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  type BackupDashboardStatsResponseDto,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import {
  buildSyntheticRestoreLatest,
  mapDashboardHistoryToChartRows,
  metricStatusFromStats,
  statsToRecoverabilitySummary,
} from '@/features/backup/logic/backupDashboardStatsMapper';
import { runRestoreDrill } from '@/features/backup/logic/backupDrillApi';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';

function formatDt(iso: string | undefined | null, formatLocale: string): string {
  if (!iso) return '—';
  return formatDateTime(iso, formatLocale);
}

export function BackupDashboard() {
  const { t, formatLocale } = useI18n();
  const notify = useNotify();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { canManageBackup, canRestore } = useBackupPermissions();
  const [drilling, setDrilling] = useState(false);

  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS, refetchOnWindowFocus: true },
  });

  const stats = statsQuery.data;
  const latestFromStatus = statusQuery.data?.latestRun;

  const operatorTruth = useMemo(
    () =>
      buildBackupOperatorTruthModel({
        t,
        health: stats?.configurationHealth,
        healthLv: (stats?.configurationHealth?.level ?? '').toLowerCase(),
        restoreReady: undefined,
        restoreLv: '',
        latest: latestFromStatus,
        detailForPipeline: null,
        verification: undefined,
        restoreLatest: buildSyntheticRestoreLatest(stats ?? {}),
        recoverabilitySummary: statsToRecoverabilitySummary(stats ?? {}),
        restoreCapability: statusQuery.data?.restore,
        externalCopyVariant: 'unknown',
        omitDedicatedSectionIssueDuplicates: true,
        hasStatusPayload: Boolean(stats),
      }),
    [t, stats, latestFromStatus]
  );

  const metrics = useMemo(() => (stats ? metricStatusFromStats(stats) : null), [stats]);

  const chartRows = useMemo(
    () => mapDashboardHistoryToChartRows(stats?.history30Days, formatLocale),
    [stats?.history30Days, formatLocale]
  );

  const navigateToRun = useCallback(
    (runId: string) => {
      router.push(`/backup/dashboard?runId=${encodeURIComponent(runId)}`);
    },
    [router]
  );

  const invalidateAll = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: getBackupDashboardStatsQueryKey() });
    await queryClient.invalidateQueries({ queryKey: getBackupDashboardHealthQueryKey() });
    await queryClient.invalidateQueries({ queryKey: ['/api/admin/backup'] });
  }, [queryClient]);

  const handleRunDrill = useCallback(async () => {
    setDrilling(true);
    notify.info(t('backup.drillRunning'));
    try {
      const result = await runRestoreDrill();
      if (result.success) {
        if (result.newQueuedRunCreated) notify.successKey('backup.drillCompleted');
        else if (result.existingRunReturned) notify.info(t('backup.drillExisting'));
        else notify.successKey('backup.drillCompleted');
      } else {
        notify.error(t('backup.drillFailed'), {
          description: result.errors?.join(' · ') || result.status,
        });
      }
      await invalidateAll();
      await queryClient.invalidateQueries({ queryKey: ['/api/admin/restore-verification'] });
    } catch (err) {
      notify.apiError(err, {
        logContext: 'BackupDashboard.runDrill',
        fallbackKey: 'backup.drillFailed',
      });
    } finally {
      setDrilling(false);
    }
  }, [invalidateAll, notify, queryClient, t]);

  if (statsQuery.isLoading && !stats) {
    return <PageSkeleton widgets={6} />;
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

  const lastBackupLabel = `${operatorTruth.labels.backupStatus(stats?.lastBackupStatus)} · ${formatDt(
    stats?.lastBackupAtUtc,
    formatLocale
  )}`;

  const durationMs =
    stats?.averageSucceededBackupDurationSeconds != null
      ? stats.averageSucceededBackupDurationSeconds * 1000
      : undefined;

  return (
    <DashboardBody
      stats={stats!}
      metrics={metrics}
      lastBackupLabel={lastBackupLabel}
      durationMs={durationMs}
      chartRows={chartRows}
      operatorTruth={operatorTruth}
      formatDt={formatDt}
      formatLocale={formatLocale}
      canManageBackup={canManageBackup}
      canRestore={canRestore}
      drilling={drilling}
      onRunDrill={handleRunDrill}
      statsFetching={statsQuery.isFetching}
      activeBackupHint={
        latestFromStatus?.status === BackupRunStatus.NUMBER_1 ||
        latestFromStatus?.status === BackupRunStatus.NUMBER_2
      }
      navigateToRun={navigateToRun}
      invalidateAll={invalidateAll}
      t={t}
    />
  );
}

function DashboardBody({
  stats,
  metrics,
  lastBackupLabel,
  durationMs,
  chartRows,
  operatorTruth,
  formatDt,
  formatLocale,
  canManageBackup,
  canRestore,
  drilling,
  onRunDrill,
  statsFetching,
  activeBackupHint,
  navigateToRun,
  invalidateAll,
  t,
}: {
  stats: BackupDashboardStatsResponseDto;
  metrics: ReturnType<typeof metricStatusFromStats> | null;
  lastBackupLabel: string;
  durationMs: number | undefined;
  chartRows: ReturnType<typeof mapDashboardHistoryToChartRows>;
  operatorTruth: ReturnType<typeof buildBackupOperatorTruthModel>;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  canManageBackup: boolean;
  canRestore: boolean;
  drilling: boolean;
  onRunDrill: () => void;
  statsFetching: boolean;
  activeBackupHint: boolean;
  navigateToRun: (runId: string) => void;
  invalidateAll: () => Promise<void>;
  t: (key: string, options?: Record<string, string | number>) => string;
}) {
  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      {activeBackupHint ? (
        <Alert type="info" showIcon title={t('backupDr.monitoring.header.activeHint')} />
      ) : null}

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

      {canRestore ? (
        <Space>
          <Button type="primary" loading={drilling} onClick={onRunDrill}>
            {t('backup.runRestoreDrill')}
          </Button>
        </Space>
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backup.dashboard.overallHealth')}
            value={`${stats.healthScore ?? '—'}%`}
            status={
              (stats.healthLevel ?? '').toLowerCase() === 'healthy'
                ? 'success'
                : (stats.healthLevel ?? '').toLowerCase() === 'critical'
                  ? 'error'
                  : 'warning'
            }
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backup.dashboard.verificationStatus')}
            value={
              stats.lastVerificationStatus === 1
                ? t('backup.dashboard.verificationPassed')
                : stats.lastVerificationStatus === 2
                  ? t('backup.dashboard.verificationFailed')
                  : t('backup.dashboard.verificationNone')
            }
            status={
              stats.lastVerificationStatus === 1
                ? 'success'
                : stats.lastVerificationStatus === 2
                  ? 'error'
                  : 'info'
            }
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backup.dashboard.contentValidation')}
            value={(stats.contentValidationSummary?.status ?? 'unknown').toString()}
            status={
              (stats.contentValidationSummary?.status ?? '').toLowerCase() === 'passed'
                ? 'success'
                : (stats.contentValidationSummary?.status ?? '').toLowerCase() === 'failed'
                  ? 'error'
                  : 'warning'
            }
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backup.dashboard.rpoStatus')}
            value={`${stats.rpoStatus ?? '—'}${
              stats.rpoHours != null ? ` (${Math.round(stats.rpoHours)}h)` : ''
            }`}
            status={
              (stats.rpoStatus ?? '').toLowerCase() === 'healthy' ||
              (stats.rpoStatus ?? '').toLowerCase() === 'ok'
                ? 'success'
                : (stats.rpoStatus ?? '').toLowerCase() === 'critical' ||
                    (stats.rpoStatus ?? '').toLowerCase() === 'overdue'
                  ? 'error'
                  : 'warning'
            }
            loading={statsFetching}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.totalRuns')}
            value={String(stats.totalRuns30Days ?? 0)}
            status="info"
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.succeededRuns')}
            value={String(stats.succeededRuns30Days ?? 0)}
            status="success"
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.failedRuns')}
            value={String(stats.failedRuns30Days ?? 0)}
            status={(stats.failedRuns30Days ?? 0) > 0 ? 'error' : 'success'}
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.pendingRuns')}
            value={String(stats.pendingRunsCount ?? 0)}
            status={(stats.pendingRunsCount ?? 0) > 0 ? 'warning' : 'info'}
            loading={statsFetching}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.lastBackup')}
            value={lastBackupLabel}
            status={metrics?.lastBackupStatus}
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.nextBackup')}
            value={formatDt(stats.nextScheduledBackupAtUtc, formatLocale)}
            status={stats.nextScheduledBackupAtUtc ? 'info' : undefined}
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.backupSize')}
            value={formatBackupBytes(stats.backupSizeBytes ?? undefined, t)}
            status={stats.backupSizeBytes ? 'info' : undefined}
            loading={statsFetching}
          />
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('backupDr.monitoring.metrics.duration')}: {formatBackupDurationMs(durationMs, t)}
          </Typography.Text>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.successRate30d')}
            value={metrics?.successRateValue ?? '—'}
            status={metrics?.successMetricStatus}
            trend={stats.successRateTrendVsPrior30DaysPercent ?? undefined}
            trendLabel={t('backupDr.monitoring.metrics.trendVsPriorMonth')}
            loading={statsFetching}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.lastRestoreDrill')}
            value={`${operatorTruth.labels.restoreStatus(stats.latestRestoreDrillStatus)} · ${formatDt(
              stats.lastSuccessfulRestoreDrillAtUtc,
              formatLocale
            )}`}
            status={metrics?.drillStatus}
            loading={statsFetching}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <MetricCard
            title={t('backupDr.monitoring.metrics.stagingDisk')}
            value={stats.stagingDiskUsedPercent != null ? `${stats.stagingDiskUsedPercent}%` : '—'}
            status={stats.stagingDiskAlert ? 'warning' : 'info'}
            loading={statsFetching}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24}>
          <BackupHistoryChart
            chartData={chartRows}
            formatLocale={formatLocale}
            title={t('backupDr.monitoring.charts.history30d')}
            successLabel={t('backupDr.monitoring.charts.legendSuccess')}
            failedLabel={t('backupDr.monitoring.charts.legendFailed')}
            durationLabel={t('backupDr.monitoring.charts.legendDuration')}
            durationSuffix={t('backupDr.monitoring.charts.durationSuffix')}
            onBarClick={navigateToRun}
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <ConfigurationHealthCard canManage={canManageBackup} />
        </Col>
        <Col xs={24} lg={12}>
          <RestoreReadinessCard
            recoverability={statsToRecoverabilitySummary(stats)}
            restoreLatest={buildSyntheticRestoreLatest(stats)}
            averageSucceededBackupDurationSeconds={
              stats.rtoMinutes != null
                ? stats.rtoMinutes * 60
                : stats.averageSucceededBackupDurationSeconds
            }
            restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
            formatDt={formatDt}
            formatLocale={formatLocale}
            canManage={canRestore}
            t={t}
          />
        </Col>
      </Row>

      <RecentBackupsTable
        backupStatusLabel={(s) => operatorTruth.labels.backupStatus(s)}
        formatDt={formatDt}
        formatLocale={formatLocale}
        t={t}
        onRetryInvalidate={invalidateAll}
      />

      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
        {t('backupDr.monitoring.dashboardRefreshHint', {
          seconds: String(BACKUP_DASHBOARD_STATS_POLL_MS / 1000),
        })}
      </Typography.Paragraph>
    </Space>
  );
}
