import { customInstance } from '@/lib/axios';

export type FiskalyStatusDto = {
  isEnabled: boolean;
  isConfigured: boolean;
  environment: string;
  isAuthenticated: boolean;
  lastCheck?: string | null;
  error?: string | null;
  source?: string;
  scuId?: string | null;
  scuState?: string | null;
  scuInitialized?: boolean;
  cashRegisterInitialized?: boolean;
};

export type FiskalySettingsDto = {
  enabled: boolean;
  configEnabled: boolean;
  overrideEnabled?: boolean | null;
  environment: string;
  isConfigured: boolean;
  apiBaseUrl: string;
  source: string;
};

export async function getFiskalyStatus(
  probeAuthentication = true,
  signal?: AbortSignal
): Promise<FiskalyStatusDto> {
  return customInstance<FiskalyStatusDto>({
    url: '/api/admin/fiskaly/status',
    method: 'GET',
    params: { probeAuthentication },
    signal,
  });
}

export async function getFiskalySettings(signal?: AbortSignal): Promise<FiskalySettingsDto> {
  return customInstance<FiskalySettingsDto>({
    url: '/api/admin/fiskaly/settings',
    method: 'GET',
    signal,
  });
}

export async function updateFiskalySettings(
  enabled: boolean,
  signal?: AbortSignal
): Promise<FiskalySettingsDto> {
  return customInstance<FiskalySettingsDto>({
    url: '/api/admin/fiskaly/settings',
    method: 'POST',
    data: { enabled },
    signal,
  });
}
