import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model';
import {
  BACKUP_ACTIVE_POLL_MS,
  RUN_DETAIL_CATCH_UP_POLL_MS,
  computeRunDetailRefetchIntervalMs,
  isBackupLatestRunActiveStatus,
} from '@/features/backup-dr/logic/backupRunDetailPollPolicy';

describe('computeRunDetailRefetchIntervalMs (staleness / post-terminal catch-up)', () => {
  it('returns false when terminal detail matches latest status — polling stops', () => {
    expect(
      computeRunDetailRefetchIntervalMs({
        latestRunId: 'run-1',
        latestStatus: BackupRunStatus.Succeeded,
        detail: { status: BackupRunStatus.Succeeded } as never,
      })
    ).toBe(false);
  });

  it('returns catch-up interval when detail not loaded yet after terminal transition', () => {
    expect(
      computeRunDetailRefetchIntervalMs({
        latestRunId: 'run-1',
        latestStatus: BackupRunStatus.Succeeded,
        detail: undefined,
      })
    ).toBe(RUN_DETAIL_CATCH_UP_POLL_MS);
  });

  it('returns catch-up when detail status lags behind latest summary', () => {
    expect(
      computeRunDetailRefetchIntervalMs({
        latestRunId: 'run-1',
        latestStatus: BackupRunStatus.Succeeded,
        detail: { status: BackupRunStatus.AwaitingVerification } as never,
      })
    ).toBe(RUN_DETAIL_CATCH_UP_POLL_MS);
  });

  it('uses active interval while latest run is still active', () => {
    expect(
      computeRunDetailRefetchIntervalMs({
        latestRunId: 'run-1',
        latestStatus: BackupRunStatus.Running,
        detail: { status: BackupRunStatus.Running } as never,
      })
    ).toBe(BACKUP_ACTIVE_POLL_MS);
  });

  it('returns false when no latest run id', () => {
    expect(
      computeRunDetailRefetchIntervalMs({
        latestRunId: undefined,
        latestStatus: BackupRunStatus.Succeeded,
        detail: undefined,
      })
    ).toBe(false);
  });
});

describe('isBackupLatestRunActiveStatus', () => {
  it.each([
    [BackupRunStatus.Queued, true],
    [BackupRunStatus.Running, true],
    [BackupRunStatus.AwaitingVerification, true],
    [BackupRunStatus.Succeeded, false],
    [BackupRunStatus.Failed, false],
  ])('status %s -> %s', (status, expected) => {
    expect(isBackupLatestRunActiveStatus(status)).toBe(expected);
  });
});
