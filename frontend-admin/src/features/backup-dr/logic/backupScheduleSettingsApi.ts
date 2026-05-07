/**
 * Backup automation schedule settings (GET/PUT /api/admin/backup/settings, schedule status).
 * Manual integration until Orval regenerates from OpenAPI.
 */

import { customInstance } from '@/lib/axios';

export const BACKUP_SCHEDULE_SETTINGS_PATH = '/api/admin/backup/settings' as const;
export const BACKUP_SCHEDULE_STATUS_PATH = '/api/admin/backup/schedule/status' as const;

export function getBackupScheduleSettingsQueryKey() {
  return [BACKUP_SCHEDULE_SETTINGS_PATH] as const;
}

export function getBackupScheduleStatusQueryKey() {
  return [BACKUP_SCHEDULE_STATUS_PATH] as const;
}

export interface BackupSettingsResponseDto {
  enabled: boolean;
  scheduleCron: string;
  retentionDays: number;
  lastRunAtUtc?: string | null;
  nextRunAtUtc?: string | null;
  updatedAtUtc: string;
}

export interface BackupSettingsPutRequestDto {
  enabled: boolean;
  scheduleCron: string;
  retentionDays: number;
}

export interface BackupScheduleLatestRunSummaryDto {
  id: string;
  status: number;
  requestedAt: string;
  completedAt?: string | null;
  failureCode?: string | null;
  failureDetail?: string | null;
}

export interface BackupScheduleStatusResponseDto {
  databaseAutomationEnabled: boolean;
  scheduleCronUtc: string;
  storedLastRunAtUtc?: string | null;
  storedNextRunAtUtc?: string | null;
  computedNextRunAtUtc?: string | null;
  latestScheduledBackupRun?: BackupScheduleLatestRunSummaryDto | null;
}

export async function getBackupScheduleSettings(): Promise<BackupSettingsResponseDto> {
  return customInstance<BackupSettingsResponseDto>({
    url: BACKUP_SCHEDULE_SETTINGS_PATH,
    method: 'GET',
  });
}

export async function putBackupScheduleSettings(
  body: BackupSettingsPutRequestDto,
): Promise<BackupSettingsResponseDto> {
  return customInstance<BackupSettingsResponseDto>({
    url: BACKUP_SCHEDULE_SETTINGS_PATH,
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}

export async function getBackupScheduleStatus(): Promise<BackupScheduleStatusResponseDto> {
  return customInstance<BackupScheduleStatusResponseDto>({
    url: BACKUP_SCHEDULE_STATUS_PATH,
    method: 'GET',
  });
}

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
