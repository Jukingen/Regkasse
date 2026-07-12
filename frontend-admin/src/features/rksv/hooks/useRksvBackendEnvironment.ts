'use client';

import { useQuery } from '@tanstack/react-query';
import { getRksvBackendEnvironment } from '@/api/admin-rksv/client';
import { rksvAdminQueryKeys } from '@/api/admin-rksv/query-keys';
import {
  isRksvBackendDemo,
  type RksvBackendEnvironmentStatus,
} from '@/features/rksv/types/rksvBackendEnvironment';

const RKSV_ENVIRONMENT_STALE_MS = 5 * 60 * 1000;

export function useRksvBackendEnvironment(options?: { enabled?: boolean }) {
  const enabled = options?.enabled ?? true;

  return useQuery<RksvBackendEnvironmentStatus, unknown>({
    queryKey: rksvAdminQueryKeys.environment,
    queryFn: ({ signal }) => getRksvBackendEnvironment(signal),
    enabled,
    staleTime: RKSV_ENVIRONMENT_STALE_MS,
    refetchOnWindowFocus: true,
    retry: 1,
  });
}

export function useRksvStatus(options?: { enabled?: boolean }) {
  const query = useRksvBackendEnvironment(options);
  const data = query.data ?? null;

  return {
    ...query,
    data,
    isDemo: isRksvBackendDemo(data),
  };
}
