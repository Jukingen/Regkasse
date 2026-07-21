import { describe, expect, it } from 'vitest';

import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  buildBackupHistory30DayChartData,
  computeSuccessRateInWindow,
  computeSuccessRateTrendPercent,
  mapBackupRunToMetricStatus,
} from '@/features/backup-dr/logic/backupMonitoringMetrics';

function run(over: Partial<BackupRunResponseDto> & { status: number }): BackupRunResponseDto {
  return {
    id: crypto.randomUUID(),
    requestedAt: new Date().toISOString(),
    completedAt: new Date().toISOString(),
    ...over,
  };
}

describe('backupMonitoringMetrics', () => {
  it('maps succeeded backup to success unless simulated', () => {
    expect(mapBackupRunToMetricStatus(BackupRunStatus.NUMBER_3, { simulated: true })).toBe(
      'warning'
    );
    expect(mapBackupRunToMetricStatus(BackupRunStatus.NUMBER_3)).toBe('success');
    expect(mapBackupRunToMetricStatus(BackupRunStatus.NUMBER_4)).toBe('error');
  });

  it('computes 30d success rate from terminal runs', () => {
    const now = Date.now();
    const iso = new Date(now - 2 * 86_400_000).toISOString();
    const runs = [
      run({ status: BackupRunStatus.NUMBER_3, completedAt: iso }),
      run({ status: BackupRunStatus.NUMBER_4, completedAt: iso }),
      run({ status: BackupRunStatus.NUMBER_1, completedAt: iso }),
    ];
    const w = computeSuccessRateInWindow(runs, now - 30 * 86_400_000, now + 1000);
    expect(w.terminalCount).toBe(2);
    expect(w.ratePercent).toBe(50);
  });

  it('builds 30d history chart rows with success, failed, and duration', () => {
    const now = Date.now();
    const iso = new Date(now - 2 * 86_400_000).toISOString();
    const started = new Date(now - 2 * 86_400_000 - 60_000).toISOString();
    const runs = [
      run({
        status: BackupRunStatus.NUMBER_3,
        completedAt: iso,
        startedAt: started,
        requestedAt: started,
      }),
      run({ status: BackupRunStatus.NUMBER_5, completedAt: iso }),
      run({
        status: BackupRunStatus.NUMBER_3,
        completedAt: new Date(now - 40 * 86_400_000).toISOString(),
      }),
    ];
    const rows = buildBackupHistory30DayChartData(runs, () => '01.01.', now);
    expect(rows).toHaveLength(2);
    expect(rows[0]?.success).toBe(1);
    expect(rows[0]?.failed).toBe(0);
    expect(rows[0]?.duration).toBeGreaterThan(0);
    expect(rows[1]?.failed).toBe(1);
    expect(rows[1]?.success).toBe(0);
  });

  it('returns trend when both windows have data', () => {
    const now = Date.now();
    const recentIso = new Date(now - 5 * 86_400_000).toISOString();
    const oldIso = new Date(now - 40 * 86_400_000).toISOString();
    const runs = [
      run({ status: BackupRunStatus.NUMBER_3, completedAt: recentIso }),
      run({ status: BackupRunStatus.NUMBER_3, completedAt: recentIso }),
      run({ status: BackupRunStatus.NUMBER_4, completedAt: oldIso }),
      run({ status: BackupRunStatus.NUMBER_4, completedAt: oldIso }),
    ];
    const trend = computeSuccessRateTrendPercent(runs, now);
    expect(trend).toBe(100);
  });
});
