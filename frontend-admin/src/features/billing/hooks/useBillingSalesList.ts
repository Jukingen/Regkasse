'use client';

import { useQuery } from '@tanstack/react-query';

import { getApiAdminBillingLicenseSales } from '@/api/generated/admin/admin';
import type { LicenseSaleListQuery } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export type BillingSalesListFilters = LicenseSaleListQuery;

function normalizeSalesListParams(filters: BillingSalesListFilters) {
  return {
    page: filters.page,
    pageSize: filters.pageSize,
    tenantId: filters.tenantId,
    search: filters.search?.trim() || undefined,
    status: filters.status && filters.status !== 'all' ? filters.status : undefined,
    fromDate: filters.fromDate,
    toDate: filters.toDate,
  };
}

export function useBillingSalesList(filters: BillingSalesListFilters) {
  const canAccess = useBillingAccess();
  const params = normalizeSalesListParams(filters);

  return useQuery({
    queryKey: billingQueryKeys.salesList(params),
    queryFn: () => getApiAdminBillingLicenseSales(params),
    enabled: canAccess,
    staleTime: 30 * 1000,
  });
}
