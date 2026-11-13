import { describe, expect, it } from 'vitest';

import { buildBackupHealthWidgetAlerts } from '@/features/backup/logic/backupHealthWidgetAlerts';

describe('buildBackupHealthWidgetAlerts', () => {
  it('emits last-failed, RPO critical, drill failed, and storage alerts', () => {
    const alerts = buildBackupHealthWidgetAlerts({
      lastBackupStatus: 4,
      rpoStatus: 'Critical',
      latestRestoreDrillStatus: 3,
      stagingDiskUsedPercent: 85,
      pendingRunsCount: 2,
    });

    expect(alerts.map((a) => a.key)).toEqual([
      'dashboard.backupStatusWidget.alert_last_failed',
      'dashboard.backupStatusWidget.alert_rpo_critical',
      'dashboard.backupStatusWidget.alert_drill_failed',
      'dashboard.backupStatusWidget.storage_alert',
      'dashboard.backupStatusWidget.pending_runs',
    ]);
    expect(alerts.find((a) => a.key.endsWith('pending_runs'))?.values).toEqual({ count: 2 });
  });

  it('returns no alerts for a healthy snapshot', () => {
    expect(
      buildBackupHealthWidgetAlerts({
        lastBackupStatus: 3,
        rpoStatus: 'Healthy',
        latestRestoreDrillStatus: 2,
        stagingDiskUsedPercent: 20,
        pendingRunsCount: 0,
      })
    ).toEqual([]);
  });
});
