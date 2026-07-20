"use client";

/**
 * Backup performance metrics — avg duration/size, success rate, staging usage, 30d history.
 */

import React, { useMemo } from "react";
import { Alert, Card, Col, Row, Statistic, Table, Typography } from "antd";
import type { ColumnsType } from "antd/es/table";
import { PageSkeleton } from "@/components/Skeleton";
import { useI18n } from "@/i18n";
import { useBackupPerformance } from "@/features/backup/hooks/useBackupPerformance";
import type { BackupPerformanceHistoryRow } from "@/features/backup/logic/backupPerformancePresentation";
import { BackupStatusBadge } from "@/features/backup/components/BackupStatusBadge";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";

function uiKeyToStatus(uiKey: string): number | undefined {
  switch (uiKey) {
    case "queued":
      return BackupRunStatus.NUMBER_0;
    case "running":
      return BackupRunStatus.NUMBER_1;
    case "awaitingVerification":
      return BackupRunStatus.NUMBER_2;
    case "succeeded":
      return BackupRunStatus.NUMBER_3;
    case "failed":
      return BackupRunStatus.NUMBER_4;
    case "verificationFailed":
      return BackupRunStatus.NUMBER_5;
    case "cancelled":
      return BackupRunStatus.NUMBER_6;
    default:
      return undefined;
  }
}

export function BackupPerformanceDashboard() {
  const { t } = useI18n();
  const { data: stats, isLoading, isError } = useBackupPerformance();

  const columns: ColumnsType<BackupPerformanceHistoryRow> = useMemo(
    () => [
      {
        title: t("backupDr.performance.columns.date"),
        dataIndex: "date",
        key: "date",
      },
      {
        title: t("backupDr.performance.columns.duration"),
        dataIndex: "duration",
        key: "duration",
      },
      {
        title: t("backupDr.performance.columns.size"),
        dataIndex: "size",
        key: "size",
      },
      {
        title: t("backupDr.performance.columns.status"),
        key: "status",
        render: (_: unknown, row: BackupPerformanceHistoryRow) => (
          <BackupStatusBadge status={uiKeyToStatus(row.statusUiKey)} />
        ),
      },
    ],
    [t],
  );

  if (isLoading) return <PageSkeleton widgets={4} />;

  if (isError || !stats) {
    return (
      <Alert
        type="error"
        showIcon
        title={t("backupDr.errors.loadFailed")}
        description={t("backupDr.monitoring.dashboardStatsLoadFailed")}
      />
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t("backupDr.performance.subtitle")}
      </Typography.Paragraph>

      {stats.stagingDiskAlert ? (
        <Alert
          type="warning"
          showIcon
          title={t("backupDr.monitoring.diskAlert.title")}
          description={t("backupDr.monitoring.diskAlert.description", {
            percent: stats.storageUsedPercent ?? "—",
          })}
        />
      ) : null}

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.performance.metrics.avgDuration")}
              value={stats.avgDurationSeconds ?? "—"}
              suffix={stats.avgDurationSeconds != null ? t("backupDr.performance.suffix.seconds") : undefined}
            />
            {stats.sampleCount > 0 ? (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t("backupDr.performance.sampleCount", { n: stats.sampleCount })}
              </Typography.Text>
            ) : null}
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.performance.metrics.avgSize")}
              value={stats.avgSizeMb ?? "—"}
              suffix={stats.avgSizeMb != null ? t("backupDr.performance.suffix.mb") : undefined}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t("backupDr.performance.avgSizeHint")}
            </Typography.Text>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.performance.metrics.successRate")}
              value={stats.successRatePercent ?? "—"}
              suffix={stats.successRatePercent != null ? t("backupDr.performance.suffix.percent") : undefined}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card size="small">
            <Statistic
              title={t("backupDr.performance.metrics.storageUsed")}
              value={stats.storageUsedPercent ?? "—"}
              suffix={
                stats.storageUsedPercent != null
                  ? t("backupDr.performance.suffix.percent")
                  : undefined
              }
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t("backupDr.performance.storageHint")}
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Card size="small" title={t("backupDr.performance.historyTitle")}>
        <Table<BackupPerformanceHistoryRow>
          rowKey="key"
          dataSource={stats.history}
          columns={columns}
          pagination={{ pageSize: 10 }}
          size="small"
          locale={{ emptyText: t("backupDr.performance.historyEmpty") }}
        />
      </Card>
    </div>
  );
}
