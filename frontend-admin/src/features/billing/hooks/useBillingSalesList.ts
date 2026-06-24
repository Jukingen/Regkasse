'use client';

import type { LicenseSaleListQuery } from '@/features/billing/api/billingApi';
import { billingApi } from '@/features/billing/api/billingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export type BillingSalesListFilters = LicenseSaleListQuery;

export function useBillingSalesList(filters: BillingSalesListFilters) {
    const canAccess = useBillingAccess();

    return billingApi.useList(
        {
            page: filters.page,
            pageSize: filters.pageSize,
            tenantId: filters.tenantId,
            search: filters.search?.trim() || undefined,
            status: filters.status && filters.status !== 'all' ? filters.status : undefined,
            fromDate: filters.fromDate,
            toDate: filters.toDate,
        },
        { query: { enabled: canAccess } },
    );
}
