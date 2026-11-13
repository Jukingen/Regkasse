'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  clearDevLimitCache,
  devLimitsQueryKey,
  getDevLimitStatus,
  resetAllDevLimits,
  setDevLimit,
  triggerDevLimitScenario,
  type SetDevLimitPayload,
  type TriggerDevLimitScenarioPayload,
} from '@/features/dev-limits/api/devLimits';
import { tenantLimitsQueryKeys } from '@/features/tenants/api/tenantLimits';

const STALE_MS = 15 * 1000;

export function useDevLimitStatus(tenantId?: string | null) {
  return useQuery({
    queryKey: devLimitsQueryKey(tenantId),
    queryFn: async () => {
      if (!tenantId) throw new Error('Tenant ID required');
      return getDevLimitStatus(tenantId);
    },
    enabled: isDevelopment() && Boolean(tenantId),
    staleTime: STALE_MS,
    refetchOnMount: true,
    refetchOnWindowFocus: true,
  });
}

function invalidateLimitQueries(
  queryClient: ReturnType<typeof useQueryClient>,
  tenantId: string
): void {
  void queryClient.invalidateQueries({ queryKey: devLimitsQueryKey(tenantId) });
  void queryClient.invalidateQueries({ queryKey: tenantLimitsQueryKeys.byTenant(tenantId) });
  void queryClient.invalidateQueries({ queryKey: tenantLimitsQueryKeys.usage });
}

export function useDevLimitMutations() {
  const queryClient = useQueryClient();

  const applyCache = (nextTenantId: string, data: unknown) => {
    queryClient.setQueryData(devLimitsQueryKey(nextTenantId), data);
    invalidateLimitQueries(queryClient, nextTenantId);
  };

  const setMutation = useMutation({
    mutationFn: (payload: SetDevLimitPayload) => setDevLimit(payload),
    onSuccess: (data, variables) => applyCache(variables.tenantId, data),
  });

  const resetMutation = useMutation({
    mutationFn: (id: string) => resetAllDevLimits(id),
    onSuccess: (data, id) => applyCache(id, data),
  });

  const scenarioMutation = useMutation({
    mutationFn: (payload: TriggerDevLimitScenarioPayload) => triggerDevLimitScenario(payload),
    onSuccess: (data, variables) => applyCache(variables.tenantId, data),
  });

  const cacheMutation = useMutation({
    mutationFn: (id: string) => clearDevLimitCache(id),
    onSuccess: (data, id) => applyCache(id, data),
  });

  return {
    setMutation,
    resetMutation,
    scenarioMutation,
    cacheMutation,
    isPending:
      setMutation.isPending ||
      resetMutation.isPending ||
      scenarioMutation.isPending ||
      cacheMutation.isPending,
  };
}
