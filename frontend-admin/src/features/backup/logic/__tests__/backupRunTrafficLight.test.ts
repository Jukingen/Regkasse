import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  backupRunTrafficLightTagColor,
  resolveBackupRunTrafficLight,
} from '@/features/backup/logic/backupRunTrafficLight';

describe('backupRunTrafficLight', () => {
  it('maps succeeded to green, pending to yellow, failed to red', () => {
    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_3)).toBe('success');
    expect(backupRunTrafficLightTagColor('success')).toBe('success');

    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_0)).toBe('pending');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_1)).toBe('pending');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_2)).toBe('pending');
    expect(backupRunTrafficLightTagColor('pending')).toBe('warning');

    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_4)).toBe('failed');
    expect(resolveBackupRunTrafficLight(BackupRunStatus.NUMBER_5)).toBe('failed');
    expect(backupRunTrafficLightTagColor('failed')).toBe('error');
  });
});
