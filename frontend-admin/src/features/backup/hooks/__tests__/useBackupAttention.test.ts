import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { resolveBackupRunStatusUiKey } from '@/features/backup/logic/backupRunTablePresentation';

function isBackupFailureStatus(status: number | undefined): boolean {
  const uiKey = resolveBackupRunStatusUiKey(status);
  return uiKey === 'failed' || uiKey === 'verificationFailed';
}

describe('useBackupAttention status helper', () => {
  it('treats failed and verificationFailed as attention', () => {
    expect(isBackupFailureStatus(BackupRunStatus.NUMBER_4)).toBe(true);
    expect(isBackupFailureStatus(BackupRunStatus.NUMBER_5)).toBe(true);
  });

  it('ignores succeeded and in-progress statuses', () => {
    expect(isBackupFailureStatus(BackupRunStatus.NUMBER_3)).toBe(false);
    expect(isBackupFailureStatus(BackupRunStatus.NUMBER_1)).toBe(false);
    expect(isBackupFailureStatus(undefined)).toBe(false);
  });
});
