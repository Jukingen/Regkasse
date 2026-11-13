import { describe, expect, it } from 'vitest';

import type { RestoreVerificationRunDto } from '@/features/backup/logic/restoreVerificationApi';
import { buildRestoreVerificationProgressViewModel } from '@/features/backup/logic/restoreVerificationProgressPresentation';

function run(partial: Partial<RestoreVerificationRunDto>): RestoreVerificationRunDto {
  return {
    id: 'run-1',
    status: 1,
    triggerSource: 0,
    requestedAt: '2026-09-10T00:00:00Z',
    ...partial,
  };
}

describe('buildRestoreVerificationProgressViewModel', () => {
  it('marks Hash as current when the dump list stage is not reached', () => {
    const vm = buildRestoreVerificationProgressViewModel(run({ restoreDrillReachedStage: 10 }));
    expect(vm?.steps[0]?.id).toBe('hash');
    expect(vm?.steps[0]?.state).toBe('process');
    expect(vm?.isInProgress).toBe(true);
    expect(vm?.progressStatus).toBe('active');
  });

  it('completes Hash/Schema/Data after continuity stage and estimates remaining time', () => {
    const vm = buildRestoreVerificationProgressViewModel(run({ restoreDrillReachedStage: 40 }), {
      typicalDurationMs: 100_000,
    });
    expect(vm?.steps.find((s) => s.id === 'hash')?.state).toBe('finish');
    expect(vm?.steps.find((s) => s.id === 'schema')?.state).toBe('finish');
    expect(vm?.steps.find((s) => s.id === 'data')?.state).toBe('finish');
    expect(vm?.estimatedRemainingMs).toBeGreaterThan(0);
  });

  it('uses check results for FAILED fiscal and exception bar', () => {
    const vm = buildRestoreVerificationProgressViewModel(
      run({
        status: 3,
        restoreDrillReachedStage: 50,
        checks: [
          { id: 'hash', result: 'passed' },
          { id: 'schema', result: 'passed' },
          { id: 'data', result: 'passed' },
          { id: 'tse', result: 'skipped' },
          { id: 'fiscal', result: 'failed' },
        ],
      })
    );
    expect(vm?.isError).toBe(true);
    expect(vm?.progressStatus).toBe('exception');
    expect(vm?.steps.find((s) => s.id === 'fiscal')?.state).toBe('error');
  });
});
