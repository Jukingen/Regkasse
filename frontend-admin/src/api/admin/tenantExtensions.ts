import { useQuery, type UseQueryOptions, type UseQueryResult } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export interface TenantExtensionStatus {
  name: string;
  enabled: boolean;
  hooks: string[];
}

export interface SetTenantExtensionRequest {
  name: string;
  enabled: boolean;
}

export const tenantExtensionQueryKeys = {
  all: ['admin', 'tenant-extensions'] as const,
  list: () => [...tenantExtensionQueryKeys.all, 'list'] as const,
};

export function listTenantExtensions(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<TenantExtensionStatus[]> {
  return customInstance<TenantExtensionStatus[]>(
    { url: '/api/admin/tenant/extensions', method: 'GET', signal },
    options
  ).then((res) => unwrapData<TenantExtensionStatus[]>(res));
}

export function setTenantExtension(
  body: SetTenantExtensionRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<TenantExtensionStatus> {
  return customInstance<TenantExtensionStatus>(
    { url: '/api/admin/tenant/extensions', method: 'PUT', data: body },
    options
  ).then((res) => unwrapData<TenantExtensionStatus>(res));
}

export function useTenantExtensions(
  options?: Partial<UseQueryOptions<TenantExtensionStatus[], Error, TenantExtensionStatus[]>>
): UseQueryResult<TenantExtensionStatus[], Error> {
  return useQuery({
    queryKey: tenantExtensionQueryKeys.list(),
    queryFn: ({ signal }) => listTenantExtensions(undefined, signal),
    ...options,
  });
}
