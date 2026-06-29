'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  fetchOfflineSettings,
  updateOfflineSettings,
  type OfflineSettings,
  type UpdateOfflineSettingsPayload,
} from '@/features/settings/api/offlineSettingsApi';

export const OFFLINE_SETTINGS_KEYS = {
  all: ['offline-settings'] as const,
};

export function useOfflineSettings() {
  return useQuery({
    queryKey: OFFLINE_SETTINGS_KEYS.all,
    queryFn: fetchOfflineSettings,
    staleTime: 60_000,
  });
}

export function useUpdateOfflineSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: UpdateOfflineSettingsPayload) => updateOfflineSettings(payload),
    onSuccess: (data: OfflineSettings) => {
      queryClient.setQueryData(OFFLINE_SETTINGS_KEYS.all, data);
    },
  });
}
