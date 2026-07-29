import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { AXIOS_INSTANCE } from '@/lib/axios';
import { PERMISSIONS } from '@/shared/auth/permissions';

export type GracePeriodTenantRow = {
  id: string;
  name: string;
  slug: string;
  expiredAtUtc: string;
  daysRemaining: number;
  lockdownAtUtc: string;
};

export type GracePeriodDashboard = {
  total: number;
  critical: number;
  medium: number;
  good: number;
  list: GracePeriodTenantRow[];
};

export const gracePeriodDashboardQueryKey = ['admin', 'license', 'grace-period'] as const;

/** GET /api/admin/license/grace-period — Super Admin grace cohort overview. */
export async function getGracePeriodDashboard(): Promise<GracePeriodDashboard> {
  const { data } = await AXIOS_INSTANCE.get<GracePeriodDashboard>(
    '/api/admin/license/grace-period'
  );
  return data;
}

export function useGracePeriodDashboard(options?: { enabled?: boolean }) {
  return useAuthorizedQuery({
    queryKey: gracePeriodDashboardQueryKey,
    queryFn: getGracePeriodDashboard,
    requiredRole: 'SuperAdmin',
    requiredPermission: PERMISSIONS.SYSTEM_CRITICAL,
    refetchInterval: 60_000,
    enabled: options?.enabled !== false,
  });
}
