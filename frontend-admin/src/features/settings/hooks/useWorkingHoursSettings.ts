'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type WorkingHoursSettings,
  fetchWorkingHours,
  updateWorkingHours,
} from '@/features/settings/api/workingHoursApi';

export const WORKING_HOURS_KEYS = {
  all: ['working-hours'] as const,
};

export function useWorkingHoursSettings() {
  return useQuery({
    queryKey: WORKING_HOURS_KEYS.all,
    queryFn: fetchWorkingHours,
    staleTime: 60_000,
  });
}

export function useUpdateWorkingHoursSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: WorkingHoursSettings) => updateWorkingHours(payload),
    onSuccess: (data: WorkingHoursSettings) => {
      queryClient.setQueryData(WORKING_HOURS_KEYS.all, data);
    },
  });
}
