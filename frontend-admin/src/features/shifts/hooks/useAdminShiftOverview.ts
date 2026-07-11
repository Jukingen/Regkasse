import { useQuery } from '@tanstack/react-query';

import {
  adminShiftOverviewQueryKey,
  fetchAdminShiftOverview,
  type AdminShiftOverviewParams,
} from '@/features/shifts/api/shiftsOverview';

export function useAdminShiftOverview(params: AdminShiftOverviewParams = {}) {
  return useQuery({
    queryKey: adminShiftOverviewQueryKey(params),
    queryFn: () => fetchAdminShiftOverview(params),
    refetchOnMount: true,
    refetchOnWindowFocus: true,
    staleTime: 10_000,
  });
}
