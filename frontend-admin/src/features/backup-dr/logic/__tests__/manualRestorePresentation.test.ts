import { describe, expect, it } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import {
  defaultValidationDatabaseName,
  isBackupRunEligibleForManualRestore,
  isValidValidationDatabaseName,
  shouldPollManualRestoreStatus,
} from '@/features/backup-dr/logic/manualRestorePresentation';

describe('manualRestorePresentation', () => {
  it('defaultValidationDatabaseName uses restore_validation_ prefix', () => {
    const name = defaultValidationDatabaseName(new Date('2024-12-31T12:00:00Z'));
    expect(name).toBe('restore_validation_20241231');
  });

  it('validates target database naming', () => {
    expect(isValidValidationDatabaseName('restore_validation_test')).toBe(true);
    expect(isValidValidationDatabaseName('prod_db')).toBe(false);
  });

  it('eligible only for succeeded backup runs', () => {
    expect(isBackupRunEligibleForManualRestore(BackupRunStatus.NUMBER_3)).toBe(true);
    expect(isBackupRunEligibleForManualRestore(BackupRunStatus.NUMBER_4)).toBe(false);
  });

  it('polls while executing', () => {
    expect(shouldPollManualRestoreStatus('Executing')).toBe(true);
    expect(shouldPollManualRestoreStatus('Completed')).toBe(false);
  });
});
