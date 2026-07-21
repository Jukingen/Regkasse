import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import type { BackupDashboardStatsResponseDto } from '@/features/backup/logic/backupDashboardStatsApi';
import { mapDashboardStatsToPerformance } from '@/features/backup/logic/backupPerformancePresentation';

describe('backupPerformancePresentation', () => {
  it('maps dashboard stats into performance view-model', () => {
    const stats: BackupDashboardStatsResponseDto = {
      averageSucceededBackupDurationSeconds: 125.4,
      averageSucceededBackupDurationSampleCount: 5,
      backupSizeBytes: 10 * 1024 * 1024,
      successRate30DaysPercent: 92.5,
      stagingDiskUsedPercent: 41.2,
      stagingDiskAlert: false,
      history30Days: [
        {
          runId: 'run-1',
          completedAtUtc: '2026-07-17T10:00:00Z',
          status: BackupRunStatus.NUMBER_3,
          success: 1,
          failed: 0,
          durationSeconds: 120,
        },
      ],
    };

    const vm = mapDashboardStatsToPerformance(stats, 'de-AT', (key, opts) =>
      opts ? `${key}:${JSON.stringify(opts)}` : key
    );

    expect(vm).not.toBeNull();
    expect(vm!.avgDurationSeconds).toBe(125.4);
    expect(vm!.avgSizeMb).toBe(10);
    expect(vm!.successRatePercent).toBe(92.5);
    expect(vm!.storageUsedPercent).toBe(41.2);
    expect(vm!.history).toHaveLength(1);
    expect(vm!.history[0].statusUiKey).toBe('succeeded');
  });
});
