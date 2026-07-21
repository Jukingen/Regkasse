import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import {
  OFFLINE_MONITORING_QUERY_KEY,
  fetchOfflineMonitoringStatus,
} from '@/features/offline/api/offlineMonitoringApi';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export function useOfflineMonitoring() {
  return useAuthorizedQuery({
    queryKey: OFFLINE_MONITORING_QUERY_KEY,
    queryFn: ({ signal }) => fetchOfflineMonitoringStatus(signal),
    requiredPermission: PERMISSIONS.PAYMENT_VIEW,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: false,
  });
}
