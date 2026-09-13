import { isBackupRunFailed } from '@/features/backup/logic/backupRunTablePresentation';
import type { BackupDashboardStatsResponseDto } from '@/features/backup/logic/backupDashboardStatsApi';

export type BackupHealthWidgetAlert = {
  type: 'error' | 'warning' | 'info';
  key: string;
  values?: Record<string, string | number>;
};

/** Compact alert strip for the main-dashboard backup widget. */
export function buildBackupHealthWidgetAlerts(
  stats: BackupDashboardStatsResponseDto | undefined
): BackupHealthWidgetAlert[] {
  if (!stats) return [];
  const alerts: BackupHealthWidgetAlert[] = [];

  if (isBackupRunFailed(stats.lastBackupStatus)) {
    alerts.push({ type: 'error', key: 'dashboard.backupStatusWidget.alert_last_failed' });
  }

  const rpo = (stats.rpoStatus ?? '').trim();
  if (rpo === 'Critical') {
    alerts.push({ type: 'error', key: 'dashboard.backupStatusWidget.alert_rpo_critical' });
  } else if (rpo === 'AtRisk') {
    alerts.push({ type: 'warning', key: 'dashboard.backupStatusWidget.alert_rpo_warning' });
  }

  if (stats.latestRestoreDrillStatus === 3) {
    alerts.push({ type: 'error', key: 'dashboard.backupStatusWidget.alert_drill_failed' });
  }

  const storagePercent = stats.stagingDiskUsedPercent ?? null;
  if (stats.stagingDiskAlert === true || (storagePercent != null && storagePercent >= 80)) {
    alerts.push({ type: 'warning', key: 'dashboard.backupStatusWidget.storage_alert' });
  }

  if ((stats.pendingRunsCount ?? 0) > 0) {
    alerts.push({
      type: 'info',
      key: 'dashboard.backupStatusWidget.pending_runs',
      values: { count: stats.pendingRunsCount ?? 0 },
    });
  }

  return alerts;
}
