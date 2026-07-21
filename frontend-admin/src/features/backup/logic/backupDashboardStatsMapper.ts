import type {
  BackupRecoverabilitySummaryResponseDto,
  BackupRunResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';
import {
  mapBackupRunToMetricStatus,
  mapRestoreDrillToMetricStatus,
} from '@/features/backup-dr/logic/backupMonitoringMetrics';
import type {
  BackupDashboardHistoryPointDto,
  BackupDashboardStatsResponseDto,
} from '@/features/backup/logic/backupDashboardStatsApi';
import { formatUserMonthDay } from '@/lib/dateFormatter';

export interface BackupHistoryChartRow {
  key: string;
  date: string;
  success: number;
  failed: number;
  duration: number;
  runId?: string;
}

export function mapDashboardHistoryToChartRows(
  points: BackupDashboardHistoryPointDto[] | null | undefined,
  formatLocale: string
): BackupHistoryChartRow[] {
  return (points ?? []).map((p) => ({
    key: p.runId,
    runId: p.runId,
    date: formatChartDate(p.completedAtUtc, formatLocale),
    success: p.success,
    failed: p.failed,
    duration: p.durationSeconds,
  }));
}

function formatChartDate(iso: string, _formatLocale: string): string {
  return formatUserMonthDay(iso) || '—';
}

export function statsToRecoverabilitySummary(
  stats: BackupDashboardStatsResponseDto
): BackupRecoverabilitySummaryResponseDto {
  return {
    lastSuccessfulBackupAt:
      stats.lastSuccessfulBackupAtUtc ?? stats.lastVerifiedBackupAtUtc ?? undefined,
    lastSuccessfulRestoreProofAt: stats.lastSuccessfulRestoreDrillAtUtc ?? undefined,
    lastSuccessfulArtifactVerificationAt: stats.lastVerifiedBackupAtUtc ?? undefined,
    latestRestoreRunStatus:
      stats.latestRestoreDrillStatus as BackupRecoverabilitySummaryResponseDto['latestRestoreRunStatus'],
  };
}

export function buildSyntheticLatestRun(
  stats: BackupDashboardStatsResponseDto
): BackupRunResponseDto | undefined {
  if (!stats.lastBackupRunId && stats.lastBackupAtUtc == null) return undefined;
  return {
    id: stats.lastBackupRunId ?? undefined,
    status: stats.lastBackupStatus as BackupRunResponseDto['status'],
    completedAt: stats.lastBackupAtUtc ?? undefined,
    requestedAt: stats.lastBackupAtUtc ?? undefined,
  };
}

export function buildSyntheticRestoreLatest(
  stats: BackupDashboardStatsResponseDto
): RestoreVerificationRunResponseDto | undefined {
  if (stats.latestRestoreDrillStatus === undefined) return undefined;
  return {
    status: stats.latestRestoreDrillStatus as RestoreVerificationRunResponseDto['status'],
    completedAt: stats.lastSuccessfulRestoreDrillAtUtc ?? undefined,
  };
}

export function metricStatusFromStats(stats: BackupDashboardStatsResponseDto): {
  lastBackupStatus: ReturnType<typeof mapBackupRunToMetricStatus>;
  successRateValue: string;
  successMetricStatus: ReturnType<typeof mapBackupRunToMetricStatus>;
  drillStatus: ReturnType<typeof mapRestoreDrillToMetricStatus>;
} {
  const lastBackupStatus = mapBackupRunToMetricStatus(stats.lastBackupStatus);
  const successRateValue =
    stats.successRate30DaysPercent == null ? '—' : `${stats.successRate30DaysPercent}%`;
  const rate = stats.successRate30DaysPercent ?? 0;
  const successMetricStatus =
    stats.successRate30DaysPercent == null
      ? undefined
      : rate >= 90
        ? 'success'
        : rate >= 70
          ? 'warning'
          : 'error';

  return {
    lastBackupStatus,
    successRateValue,
    successMetricStatus,
    drillStatus: mapRestoreDrillToMetricStatus(stats.latestRestoreDrillStatus),
  };
}
