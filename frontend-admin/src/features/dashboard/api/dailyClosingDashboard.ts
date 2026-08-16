import { useGetApiAdminDailyClosingDashboardSummary } from '@/api/generated/admin/admin';
import { DASHBOARD_DAILY_CLOSING_REFRESH_MS } from '@/features/dashboard/types';

export function useDailyClosingDashboardSummary(
  cashRegisterId: string | undefined,
  enabled: boolean
) {
  return useGetApiAdminDailyClosingDashboardSummary(
    { cashRegisterId },
    {
      query: {
        enabled,
        staleTime: DASHBOARD_DAILY_CLOSING_REFRESH_MS / 2,
        refetchInterval: DASHBOARD_DAILY_CLOSING_REFRESH_MS,
        refetchIntervalInBackground: false,
        refetchOnWindowFocus: true,
      },
    }
  );
}
