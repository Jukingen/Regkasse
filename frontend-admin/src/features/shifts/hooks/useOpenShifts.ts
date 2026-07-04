'use client';

import { useQuery } from '@tanstack/react-query';

import {
    adminShiftOverviewQueryKey,
    fetchAdminShiftOverview,
    type AdminShiftRow,
} from '@/features/shifts/api/shiftsOverview';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Active (open) shifts for the tenant, optionally scoped to one cash register.
 * Uses GET /api/admin/shifts/overview.
 */
export function useOpenShifts(cashRegisterId?: string) {
    const trimmedRegisterId = cashRegisterId?.trim() ?? '';
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.SHIFT_VIEW });

    const query = useQuery({
        queryKey: adminShiftOverviewQueryKey({
            cashRegisterId: trimmedRegisterId || undefined,
        }),
        queryFn: () =>
            fetchAdminShiftOverview({
                cashRegisterId: trimmedRegisterId || undefined,
            }),
        enabled: isAuthorized,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
        refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
        refetchOnWindowFocus: true,
    });

    const data: AdminShiftRow[] = query.data?.activeShifts ?? [];

    return {
        data,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        error: query.error,
        refetch: query.refetch,
    };
}
