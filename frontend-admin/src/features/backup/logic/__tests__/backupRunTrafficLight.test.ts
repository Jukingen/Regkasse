import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  backupRunTrafficLightTagColor,
  resolveBackupRunTrafficLight,
} from '@/features/backup/logic/backupRunTrafficLight';

describe('backupRunTrafficLight', () => {
  it('maps succeeded to green, pending to yellow, failed to red', () => {
    expect(resolveBackupRunTrafficLight(BackupRunStatus.Succeeded)).toBe('success');
    expect(backupRunTrafficLightTagColor('success')).toBe('success');

    expect(resolveBackupRunTrafficLight(BackupRunStatus.Queued)).toBe('pending');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.Running)).toBe('pending');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.AwaitingVerification)).toBe('pending');
    expect(backupRunTrafficLightTagColor('pending')).toBe('warning');

    expect(resolveBackupRunTrafficLight(BackupRunStatus.Failed)).toBe('failed');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.VerificationFailed)).toBe('failed');
    expect(backupRunTrafficLightTagColor('failed')).toBe('error');
  });
});
