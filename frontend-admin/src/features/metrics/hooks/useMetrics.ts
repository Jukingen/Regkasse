'use client';

import { useQuery } from '@tanstack/react-query';

import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { fetchSystemMetricsSummary, metricsQueryKeys } from '@/features/metrics/api/metricsApi';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export function useMetrics() {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.SYSTEM_CRITICAL,
  });

  return useQuery({
    queryKey: metricsQueryKeys.summary,
    queryFn: fetchSystemMetricsSummary,
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    refetchOnWindowFocus: true,
  });
}
