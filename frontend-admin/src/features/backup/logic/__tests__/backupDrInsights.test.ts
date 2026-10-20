import { describe, expect, it } from 'vitest';

import {
  costSharePercents,
  downgradeGateForAcceptance,
  knownGapRowStatus,
  lastScheduledDrillCompletedAt,
} from '@/features/backup/logic/backupDrInsights';
import type { RestoreVerificationRunDto } from '@/features/backup/logic/restoreVerificationApi';

function run(
  partial: Pick<RestoreVerificationRunDto, 'id' | 'status' | 'triggerSource' | 'completedAt'>
): RestoreVerificationRunDto {
  return {
    requestedAt: partial.completedAt ?? '2026-01-01T00:00:00Z',
    restoreAttemptExecuted: false,
    fiscalSqlSkipped: false,
    postRestoreContinuityChecksExecuted: false,
    ...partial,
  };
}

describe('costSharePercents', () => {
  it('uses largest remainder so uneven costs still total 100', () => {
    const shares = costSharePercents([1.2, 0.4, 0.1]);
    expect(shares).toEqual([71, 23, 6]);
    expect(shares.reduce((sum, share) => sum + share, 0)).toBe(100);
  });

  it('returns zeros when there is no weight', () => {
    expect(costSharePercents([0, 0, 0])).toEqual([0, 0, 0]);
  });

  it('gives a single positive slice the full 100 percent', () => {
    expect(costSharePercents([0.023, 0, 0])).toEqual([100, 0, 0]);
  });
});

describe('lastScheduledDrillCompletedAt', () => {
  it('ignores a newer manual success and a scheduled row without CompletedAt', () => {
    const at = lastScheduledDrillCompletedAt([
      run({
        id: 'manual-new',
        status: 2,
        triggerSource: 0,
        completedAt: '2026-10-12T08:00:00Z',
      }),
      run({
        id: 'scheduled-open',
        status: 'Succeeded',
        triggerSource: 'Scheduled',
        completedAt: null,
      }),
      run({
        id: 'scheduled-old',
        status: 'Succeeded',
        triggerSource: 1,
        completedAt: '2026-10-01T08:00:00Z',
      }),
    ]);

    expect(at).toBe('2026-10-01T08:00:00Z');
  });

  it('returns null when every success is manual', () => {
    expect(
      lastScheduledDrillCompletedAt([
        run({
          id: 'manual',
          status: 'Succeeded',
          triggerSource: 'Manual',
          completedAt: '2026-10-12T08:00:00Z',
        }),
      ])
    ).toBeNull();
  });
});

describe('known gap acceptance color', () => {
  it('turns a red gate amber and leaves green unchanged', () => {
    expect(downgradeGateForAcceptance('red', true)).toBe('yellow');
    expect(downgradeGateForAcceptance('red', false)).toBe('red');
    expect(downgradeGateForAcceptance('green', true)).toBe('green');
    expect(knownGapRowStatus('amber')).toBe('amber');
    expect(knownGapRowStatus('red')).toBe('red');
  });
});
