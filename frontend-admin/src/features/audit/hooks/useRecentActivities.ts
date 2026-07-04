'use client';

import { useQuery } from '@tanstack/react-query';

import { fetchActivities, type ActivityDto } from '@/api/manual/activityEvents';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';

const recentActivitiesQueryKey = (limit: number) => ['admin', 'activities', 'recent', limit] as const;

/**
 * Latest tenant-scoped activity feed entries.
 * Uses GET /api/admin/activities.
 */
export function useRecentActivities(limit = 10) {
    const safeLimit = Math.max(1, Math.min(limit, 50));

    return useQuery({
        queryKey: recentActivitiesQueryKey(safeLimit),
        queryFn: ({ signal }) => fetchActivities({ limit: safeLimit, offset: 0 }, signal),
        staleTime: DASHBOARD_AUTO_REFRESH_MS,
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        refetchOnWindowFocus: true,
        select: (response): ActivityDto[] => response.items ?? [],
    });
}
