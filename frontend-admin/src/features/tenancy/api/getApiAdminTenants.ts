import type { UseQueryOptions } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type GetApiAdminTenantsParams = {
  includeDeleted?: boolean;
};

export const getApiAdminTenantsQueryKey = (includeDeleted = false) =>
  ['api', 'admin', 'tenants', includeDeleted] as const;

/** GET /api/tenants/switcher — SuperAdmin sees all; others see active memberships only. */
export async function getApiAdminTenants(includeDeleted = false): Promise<AdminTenantListItem[]> {
  const { data } = await AXIOS_INSTANCE.get<AdminTenantListItem[]>('/api/tenants/switcher', {
    params: { includeDeleted },
  });
  return data;
}

export function useGetApiAdminTenants<TData = AdminTenantListItem[]>(
  params?: GetApiAdminTenantsParams,
  options?: Omit<
    UseQueryOptions<
      AdminTenantListItem[],
      Error,
      TData,
      ReturnType<typeof getApiAdminTenantsQueryKey>
    >,
    'queryKey' | 'queryFn'
  >
) {
  const includeDeleted = params?.includeDeleted ?? false;
  return useQuery({
    queryKey: getApiAdminTenantsQueryKey(includeDeleted),
    queryFn: () => getApiAdminTenants(includeDeleted),
    ...options,
  });
}
