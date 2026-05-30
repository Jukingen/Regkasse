'use client';

import { useQuery } from '@tanstack/react-query';
import type { Dayjs } from 'dayjs';

import { fetchPaymentTrends } from '@/features/payments/api/paymentTrends';
import type { TrendPeriod } from '@/features/payments/types/paymentTrends';
import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { usePermissions } from '@/shared/auth/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const trendsKey = ['admin', 'payments', 'trends'] as const;

export type PaymentTrendsDateRange = [Dayjs, Dayjs] | null;

export function usePaymentTrendsAccess(): boolean {
    const { hasPermission } = usePermissions();
    return hasPermission(PERMISSIONS.PAYMENT_VIEW);
}

export function usePaymentTrends(
    period: TrendPeriod,
    dateRange?: PaymentTrendsDateRange,
    enabled = true,
) {
    const canSee = usePaymentTrendsAccess();
    const startDate = dateRange?.[0]?.format('YYYY-MM-DD');
    const endDate = dateRange?.[1]?.format('YYYY-MM-DD');

    return useQuery({
        queryKey: [...trendsKey, { period, startDate, endDate }],
        queryFn: ({ signal }) =>
            fetchPaymentTrends(
                {
                    period,
                    startDate,
                    endDate,
                },
                signal,
            ),
        enabled: enabled && canSee,
        staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
        refetchInterval: enabled && canSee && !dateRange ? DASHBOARD_AUTO_REFRESH_MS : false,
    });
}
