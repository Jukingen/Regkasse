import { useMutation, useQueryClient } from '@tanstack/react-query';

import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';

import {
  type TenantLimitsDto,
  type UpdateTenantLimitsRequest,
  getLimitDashboard,
  getTenantLimitUsage,
  getTenantLimits,
  resetTenantLimits,
  tenantLimitsQueryKeys,
  updateTenantLimits,
} from '@/features/tenants/api/tenantLimits';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export function useTenantLimits(tenantId: string) {
  return useAuthorizedQuery({
    queryKey: tenantLimitsQueryKeys.byTenant(tenantId),
    queryFn: () => getTenantLimits(tenantId),
    requiredRole: 'SuperAdmin',
    requiredPermission: PERMISSIONS.SYSTEM_CRITICAL,
    enabled: !!tenantId,
  });
}

export function useUpdateTenantLimits(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateTenantLimitsRequest) => updateTenantLimits(tenantId, body),
    onSuccess: (data: TenantLimitsDto) => {
      queryClient.setQueryData(tenantLimitsQueryKeys.byTenant(tenantId), data);
      void queryClient.invalidateQueries({ queryKey: tenantLimitsQueryKeys.root });
    },
  });
}

export function useResetTenantLimits(tenantId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => resetTenantLimits(tenantId),
    onSuccess: (data: TenantLimitsDto) => {
      queryClient.setQueryData(tenantLimitsQueryKeys.byTenant(tenantId), data);
      void queryClient.invalidateQueries({ queryKey: tenantLimitsQueryKeys.root });
    },
  });
}

/** Ambient-tenant caps + live usage for FA warnings (products / users / dashboard). */
export function useTenantLimitUsage(enabled = true) {
  return useAuthorizedQuery({
    queryKey: tenantLimitsQueryKeys.usage,
    queryFn: getTenantLimitUsage,
    enabled,
  });
}

export function useLimitDashboard(
  options: { allTenants: boolean; tenantId?: string | null },
  enabled = true
) {
  return useAuthorizedQuery({
    queryKey: tenantLimitsQueryKeys.dashboard(options.allTenants, options.tenantId),
    queryFn: () =>
      getLimitDashboard({ allTenants: options.allTenants, tenantId: options.tenantId }),
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
    enabled,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchIntervalInBackground: false,
  });
}
