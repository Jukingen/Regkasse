'use client';

import dayjs from 'dayjs';
import { useGetApiReportsOperationalSummary } from '@/api/generated/operational-reports/operational-reports';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export type TodaySalesSummary = {
    total: number;
    count: number;
};

/**
 * Register-scoped gross sales for the current local calendar day.
 * Uses GET /api/reports/operational/summary.
 */
export function useTodaySales(cashRegisterId?: string) {
    const today = dayjs().format('YYYY-MM-DD');
    const trimmedRegisterId = cashRegisterId?.trim() ?? '';
    const { isAuthorized } = useAuthorizationGate({ requiredPermission: PERMISSIONS.REPORT_VIEW });

    const query = useGetApiReportsOperationalSummary(
        {
            startDate: today,
            endDate: today,
            cashRegisterId: trimmedRegisterId || undefined,
            activeOnly: true,
        },
        {
            query: {
                enabled: isAuthorized && trimmedRegisterId.length > 0,
                refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
                staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
                refetchOnWindowFocus: true,
            },
        },
    );

    const data: TodaySalesSummary | undefined =
        query.data != null
            ? {
                  total: query.data.grossTotalAmount ?? 0,
                  count: query.data.paymentRowCount ?? 0,
              }
            : undefined;

    return {
        data,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        error: query.error,
        refetch: query.refetch,
    };
}
