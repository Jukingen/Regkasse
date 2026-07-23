'use client';

import { useQuery } from '@tanstack/react-query';

import {
  getMaintenanceStatus,
  type MaintenanceModeStatusDto,
} from '@/api/manual/maintenanceMode';

export const MAINTENANCE_MODE_STATUS_QUERY_KEY = ['maintenance', 'mode-status'] as const;

const POLL_MS = 60_000;

/**
 * Platform maintenance mode status for FA limited-mode UI (read-mostly banner / disabled writes).
 */
export function useMaintenanceMode() {
  const query = useQuery({
    queryKey: MAINTENANCE_MODE_STATUS_QUERY_KEY,
    queryFn: ({ signal }) => getMaintenanceStatus(signal),
    refetchInterval: POLL_MS,
    staleTime: 30_000,
    retry: 1,
  });

  const status: MaintenanceModeStatusDto | undefined = query.data;
  const isMaintenanceMode = Boolean(status?.isActive);
  const blocksWrites = Boolean(status?.blocksApiWrites);

  return {
    status,
    isMaintenanceMode,
    blocksWrites,
    isLoading: query.isLoading,
    isError: query.isError,
    refetch: query.refetch,
  };
}
