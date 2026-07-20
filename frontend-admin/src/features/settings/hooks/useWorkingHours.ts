'use client';

import {
    useUpdateWorkingHoursSettings,
    useWorkingHoursSettings,
    WORKING_HOURS_KEYS,
} from '@/features/settings/hooks/useWorkingHoursSettings';
import type { WorkingHoursSettings } from '@/features/settings/api/workingHoursApi';

export { WORKING_HOURS_KEYS, useWorkingHoursSettings, useUpdateWorkingHoursSettings };

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
