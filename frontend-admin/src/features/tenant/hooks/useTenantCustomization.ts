'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type TenantCustomization,
  type UpsertTenantCustomizationInput,
  fetchTenantCustomization,
  upsertTenantCustomization,
} from '@/features/website-generator/api/tenantCustomizationApi';

export type UseTenantCustomizationOptions = {
  tenantId?: string;
  surface?: 'website' | 'app';
  enabled?: boolean;
};

/**
 * Load / save tenant website or app branding via `/api/admin/tenant-customizations`.
 */
export function useTenantCustomization({
  tenantId,
  surface = 'website',
  enabled = true,
}: UseTenantCustomizationOptions) {
  const queryClient = useQueryClient();
  const queryKey = ['admin', 'tenant-customizations', surface, tenantId ?? 'ambient'] as const;

  const query = useQuery({
    queryKey,
    queryFn: () => fetchTenantCustomization(surface, tenantId),
    enabled,
  });

  const mutation = useMutation({
    mutationFn: (input: Omit<UpsertTenantCustomizationInput, 'type' | 'tenantId'>) =>
      upsertTenantCustomization({
        ...input,
        type: surface,
        tenantId,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKey, data);
      void queryClient.invalidateQueries({ queryKey: ['admin', 'tenant-customizations'] });
    },
  });

  return {
    data: query.data as TenantCustomization | undefined,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    refetch: query.refetch,
    update: mutation.mutateAsync,
    isUpdating: mutation.isPending,
  };
}
