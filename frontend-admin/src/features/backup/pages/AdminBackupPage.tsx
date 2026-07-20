"use client";

/**
 * Admin backup dashboard — /admin/backup
 * Metrikler, 30 günlük grafik, son çalıştırmalar ve daraltılabilir yapılandırma.
 */

import React, { useCallback, useMemo } from "react";
import { Alert, Card, Col, Collapse, Row, Space, Typography } from "antd";
import { PageSkeleton } from "@/components/Skeleton";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useI18n } from "@/i18n";
import { formatDateTime } from "@/i18n/formatting";
import { useBackupPermissions } from "@/features/backup/hooks/useBackupPermissions";
import { MetricCard } from "@/features/backup/components/MetricCard";
import { TriggerBackupButton } from "@/features/backup/components/TriggerBackupButton";
import { PitrRestoreWorkflow } from "@/features/backup/components/PitrRestoreWorkflow";
import { BackupHistoryChart } from "@/features/backup/components/BackupHistoryChart";
import { BackupList } from "@/features/backup/components/BackupList";
import { BackupSettings } from "@/features/backup/components/BackupSettings";
import { BackupRecentRestoreDrillsTable } from "@/features/backup-dr/components/BackupRecentRestoreDrillsTable";
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from "@/features/backup/logic/backupDashboardStatsApi";
import {
  buildSyntheticRestoreLatest,
  mapDashboardHistoryToChartRows,
  metricStatusFromStats,
  statsToRecoverabilitySummary,
} from "@/features/backup/logic/backupDashboardStatsMapper";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";
import { useGetApiAdminRestoreVerificationRunsLatest } from "@/api/generated/admin-restore-verification/admin-restore-verification";
import { buildBackupOperatorTruthModel } from "@/features/backup-dr/logic/backupDrOperatorTruthModel";
import { mapArtifactsToExternalCopyVariant } from "@/features/backup-dr/logic/backupDrMappers";
import { apiNullableToUndefined } from "@/features/backup-dr/logic/backupDrDtoNormalize";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import { normalizeHealthLevelString } from "@/features/backup-dr/logic/backupDrMappers";
import { useBackupManagementAccess } from "@/features/backup-management/hooks/useBackupManagementAccess";

function formatDt(iso: string | undefined | null, formatLocale: string): string {
  if (!iso) return "—";
  return formatDateTime(iso, formatLocale);
}

export function AdminBackupPage() {
  const { t, formatLocale } = useI18n();
  const router = useRouter();
  const queryClient = useQueryClient();
  const access = useBackupManagementAccess();
  const permissions = useBackupPermissions();

  const statsQuery = useQuery({
    queryKey: getBackupDashboardStatsQueryKey(),
    queryFn: getBackupDashboardStats,
    refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS,
    refetchOnWindowFocus: true,
  });

  const statusQuery = useGetApiAdminBackupStatusLatest({
    query: { refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS, refetchOnWindowFocus: true },
  });

  const restoreLatestQuery = useGetApiAdminRestoreVerificationRunsLatest({
    query: { refetchInterval: BACKUP_DASHBOARD_STATS_POLL_MS, refetchOnWindowFocus: true },
  });

  const stats = statsQuery.data;
  const latestFromStatus = statusQuery.data?.latestRun;
  const restoreLatest = apiNullableToUndefined(restoreLatestQuery.data);

  const health = stats?.configurationHealth ?? statusQuery.data?.configurationHealth;
  const healthLv = normalizeHealthLevelString(health?.level);

  const operatorTruth = useMemo(
    () =>
      buildBackupOperatorTruthModel({
        t,
        health,
        healthLv,
        restoreReady: undefined,
        restoreLv: "",
        latest: latestFromStatus,
        detailForPipeline: null,
        verification: undefined,
        restoreLatest,
        recoverabilitySummary: statsToRecoverabilitySummary(stats ?? {}),
        restoreCapability: statusQuery.data?.restore,
        externalCopyVariant: mapArtifactsToExternalCopyVariant(undefined),
        omitDedicatedSectionIssueDuplicates: true,
        hasStatusPayload: Boolean(stats ?? statusQuery.data),
      }),
    [health, healthLv, latestFromStatus, restoreLatest, stats, statusQuery.data, t],
  );

  const metrics = useMemo(() => (stats ? metricStatusFromStats(stats) : null), [stats]);

  const chartRows = useMemo(
    () => mapDashboardHistoryToChartRows(stats?.history30Days, formatLocale),
    [stats?.history30Days, formatLocale],
  );

  const navigateToRun = useCallback(
    (runId: string) => {
      router.push(`/backup/dashboard?runId=${encodeURIComponent(runId)}`);
    },
    [router],
  );

  const invalidateAll = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: getBackupDashboardStatsQueryKey() });
    await queryClient.invalidateQueries({ queryKey: ["/api/admin/backup"] });
    await queryClient.invalidateQueries({ queryKey: ["/api/admin/restore-verification"] });
  }, [queryClient]);

  if (statsQuery.isLoading && !stats) {
    return <PageSkeleton widgets={6} />;
  }

  if (statsQuery.isError) {
    return (
      <Alert
        type="error"
        showIcon
        title={t("backupDr.errors.loadFailed")}
        description={t("backupDr.monitoring.dashboardStatsLoadFailed")}
      />
    );
  }

  const lastBackupLabel = `${operatorTruth.labels.backupStatus(stats?.lastBackupStatus)} · ${formatDt(
    stats?.lastBackupAtUtc,
    formatLocale,
  )}`;

  const healthSummary =
    health?.level?.trim() ||
    health?.effectiveAdapterKind?.trim() ||
    t("backupDr.monitoring.configHealth.title");

  const activeBackupHint =
    latestFromStatus?.status === BackupRunStatus.NUMBER_1 ||
    latestFromStatus?.status === BackupRunStatus.NUMBER_2;

  const collapseItems = [
    {
      key: "config",
      label: t("backupDr.adminBackup.collapse.config"),
      children: <BackupSettings />,
    },
    ...(permissions.canRestore
      ? [
          {
            key: "restore",
            label: t("backupDr.adminBackup.collapse.restoreVerification"),
            children: (
              <Space orientation="vertical" size={16} style={{ width: "100%" }}>
                <PitrRestoreWorkflow
                  canRestore={permissions.canRestore}
                  showRequestsTable
                  formatDt={formatDt}
                  formatLocale={formatLocale}
                />
                <BackupRecentRestoreDrillsTable
                  formatDt={formatDt}
                  formatLocale={formatLocale}
                  restoreStatusLabel={(s) => operatorTruth.labels.restoreStatus(s)}
                  isSimulatedAdapterEnvironment={operatorTruth.simulatedOperationalMode}
                  t={t}
                  onRetryInvalidate={invalidateAll}
                />
              </Space>
            ),
          },
        ]
      : []),
  ];

  return (
    <div className="backup-page">
      <Space orientation="vertical" size={16} style={{ width: "100%" }}>
        {permissions.isReadOnly ? (
          <Alert type="info" showIcon title={t("backupDr.permission.noManage")} />
        ) : null}

        {access.isSuperAdmin ? (
          <Alert type="info" showIcon title={t("backupDr.management.scope.deploymentWide")} />
        ) : null}

        {activeBackupHint ? (
          <Alert type="info" showIcon title={t("backupDr.monitoring.header.activeHint")} />
        ) : null}

        <Row gutter={[16, 16]}>
          <Col xs={24} sm={12} lg={6}>
            <MetricCard
              title={t("backupDr.monitoring.metrics.lastBackup")}
              value={lastBackupLabel}
              status={metrics?.lastBackupStatus}
              loading={statsQuery.isFetching}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <MetricCard
              title={t("backupDr.monitoring.metrics.successRate30d")}
              value={metrics?.successRateValue ?? "—"}
              status={metrics?.successMetricStatus}
              trend={stats?.successRateTrendVsPrior30DaysPercent ?? undefined}
              trendLabel={t("backupDr.monitoring.metrics.trendVsPriorMonth")}
              loading={statsQuery.isFetching}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <MetricCard
              title={t("backupDr.monitoring.metrics.lastRestoreDrill")}
              value={`${operatorTruth.labels.restoreStatus(stats?.latestRestoreDrillStatus)} · ${formatDt(
                stats?.lastSuccessfulRestoreDrillAtUtc,
                formatLocale,
              )}`}
              status={metrics?.drillStatus}
              loading={statsQuery.isFetching}
            />
          </Col>
          <Col xs={24} sm={12} lg={6}>
            <MetricCard
              title={t("backupDr.monitoring.configHealth.title")}
              value={healthSummary}
              status={
                healthLv === "unhealthy"
                  ? "error"
                  : healthLv === "degraded"
                    ? "warning"
                    : healthLv === "healthy"
                      ? "success"
                      : undefined
              }
              loading={statsQuery.isFetching}
            />
          </Col>
        </Row>

        <BackupHistoryChart
          chartData={chartRows}
          formatLocale={formatLocale}
          title={t("backupDr.monitoring.charts.history30d")}
          successLabel={t("backupDr.monitoring.charts.legendSuccess")}
          failedLabel={t("backupDr.monitoring.charts.legendFailed")}
          durationLabel={t("backupDr.monitoring.charts.legendDuration")}
          durationSuffix={t("backupDr.monitoring.charts.durationSuffix")}
          onBarClick={navigateToRun}
        />

        <Card title={t("backupDr.adminBackup.recentBackupsTitle")} size="small">
          <BackupList onRetryInvalidate={invalidateAll} />
        </Card>

        <Collapse items={collapseItems} defaultActiveKey={[]} />

        <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
          {t("backupDr.monitoring.dashboardRefreshHint", {
            seconds: String(BACKUP_DASHBOARD_STATS_POLL_MS / 1000),
          })}
        </Typography.Paragraph>
      </Space>
    </div>
  );
}

/** Page header extra slot: manual backup trigger. */
export function AdminBackupPageHeaderActions() {
  const { formatLocale } = useI18n();
  const { canTrigger, canRestore } = useBackupPermissions();

  const formatDt = (iso: string | undefined | null, locale: string) => {
    if (!iso) return "—";
    return formatDateTime(iso, locale);
  };

  return (
    <Space>
      <PitrRestoreWorkflow
        canRestore={canRestore}
        formatDt={formatDt}
        formatLocale={formatLocale}
      />
      <TriggerBackupButton canManage={canTrigger} />
    </Space>
  );
}
