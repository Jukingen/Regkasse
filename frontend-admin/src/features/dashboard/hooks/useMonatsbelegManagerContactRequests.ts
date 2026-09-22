'use client';

import { useQuery } from '@tanstack/react-query';

import { fetchActivities } from '@/api/manual/activityEvents';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import {
  MONATSBELEG_ACTIVITY_LIST_LIMIT,
  countMonatsbelegManagerContactedSince,
  monatsbelegManagerContactedSinceUtc,
} from '@/features/dashboard/utils/countMonatsbelegManagerContactedSince';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Count of `MonatsbelegManagerContacted` activities in the last 7 days.
 * GET /api/admin/activities (`settings.view`). Client-side type filter; `limit: 50` is a ceiling
 * (future pagination is a separate change).
 */
export function useMonatsbelegManagerContactRequests() {
  const { isAuthorized } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.SETTINGS_VIEW,
  });

  return useQuery({
    queryKey: ['admin', 'activities', 'monatsbeleg-manager-contacted', MONATSBELEG_ACTIVITY_LIST_LIMIT],
    queryFn: ({ signal }) =>
      fetchActivities({ limit: MONATSBELEG_ACTIVITY_LIST_LIMIT, offset: 0 }, signal),
    enabled: isAuthorized,
    staleTime: DASHBOARD_AUTO_REFRESH_MS,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
    refetchOnWindowFocus: true,
    select: (response) =>
      countMonatsbelegManagerContactedSince(response.items, monatsbelegManagerContactedSinceUtc()),
  });
}
