import { customInstance } from '@/lib/axios';

export const developmentModeSettingsQueryKey = ['admin', 'development-mode', 'settings'] as const;

/** Anonymous POS/FA poll of persisted toggles (same fields as admin read model; `updatedBy` omitted on wire). */
export const publicDevelopmentModeQueryKey = ['system', 'development-mode'] as const;

export type DevelopmentModeSettingsDto = {
  enabled: boolean;
  bypassLicense: boolean;
  bypassNtpCheck: boolean;
  bypassTseCheck: boolean;
  simulateOffline: boolean;
  forceOnline: boolean;
  validDays: number;
  features: string[];
  updatedAtUtc: string;
  updatedBy: string | null;
};

export type DevelopmentModeSettingsPutDto = {
  enabled: boolean;
  bypassLicense: boolean;
  bypassNtpCheck: boolean;
  bypassTseCheck: boolean;
  simulateOffline: boolean;
  forceOnline: boolean;
  validDays: number;
  features: string[];
};

export function fetchDevelopmentModeSettings(): Promise<DevelopmentModeSettingsDto> {
  return customInstance<DevelopmentModeSettingsDto>({
    url: '/api/admin/development-mode/settings',
    method: 'GET',
  });
}

export function fetchPublicDevelopmentModeSettings(): Promise<DevelopmentModeSettingsDto> {
  return customInstance<DevelopmentModeSettingsDto>({
    url: '/api/system/development-mode',
    method: 'GET',
  });
}

export function putDevelopmentModeSettings(
  body: DevelopmentModeSettingsPutDto,
): Promise<DevelopmentModeSettingsDto> {
  return customInstance<DevelopmentModeSettingsDto>({
    url: '/api/admin/development-mode/settings',
    method: 'PUT',
    data: body,
  });
}
