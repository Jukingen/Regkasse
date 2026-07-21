'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';

import {
  getTenantLicenseOverview,
  tenantLicenseOverviewQueryKey,
} from '@/features/license/api/tenantLicenseOverview';

export function useTenantLicenseOverview(enabled = true) {
  return useQuery({
    queryKey: tenantLicenseOverviewQueryKey,
    queryFn: getTenantLicenseOverview,
    enabled,
  });
}

export function useInvalidateTenantLicenseOverview() {
  const queryClient = useQueryClient();

  return useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: tenantLicenseOverviewQueryKey });
    void queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
  }, [queryClient]);
}
