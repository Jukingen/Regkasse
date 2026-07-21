'use client';

import { useQuery } from '@tanstack/react-query';

import { listDataManagementOverview } from '@/features/data-management/api/adminDataManagement';

export const dataManagementOverviewQueryKey = ['admin-data-management-overview'] as const;

export function useDataManagementOverview(enabled = true) {
  return useQuery({
    queryKey: dataManagementOverviewQueryKey,
    queryFn: listDataManagementOverview,
    enabled,
  });
}
