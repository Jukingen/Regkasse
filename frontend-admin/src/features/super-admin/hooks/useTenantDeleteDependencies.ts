import { useQuery } from '@tanstack/react-query';

import { getApiAdminTenantsTenantIdDeleteDependencies } from '@/api/generated/admin/admin';

export const tenantDeleteDependenciesQueryKey = (tenantId: string) =>
  ['admin', 'tenants', tenantId, 'delete-dependencies'] as const;

export function useTenantDeleteDependencies(tenantId: string, enabled = true) {
  return useQuery({
    queryKey: tenantDeleteDependenciesQueryKey(tenantId),
    queryFn: () => getApiAdminTenantsTenantIdDeleteDependencies(tenantId),
    enabled: !!tenantId && enabled,
    staleTime: 60_000,
    retry: false,
  });
}
