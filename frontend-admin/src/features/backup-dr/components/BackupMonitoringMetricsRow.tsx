'use client';

/**
 * Üst metrik kartları: son backup, boyut, 30g başarı oranı, son restore drill.
 */
import { Col, Row } from 'antd';
import React, { useMemo } from 'react';

import type {
  BackupRunResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import { MetricCard } from '@/features/backup-dr/components/MetricCard';
import { formatBackupBytes, formatBackupDurationMs } from '@/features/backup-dr/logic/backupFormat';
import {
  computeSuccessRateInWindow,
  computeSuccessRateTrendPercent,
  mapBackupRunToMetricStatus,
  mapRestoreDrillToMetricStatus,
} from '@/features/backup-dr/logic/backupMonitoringMetrics';
import {
  formatRunDurationMs,
  sumLogicalDumpBytes,
} from '@/features/backup-dr/logic/backupPipelineDerived';
import { isBackupLatestRunActiveStatus } from '@/features/backup-dr/logic/backupRunDetailPollPolicy';

export interface BackupMonitoringMetricsRowProps {
  latest: BackupRunResponseDto | undefined;
  latestDetail: BackupRunResponseDto | undefined;
  runsForMetrics: readonly BackupRunResponseDto[];
  restoreLatest: RestoreVerificationRunResponseDto | undefined;
  simulatedOperationalMode?: boolean;
  backupStatusLabel: (status: number | undefined) => string;
  restoreStatusLabel: (status: number | undefined) => string;
  formatDt: (iso: string | undefined | null, locale: string) => string;
  formatLocale: string;
  loading?: boolean;
  t: (key: string, options?: Record<string, string | number>) => string;
}

export function BackupMonitoringMetricsRow({
  latest,
  latestDetail,
  runsForMetrics,
  restoreLatest,
  simulatedOperationalMode,
  backupStatusLabel,
  restoreStatusLabel,
  formatDt,
  formatLocale,
  loading,
  t,
}: BackupMonitoringMetricsRowProps) {
  const detail = latestDetail ?? latest;
  const backupBytes = sumLogicalDumpBytes(detail?.artifacts);
  const durationMs = formatRunDurationMs(detail?.requestedAt, detail?.completedAt);

  const successWindow = useMemo(() => {
    const now = Date.now();
    return computeSuccessRateInWindow(runsForMetrics, now - 30 * 86_400_000, now);
  }, [runsForMetrics]);

  const trend = useMemo(() => computeSuccessRateTrendPercent(runsForMetrics), [runsForMetrics]);

  const lastBackupStatus = mapBackupRunToMetricStatus(latest?.status, {
    simulated: simulatedOperationalMode || latest?.isSimulatedExecution,
    active: isBackupLatestRunActiveStatus(latest?.status),
  });

  const drillStatus = mapRestoreDrillToMetricStatus(restoreLatest?.status);

  const lastBackupValue = `${backupStatusLabel(latest?.status)} · ${formatDt(
    latest?.completedAt ?? latest?.requestedAt,
    formatLocale
  )}`;

  const successRateValue =
    successWindow.ratePercent === null ? '—' : `${successWindow.ratePercent}%`;

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} sm={12} lg={6}>
        <MetricCard
          title={t('backupDr.monitoring.metrics.lastBackup')}
          value={lastBackupValue}
          status={lastBackupStatus}
          loading={loading}
        />
      </Col>
      <Col xs={24} sm={12} lg={6}>
        <MetricCard
          title={t('backupDr.monitoring.metrics.backupSize')}
          value={formatBackupBytes(backupBytes, t)}
          status={backupBytes !== undefined ? 'info' : undefined}
          loading={loading}
        />
        <div style={{ marginTop: 4, fontSize: 12, color: 'rgba(0,0,0,0.45)' }}>
          {t('backupDr.monitoring.metrics.duration')}: {formatBackupDurationMs(durationMs, t)}
        </div>
      </Col>
      <Col xs={24} sm={12} lg={6}>
        <MetricCard
          title={t('backupDr.monitoring.metrics.successRate30d')}
          value={successRateValue}
          status={
            successWindow.ratePercent === null
              ? undefined
              : successWindow.ratePercent >= 90
                ? 'success'
                : successWindow.ratePercent >= 70
                  ? 'warning'
                  : 'error'
          }
          trend={trend}
          trendLabel={t('backupDr.monitoring.metrics.trendVsPriorMonth')}
          loading={loading}
        />
      </Col>
      <Col xs={24} sm={12} lg={6}>
        <MetricCard
          title={t('backupDr.monitoring.metrics.lastRestoreDrill')}
          value={`${restoreStatusLabel(restoreLatest?.status)} · ${formatDt(
            restoreLatest?.completedAt ?? restoreLatest?.requestedAt,
            formatLocale
          )}`}
          status={drillStatus}
          loading={loading}
        />
      </Col>
    </Row>
  );
}
