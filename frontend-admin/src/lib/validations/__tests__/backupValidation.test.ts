import { describe, expect, it } from 'vitest';

import {
  BACKUP_RETENTION_DEFAULT_DAYS,
  BACKUP_RETENTION_MAX_DAYS,
  BACKUP_RETENTION_MIN_DAYS,
  clampBackupRetentionDays,
  isValidBackupRetentionDays,
  validateBackupRetentionDays,
} from '@/lib/validations/backupValidation';

describe('backupValidation', () => {
  it('uses AGENTS.md FA retention window 7–90', () => {
    expect(BACKUP_RETENTION_MIN_DAYS).toBe(7);
    expect(BACKUP_RETENTION_MAX_DAYS).toBe(90);
    expect(BACKUP_RETENTION_DEFAULT_DAYS).toBe(30);
  });

  it('validates retention edge cases', () => {
    expect(validateBackupRetentionDays(undefined)).toBe('required');
    expect(validateBackupRetentionDays(6)).toBe('tooLow');
    expect(validateBackupRetentionDays(91)).toBe('tooHigh');
    expect(validateBackupRetentionDays(7.5)).toBe('notInteger');
    expect(validateBackupRetentionDays(30)).toBeNull();
    expect(isValidBackupRetentionDays(7)).toBe(true);
  });

  it('clamps out-of-range values for UI', () => {
    expect(clampBackupRetentionDays(1)).toBe(7);
    expect(clampBackupRetentionDays(200)).toBe(90);
    expect(clampBackupRetentionDays(null)).toBe(30);
    expect(clampBackupRetentionDays(45.6)).toBe(46);
  });
});
