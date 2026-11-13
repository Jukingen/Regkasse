'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { AXIOS_INSTANCE } from '@/lib/axios';

export type DepExportMobilePushSettings = {
  pushEnabled: boolean;
  thirtyDayReminder: boolean;
  sevenDayReminder: boolean;
  oneDayReminder: boolean;
  overdueAlert: boolean;
  successNotification: boolean;
};

export const defaultDepExportMobilePushSettings = (): DepExportMobilePushSettings => ({
  pushEnabled: true,
  thirtyDayReminder: true,
  sevenDayReminder: true,
  oneDayReminder: true,
  overdueAlert: true,
  successNotification: true,
});

export const depExportPushSettingsQueryKeys = {
  all: ['rksv', 'dep-export-push-settings'] as const,
  settings: ['rksv', 'dep-export-push-settings', 'current'] as const,
};

export async function fetchDepExportPushSettings(): Promise<DepExportMobilePushSettings> {
  const response = await AXIOS_INSTANCE.get<DepExportMobilePushSettings>(
    '/api/admin/rksv/dep-export/push-notification-settings'
  );
  return {
    ...defaultDepExportMobilePushSettings(),
    ...response.data,
  };
}

export async function saveDepExportPushSettings(
  settings: DepExportMobilePushSettings
): Promise<DepExportMobilePushSettings> {
  const response = await AXIOS_INSTANCE.put<DepExportMobilePushSettings>(
    '/api/admin/rksv/dep-export/push-notification-settings',
    settings
  );
  return {
    ...defaultDepExportMobilePushSettings(),
    ...response.data,
  };
}

export function useDepExportPushSettings() {
  return useQuery({
    queryKey: depExportPushSettingsQueryKeys.settings,
    queryFn: fetchDepExportPushSettings,
    staleTime: 30_000,
  });
}

export function useSaveDepExportPushSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: saveDepExportPushSettings,
    onSuccess: (data) => {
      queryClient.setQueryData(depExportPushSettingsQueryKeys.settings, data);
    },
  });
}
