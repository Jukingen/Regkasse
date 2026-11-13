/**
 * Backup automation schedule settings — Orval-generated client + local cron helpers.
 */

export {
  getApiAdminBackupSettings as getBackupScheduleSettings,
  getGetApiAdminBackupSettingsQueryKey as getBackupScheduleSettingsQueryKey,
  getApiAdminBackupScheduleStatus as getBackupScheduleStatus,
  getGetApiAdminBackupScheduleStatusQueryKey as getBackupScheduleStatusQueryKey,
  putApiAdminBackupSettings as putBackupScheduleSettings,
} from '@/api/generated/admin/admin';
export type {
  BackupScheduleConfigurationDto,
  BackupScheduleFrequency,
  BackupScheduleLatestRunSummaryDto,
  BackupScheduleStatusResponseDto,
  BackupSettingsPutRequestDto,
  BackupSettingsResponseDto,
} from '@/api/generated/model';

/** Preset crons (UTC, CronFormat.Standard — 5 fields). */
export const BACKUP_SCHEDULE_PRESET_CRONS = {
  daily: '0 2 * * *',
  weeklyMon: '0 2 * * 1',
  monthly1: '0 2 1 * *',
} as const;

export type BackupSchedulePresetId = keyof typeof BACKUP_SCHEDULE_PRESET_CRONS | 'custom';

export function normalizeCronWhitespace(cron: string): string {
  return cron.trim().replace(/\s+/g, ' ');
}

export function detectSchedulePreset(cron: string): BackupSchedulePresetId {
  const n = normalizeCronWhitespace(cron);
  if (n === BACKUP_SCHEDULE_PRESET_CRONS.daily) return 'daily';
  if (n === BACKUP_SCHEDULE_PRESET_CRONS.weeklyMon) return 'weeklyMon';
  if (n === BACKUP_SCHEDULE_PRESET_CRONS.monthly1) return 'monthly1';
  return 'custom';
}

/** Backend uses 5-field standard cron; reject empty or wrong field count before PUT. */
export function isPlausibleStandardCron(cron: string): boolean {
  const n = normalizeCronWhitespace(cron);
  if (!n) return false;
  const fields = n.split(' ');
  return fields.length === 5 && fields.every((f) => f.length > 0);
}
