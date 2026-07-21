import type { QueryClient } from '@tanstack/react-query';

import { licenseQueryKeys, tenantLicenseUnifiedQueryKey } from '@/api/manual/adminLicense';
import { licenseHistoryQueryKeys } from '@/features/license/api/licenseHistory';
import { tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import { tenantLicenseOverviewQueryKey } from '@/features/license/api/tenantLicenseOverview';
import { getApiAdminTenantsQueryKey } from '@/features/tenancy/api/getApiAdminTenants';
import { currentTenantQueryKey } from '@/features/tenancy/api/getCurrentTenant';

const INVALIDATE_ALL = { refetchType: 'all' as const };

/** Refetch all FA surfaces that display mandant license state (page, header, switcher, overview). */
export async function invalidateTenantLicenseQueries(
  queryClient: QueryClient,
  tenantId?: string | null
): Promise<void> {
  const tasks: Array<Promise<void>> = [
    queryClient.invalidateQueries({ queryKey: currentTenantQueryKey, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: tenantLicenseUnifiedQueryKey, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: tenantLicenseQueryKeys.root, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: tenantLicenseOverviewQueryKey, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: licenseQueryKeys.publicStatus, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({
      queryKey: licenseQueryKeys.deploymentStatus,
      ...INVALIDATE_ALL,
    }),
    queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status, ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'], ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({ queryKey: ['api', 'admin', 'tenants'], ...INVALIDATE_ALL }),
    queryClient.invalidateQueries({
      queryKey: getApiAdminTenantsQueryKey(false),
      ...INVALIDATE_ALL,
    }),
    queryClient.invalidateQueries({
      queryKey: getApiAdminTenantsQueryKey(true),
      ...INVALIDATE_ALL,
    }),
  ];

  if (tenantId) {
    tasks.push(
      queryClient.invalidateQueries({
        queryKey: tenantLicenseQueryKeys.detail(tenantId),
        ...INVALIDATE_ALL,
      }),
      queryClient.invalidateQueries({
        queryKey: licenseHistoryQueryKeys.detail(tenantId),
        ...INVALIDATE_ALL,
      })
    );
  }

  await Promise.all(tasks);

  // Refetch mounted queries immediately (header badge while navigating from test panel).
  await Promise.all([
    queryClient.refetchQueries({ queryKey: currentTenantQueryKey, type: 'active' }),
    queryClient.refetchQueries({ queryKey: tenantLicenseUnifiedQueryKey, type: 'active' }),
    queryClient.refetchQueries({ queryKey: tenantLicenseQueryKeys.root, type: 'active' }),
    queryClient.refetchQueries({ queryKey: getApiAdminTenantsQueryKey(false), type: 'active' }),
    tenantId
      ? queryClient.refetchQueries({
          queryKey: tenantLicenseQueryKeys.detail(tenantId),
          type: 'active',
        })
      : Promise.resolve(),
  ]);
}
