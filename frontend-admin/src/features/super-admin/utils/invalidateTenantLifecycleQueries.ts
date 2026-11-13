import type { QueryClient } from '@tanstack/react-query';

import { tenantDeleteDependenciesQueryKey } from '@/features/super-admin/hooks/useTenantDeleteDependencies';

export const TENANT_DETAIL_QUERY_KEY = ['admin', 'tenant-detail'] as const;
export const ADMIN_TENANTS_QUERY_KEY = ['admin', 'tenants'] as const;

/** Refetch tenant detail, dependency summary, and tenant list after archive/restore/delete. */
export function invalidateTenantLifecycleQueries(
  queryClient: QueryClient,
  tenantId?: string
): void {
  if (tenantId) {
    void queryClient.invalidateQueries({ queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId] });
    void queryClient.invalidateQueries({ queryKey: tenantDeleteDependenciesQueryKey(tenantId) });
  }
  void queryClient.invalidateQueries({ queryKey: ADMIN_TENANTS_QUERY_KEY });
}
