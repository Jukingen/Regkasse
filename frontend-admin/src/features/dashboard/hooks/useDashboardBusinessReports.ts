'use client';

import {
    useGetApiReportsCustomers,
    useGetApiReportsPayments,
    useGetApiReportsProducts,
    useGetApiReportsSales,
} from '@/api/generated/reports/reports';

const DASHBOARD_REPORT_STALE_MS = 2 * 60 * 1000;

type DateRangeParams = { startDate: string; endDate: string };

/**
 * Parallel overview report queries with longer stale time (dashboard date picker).
 */
export function useDashboardBusinessReports(params: DateRangeParams, enabled = true) {
    const query = {
        enabled,
        staleTime: DASHBOARD_REPORT_STALE_MS,
        refetchOnWindowFocus: false,
    } as const;

    const sales = useGetApiReportsSales(params, { query });
    const products = useGetApiReportsProducts(params, { query });
    const payments = useGetApiReportsPayments(params, { query });
    const customers = useGetApiReportsCustomers(params, { query });

    const anyLoading =
        sales.isLoading || products.isLoading || payments.isLoading || customers.isLoading;
    const anyError = sales.isError || products.isError || payments.isError || customers.isError;

    return {
        sales,
        products,
        payments,
        customers,
        anyLoading,
        anyError,
    };
}
