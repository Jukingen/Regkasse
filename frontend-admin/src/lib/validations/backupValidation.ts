/**
 * Backup schedule / retention validation.
 * AGENTS.md: Tenant backup retention admin API / FA clamp 7–90 days.
 */

export const BACKUP_RETENTION_MIN_DAYS = 7;
export const BACKUP_RETENTION_MAX_DAYS = 90;
export const BACKUP_RETENTION_DEFAULT_DAYS = 30;

export type BackupRetentionValidationCode = 'required' | 'tooLow' | 'tooHigh' | 'notInteger';

export function validateBackupRetentionDays(
  value: number | null | undefined
): BackupRetentionValidationCode | null {
  if (value == null || Number.isNaN(value)) {
    return 'required';
  }
  if (!Number.isInteger(value)) {
    return 'notInteger';
  }
  if (value < BACKUP_RETENTION_MIN_DAYS) {
    return 'tooLow';
  }
  if (value > BACKUP_RETENTION_MAX_DAYS) {
    return 'tooHigh';
  }
  return null;
}

/** Clamp to FA-allowed retention window (matches BackupScheduleSettings UI). */
export function clampBackupRetentionDays(value: number | null | undefined): number {
  const raw =
    typeof value === 'number' && !Number.isNaN(value) ? value : BACKUP_RETENTION_DEFAULT_DAYS;
  return Math.min(Math.max(Math.round(raw), BACKUP_RETENTION_MIN_DAYS), BACKUP_RETENTION_MAX_DAYS);
}

export function isValidBackupRetentionDays(value: number | null | undefined): boolean {
  return validateBackupRetentionDays(value) === null;
}
