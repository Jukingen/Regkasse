"use client";

/**
 * Backup izleme panosu: metrikler, grafikler, yapılandırma sağlığı, RPO/RTO.
 */

import React, { useMemo } from "react";
import { useRouter } from "next/navigation";
import { Col, Row } from "antd";
import { useQuery } from "@tanstack/react-query";
import { useGetApiAdminBackupRuns } from "@/api/generated/admin-backup/admin-backup";
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from "@/features/backup/logic/backupDashboardStatsApi";
import { mapDashboardHistoryToChartRows } from "@/features/backup/logic/backupDashboardStatsMapper";
import type {
  BackupArtifactPipelinePolicyResponseDto,
  BackupConfigurationHealthResponseDto,
  BackupLatestStatusResponseDto,
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  RestoreVerificationReadinessResponseDto,
  RestoreVerificationRunResponseDto,
} from "@/api/generated/model";
import { BackupConfigurationHealthCard } from "@/features/backup-dr/components/BackupConfigurationHealthCard";
import { BackupHistoryChart } from "@/features/backup-dr/components/BackupHistoryChart";
import { BackupMonitoringMetricsRow } from "@/features/backup-dr/components/BackupMonitoringMetricsRow";
import { BackupRestoreReadinessCard } from "@/features/backup-dr/components/BackupRestoreReadinessCard";
import { BackupStatusHeader } from "@/features/backup-dr/components/BackupStatusHeader";
import { BackupDrSection } from "@/features/backup-dr/components/BackupDrSection";
import { RecentRunsTable } from "@/features/backup-dr/components/RecentRunsTable";
import { BACKUP_METRICS_RUNS_PAGE_SIZE } from "@/features/backup-dr/logic/backupDashboardQueryTiming";
import type { ColumnsType } from "antd/es/table";

export interface BackupMonitoringSectionProps {
  statusPayload: BackupLatestStatusResponseDto | undefined;
  latest: BackupRunResponseDto | undefined;
  latestDetail: BackupRunResponseDto | undefined;
  configurationHealth: BackupConfigurationHealthResponseDto | undefined;
  artifactPipelinePolicy?: BackupArtifactPipelinePolicyResponseDto;
  /** Schedule / health edit link — backup.manage (Manager) or settings.manage. */
  canManageBackup?: boolean;
  /** Restore drill trigger — Super Admin only. */
  canRestore?: boolean;
  recoverability: BackupRecoverabilitySummaryResponseDto | undefined;
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  restoreReadiness: RestoreVerificationReadinessResponseDto | undefined;
  simulatedOperationalMode?: boolean;
  backupStatusLabel: (status: number | undefined) => string;
  restoreStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  metricsPollInterval: number | ((query: unknown) => number);
  loading?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupMonitoringSection({
  statusPayload,
  latest,
  latestDetail,
  configurationHealth,
  artifactPipelinePolicy,
  canManageBackup = false,
  canRestore = false,
  recoverability,
  restoreLatest,
  restoreReadiness,
  simulatedOperationalMode,
  backupStatusLabel,
  restoreStatusLabel,
  formatDt,
  formatLocale,
  metricsPollInterval,
  loading,
  t,
}: BackupMonitoringSectionProps) {
  const router = useRouter();

  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const chartRowsFromStats = useMemo(
    () => mapDashboardHistoryToChartRows(statsQuery.data?.history30Days, formatLocale),
    [statsQuery.data?.history30Days, formatLocale],
  );

  const metricsRunsQuery = useGetApiAdminBackupRuns(
    { page: 1, pageSize: BACKUP_METRICS_RUNS_PAGE_SIZE },
    {
      query: {
        refetchInterval: metricsPollInterval,
        refetchOnWindowFocus: true,
      },
    },
  );

  const runsForMetrics = metricsRunsQuery.data?.items ?? [];

  const recentColumns: ColumnsType<BackupRunResponseDto> = useMemo(
    () => [
      {
        title: t("backupDr.monitoring.recentTable.requested"),
        dataIndex: "requestedAt",
        render: (v: string | undefined) => formatDt(v, formatLocale),
      },
      {
        title: t("backupDr.monitoring.recentTable.status"),
        dataIndex: "status",
        render: (s: number | undefined) => backupStatusLabel(s),
      },
      {
        title: t("backupDr.monitoring.recentTable.adapter"),
        dataIndex: "adapterKind",
        ellipsis: true,
      },
    ],
    [backupStatusLabel, formatDt, formatLocale, t],
  );

  const recentRows = runsForMetrics.slice(0, 8);

  return (
    <BackupDrSection
      titleKey="backupDr.dashboardSections.monitoring"
      descriptionKey="backupDr.dashboardSections.monitoringDesc"
      t={t}
    >
      <BackupStatusHeader
        latest={latest}
        backupStatusLabel={backupStatusLabel}
        formatDt={formatDt}
        formatLocale={formatLocale}
        simulatedOperationalMode={simulatedOperationalMode}
        t={t}
      />

      <BackupMonitoringMetricsRow
        latest={latest}
        latestDetail={latestDetail}
        runsForMetrics={runsForMetrics}
        restoreLatest={restoreLatest}
        simulatedOperationalMode={simulatedOperationalMode}
        backupStatusLabel={backupStatusLabel}
        restoreStatusLabel={restoreStatusLabel}
        formatDt={formatDt}
        formatLocale={formatLocale}
        loading={loading || metricsRunsQuery.isLoading}
        t={t}
      />

      <Row gutter={[16, 16]} style={{ marginTop: 8 }}>
        <Col xs={24}>
          <BackupHistoryChart
            runs={runsForMetrics}
            chartData={
              chartRowsFromStats.length > 0 ? chartRowsFromStats : undefined
            }
            formatLocale={formatLocale}
            title={t("backupDr.monitoring.charts.history30d")}
            successLabel={t("backupDr.monitoring.charts.legendSuccess")}
            failedLabel={t("backupDr.monitoring.charts.legendFailed")}
            durationLabel={t("backupDr.monitoring.charts.legendDuration")}
            durationSuffix={t("backupDr.monitoring.charts.durationSuffix")}
            onBarClick={(runId) =>
              router.push(`/settings/backup-dr?runId=${encodeURIComponent(runId)}`)
            }
          />
        </Col>
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 8 }}>
        <Col xs={24} lg={12}>
          <BackupConfigurationHealthCard
            config={configurationHealth}
            artifactPipelinePolicy={artifactPipelinePolicy}
            canManage={canManageBackup}
            t={t}
          />
        </Col>
        <Col xs={24} lg={12}>
          <BackupRestoreReadinessCard
            recoverability={recoverability}
            restoreLatest={restoreLatest}
            restoreReadiness={restoreReadiness}
            averageSucceededBackupDurationSeconds={
              statusPayload?.averageSucceededBackupDurationSeconds
            }
            restoreStatusLabel={restoreStatusLabel}
            formatDt={formatDt}
            formatLocale={formatLocale}
            canManage={canRestore}
            t={t}
          />
        </Col>
      </Row>

      <RecentRunsTable
        title={t("backupDr.monitoring.recentTable.title")}
        rowKey="id"
        dataSource={recentRows}
        columns={recentColumns}
        loading={metricsRunsQuery.isLoading}
        queryError={metricsRunsQuery.isError}
        t={t}
        onRetry={() => void metricsRunsQuery.refetch()}
        pagination={false}
      />
    </BackupDrSection>
  );
}
