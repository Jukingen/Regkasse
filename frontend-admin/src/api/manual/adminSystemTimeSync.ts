import { AXIOS_INSTANCE } from '@/lib/axios';

export type NtpAdminConfigurationDto = {
  autoSyncEnabled: boolean;
  syncIntervalMinutes: number;
  maxAllowedOffsetSeconds: number;
  criticalOffsetSeconds: number;
  hasDatabaseOverride: boolean;
  updatedAtUtc: string | null;
};

export type AdminTimeSyncStatusDto = {
  systemTimeUtc: string;
  systemTimeLocalVienna: string;
  ntpTimeUtc: string | null;
  offsetSeconds: number | null;
  isSynchronized: boolean;
  lastSyncAt: string | null;
  warningLevel: string;
  statusBadge: string;
  effectiveConfiguration: NtpAdminConfigurationDto;
};

export type SystemTimeSyncLogEntryDto = {
  id: string;
  syncTimeUtc: string;
  offsetSeconds: number;
  ntpServerUsed: string;
  isSuccess: boolean;
  errorMessage: string | null;
};

export type NtpManualSyncResponseDto = {
  success: boolean;
  message: string;
  offsetSeconds: number | null;
  syncTimeUtc: string;
};

export type NtpAdminConfigurationUpdateDto = {
  autoSyncEnabled: boolean;
  syncIntervalMinutes: number;
  maxAllowedOffsetSeconds: number;
  criticalOffsetSeconds: number;
};

export type TimeSyncDriftSummaryDto = {
  hasActiveDrift: boolean;
  registerCountOverThreshold: number;
  largestAbsoluteOffsetSeconds: number | null;
  maxAllowedOffsetSecondsThreshold: number;
};

export async function getAdminTimeSyncStatus(): Promise<AdminTimeSyncStatusDto> {
  const { data } = await AXIOS_INSTANCE.get<AdminTimeSyncStatusDto>('/api/admin/system/time-sync');
  return data;
}

export async function getAdminTimeSyncLogs(): Promise<SystemTimeSyncLogEntryDto[]> {
  const { data } = await AXIOS_INSTANCE.get<SystemTimeSyncLogEntryDto[]>(
    '/api/admin/system/time-sync/logs'
  );
  return data;
}

export async function getTimeSyncDriftSummary(): Promise<TimeSyncDriftSummaryDto> {
  const { data } = await AXIOS_INSTANCE.get<TimeSyncDriftSummaryDto>(
    '/api/admin/system/time-sync/drift-summary'
  );
  return data;
}

export async function postManualTimeSync(): Promise<NtpManualSyncResponseDto> {
  const { data } = await AXIOS_INSTANCE.post<NtpManualSyncResponseDto>('/api/system/time/sync');
  return data;
}

export async function putAdminTimeSyncConfiguration(
  body: NtpAdminConfigurationUpdateDto
): Promise<NtpAdminConfigurationDto> {
  const { data } = await AXIOS_INSTANCE.put<NtpAdminConfigurationDto>(
    '/api/admin/system/time-sync/configuration',
    body
  );
  return data;
}
