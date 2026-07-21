'use client';

import type { WorkingHoursSettings } from '@/features/settings/api/workingHoursApi';
import {
  WORKING_HOURS_KEYS,
  useUpdateWorkingHoursSettings,
  useWorkingHoursSettings,
} from '@/features/settings/hooks/useWorkingHoursSettings';

export { useUpdateWorkingHoursSettings, useWorkingHoursSettings, WORKING_HOURS_KEYS };

/**
 * Convenience hook for the working-hours settings page:
 * `{ data, isLoading, update, refetch }`.
 */
export function useWorkingHours() {
  const query = useWorkingHoursSettings();
  const mutation = useUpdateWorkingHoursSettings();

  return {
    data: query.data,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    refetch: query.refetch,
    update: (payload: WorkingHoursSettings) => mutation.mutateAsync(payload),
    isUpdating: mutation.isPending,
    error: query.error ?? mutation.error,
  };
}
