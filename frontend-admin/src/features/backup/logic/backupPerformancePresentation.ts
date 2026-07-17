/**
 * Maps dashboard stats into the backup performance page view-model.
 */

import { formatDateTime } from "@/i18n/formatting";
import { resolveBackupRunStatusUiKey } from "@/features/backup/logic/backupRunTablePresentation";
import type { BackupDashboardStatsResponseDto } from "@/features/backup/logic/backupDashboardStatsApi";

export type BackupPerformanceHistoryRow = {
  key: string;
  date: string;
  duration: string;
  size: string;
  status: string;
  statusUiKey: string;
  runId: string;
};

export type BackupPerformanceViewModel = {
  avgDurationSeconds: number | null;
  avgSizeMb: number | null;
  successRatePercent: number | null;
  /** Staging volume usage percent when known (not absolute GB). */
  storageUsedPercent: number | null;
  stagingDiskAlert: boolean;
  history: BackupPerformanceHistoryRow[];
  sampleCount: number;
};

function bytesToMb(bytes: number | null | undefined): number | null {
  if (bytes == null || bytes < 0) return null;
  return Math.round((bytes / (1024 * 1024)) * 10) / 10;
}

export function mapDashboardStatsToPerformance(
  stats: BackupDashboardStatsResponseDto | null | undefined,
  formatLocale: string,
  t: (key: string, options?: Record<string, string | number>) => string,
): BackupPerformanceViewModel | null {
  if (!stats) return null;

  const history: BackupPerformanceHistoryRow[] = (stats.history30Days ?? [])
    .slice()
    .sort((a, b) => b.completedAtUtc.localeCompare(a.completedAtUtc))
    .map((p) => {
      const uiKey = resolveBackupRunStatusUiKey(p.status);
      const statusLabel =
        uiKey === "unknown"
          ? t("backupDr.summary.unknown")
          : t(`backupDr.runsTable.statusLabels.${uiKey}`);
      return {
        key: p.runId,
        runId: p.runId,
        date: formatDateTime(p.completedAtUtc, formatLocale),
        duration: t("backupDr.performance.durationSeconds", {
          s: Math.round(p.durationSeconds),
        }),
        size: "—",
        status: statusLabel,
        statusUiKey: uiKey,
      };
    });

  return {
    avgDurationSeconds:
      stats.averageSucceededBackupDurationSeconds != null
        ? Math.round(stats.averageSucceededBackupDurationSeconds * 10) / 10
        : null,
    avgSizeMb: bytesToMb(stats.backupSizeBytes),
    successRatePercent:
      stats.successRate30DaysPercent != null
        ? Math.round(stats.successRate30DaysPercent * 10) / 10
        : null,
    storageUsedPercent:
      stats.stagingDiskUsedPercent != null
        ? Math.round(stats.stagingDiskUsedPercent * 10) / 10
        : null,
    stagingDiskAlert: Boolean(stats.stagingDiskAlert),
    history,
    sampleCount: stats.averageSucceededBackupDurationSampleCount ?? 0,
  };
}
