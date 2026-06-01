"use client";

/**
 * Son backup durumu başlık şeridi (izleme panosu üstü).
 */

import React from "react";
import { Alert, Space, Tag, Typography } from "antd";
import type { BackupRunResponseDto } from "@/api/generated/model";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import { mapBackupRunToMetricStatus } from "@/features/backup-dr/logic/backupMonitoringMetrics";
import { mapBackupRunStatusAntdColor } from "@/features/backup-dr/logic/backupDrMappers";

export interface BackupStatusHeaderProps {
  latest: BackupRunResponseDto | undefined;
  backupStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  simulatedOperationalMode?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

function headerAlertType(
  status: MetricStatusLike,
): "success" | "warning" | "error" | "info" {
  if (status === "error") return "error";
  if (status === "warning") return "warning";
  if (status === "success") return "success";
  return "info";
}

type MetricStatusLike = ReturnType<typeof mapBackupRunToMetricStatus>;

export function BackupStatusHeader({
  latest,
  backupStatusLabel,
  formatDt,
  formatLocale,
  simulatedOperationalMode,
  t,
}: BackupStatusHeaderProps) {
  const st = latest?.status;
  const active =
    st === BackupRunStatus.NUMBER_0 ||
    st === BackupRunStatus.NUMBER_1 ||
    st === BackupRunStatus.NUMBER_2;
  const metricStatus = mapBackupRunToMetricStatus(st, {
    simulated: simulatedOperationalMode || latest?.isSimulatedExecution,
    active,
  });

  const when = formatDt(latest?.completedAt ?? latest?.requestedAt, formatLocale);

  return (
    <Alert
      type={headerAlertType(metricStatus)}
      showIcon
      title={
        <Space wrap align="center">
          <Typography.Text strong>{t("backupDr.monitoring.header.title")}</Typography.Text>
          <Tag color={mapBackupRunStatusAntdColor(st)}>
            {backupStatusLabel(st)}
          </Tag>
          <Typography.Text type="secondary">{when}</Typography.Text>
        </Space>
      }
      description={
        simulatedOperationalMode
          ? t("backupDr.monitoring.header.simulatedHint")
          : active
            ? t("backupDr.monitoring.header.activeHint")
            : undefined
      }
    />
  );
}
