'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  endMaintenance,
  getMaintenanceStatus,
  type MaintenanceModeStatusDto,
} from '@/api/manual/maintenanceMode';

export const MAINTENANCE_MODE_STATUS_QUERY_KEY = ['maintenance', 'mode-status'] as const;

/** Aligns with Super Admin maintenance management page cache. */
export const ADMIN_MAINTENANCE_STATUS_QUERY_KEY = ['admin', 'maintenance', 'status'] as const;
export const ADMIN_MAINTENANCE_NOTIFICATIONS_ACTIVE_KEY = [
  'admin',
  'maintenance-notifications',
  'active',
] as const;

const POLL_MS = 60_000;

/**
 * Platform maintenance mode status for FA limited-mode UI (read-mostly banner / disabled writes).
 * Super Admin can end maintenance via `disableMaintenance` (backend already bypasses write blocks).
 */
export function useMaintenanceMode() {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: MAINTENANCE_MODE_STATUS_QUERY_KEY,
    queryFn: ({ signal }) => getMaintenanceStatus(signal),
    refetchInterval: POLL_MS,
    staleTime: 30_000,
    retry: 1,
  });

  const endMutation = useMutation({
    mutationFn: () => endMaintenance(),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: MAINTENANCE_MODE_STATUS_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: ADMIN_MAINTENANCE_STATUS_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: ADMIN_MAINTENANCE_NOTIFICATIONS_ACTIVE_KEY }),
        queryClient.invalidateQueries({ queryKey: ['admin', 'maintenance', 'notifications'] }),
      ]);
    },
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
    disableMaintenance: () => endMutation.mutateAsync(),
    isDisabling: endMutation.isPending,
  };
}
